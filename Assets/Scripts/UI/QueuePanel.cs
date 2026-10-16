using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>
    /// The Queue button and the panel it opens: the actions waiting to resolve, in order, as the screen's player sees them
    /// (see <see cref="QueueView"/>, Fog of War included). Each action is an item, edged in its side's color, with an icon for
    /// what it is; hovering one tells more about it, and the board shows that action's arrows alone. It follows the queue as it changes, and the fog switch (F9). Clicking
    /// anywhere else closes it.
    /// The arrows by its heading page back through past Days: what was planned for each, as the screen saw it then.
    /// <see cref="BoardSkin"/> places both at the right edge of the table.
    /// </summary>
    public class QueuePanel : MonoBehaviour {
        public const string ToggleName = "QueueButton";
        public const string PanelName = "QueuePanel";
        public const string ItemName = "QueueItem ";
        private static readonly Color OursColor = UITheme.Hex(0x8FE3A8);
        private static readonly Color TheirsColor = UITheme.Hex(0xF19A8B);

        private IGameManager gameManager;
        private IGameMediator gameMediator;
        private Button toggle;
        private TextMeshProUGUI toggleLabel;
        private RectTransform panel;
        private Transform list;
        private TextMeshProUGUI empty;
        private TextMeshProUGUI heading;
        private Button earlier;
        private Button later;

        // Past Days: what the queue held when each was ended, as the screen's player saw it
        private readonly List<(int Day, List<QueueView.Item> Items)> history = new List<(int, List<QueueView.Item>)>();
        private List<QueueView.Item> lastPlanned = new List<QueueView.Item>();
        private int? shownPast; // An index into history, or null for the queue as it is now

        public RectTransform Toggle => (RectTransform)toggle.transform;
        public RectTransform Panel => panel;
        public bool IsOpen => panel != null && panel.gameObject.activeSelf;
        /// <summary>What the panel lists, one line per action and layer heading.</summary>
        public List<string> Lines { get; private set; } = new List<string>();
        /// <summary>The rows shown, headings included, top to bottom.</summary>
        public List<QueueView.Item> Items { get; private set; } = new List<QueueView.Item>();
        /// <summary>The past Days kept, oldest first.</summary>
        public IReadOnlyList<int> PastDays => history.Select(h => h.Day).ToList();
        /// <summary>The past Day shown, or null for the queue as it is now.</summary>
        public int? ShownDay => shownPast.HasValue ? history[shownPast.Value].Day : (int?)null;

        /// <summary>Makes the button and its (closed) panel next to <paramref name="sibling"/>, at its scale.</summary>
        public static QueuePanel Create(Transform sibling, IGameManager manager, IGameMediator mediator) {
            var host = new GameObject("QueueView", typeof(RectTransform));
            host.layer = sibling.gameObject.layer;
            host.transform.SetParent(sibling.parent, false);
            var view = host.AddComponent<QueuePanel>();
            view.Build(sibling.localScale);
            view.gameManager = manager;
            view.gameMediator = mediator;
            mediator.ActionsQueueChanged += view.Refresh;
            mediator.TurnEnded += view.OnTurnEnded;
            FogOfWar.Changed += view.Refresh;
            view.Refresh();
            return view;
        }

        private void Build(Vector3 scale) {
            var buttonImage = UITheme.NewImage(ToggleName, transform, UITheme.PanelLight, raycastTarget: true);
            buttonImage.rectTransform.localScale = scale;
            buttonImage.rectTransform.sizeDelta = new Vector2(220f, 70f);
            toggle = buttonImage.gameObject.AddComponent<Button>();
            toggleLabel = UITheme.NewText("Label", toggle.transform, "Queue", 28f, UITheme.Parchment);
            UITheme.Stretch(toggleLabel.rectTransform, 6f);
            UITheme.StyleButton(toggle);
            toggle.onClick.AddListener(() => SetOpen(!IsOpen));

            var panelImage = UITheme.NewImage(PanelName, transform, UITheme.Panel, raycastTarget: true);
            panel = panelImage.rectTransform;
            panel.localScale = scale;
            panel.sizeDelta = new Vector2(640f, 600f);
            UITheme.StylePanel(panelImage);

            heading = UITheme.NewText("Heading", panel, "Action Queue", 30f, UITheme.Gold, TextAlignmentOptions.Top);
            UITheme.StyleHeading(heading);
            var headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0f, 1f);
            headingRect.anchorMax = new Vector2(1f, 1f);
            headingRect.pivot = new Vector2(0.5f, 1f);
            headingRect.sizeDelta = new Vector2(0f, 44f);
            headingRect.anchoredPosition = new Vector2(0f, -12f);
            earlier = PageButton("Earlier", "<", 0f, () => Page(-1));
            later = PageButton("Later", ">", 1f, () => Page(+1));

            // The items, in a list that scrolls with the wheel when it's longer than the panel
            var viewport = UITheme.NewImage("Viewport", panel, Color.clear, raycastTarget: true);
            UITheme.Stretch(viewport.rectTransform, 18f);
            viewport.rectTransform.offsetMax = new Vector2(-18f, -62f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Items", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            list = content.transform;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            WheelScroll.AddTo(scroll);

            empty = UITheme.NewText("Empty", panel, "<i>Nothing planned yet.</i>", 24f, UITheme.Parchment, TextAlignmentOptions.Top);
            UITheme.Stretch(empty.rectTransform, 22f);
            empty.rectTransform.offsetMax = new Vector2(-22f, -70f);
            empty.raycastTarget = false;
            panel.gameObject.SetActive(false);
        }

        // A small arrow button at one end of the heading, to page through the Days
        private Button PageButton(string name, string label, float side, UnityEngine.Events.UnityAction onClick) {
            var image = UITheme.NewImage(name, panel, UITheme.PanelLight, raycastTarget: true);
            UITheme.Place(image.rectTransform, new Vector2(side, 1f), new Vector2(side == 0f ? 46f : -46f, -34f), new Vector2(56f, 44f));
            var button = image.gameObject.AddComponent<Button>();
            var text = UITheme.NewText("Label", image.transform, label, 28f, UITheme.Parchment);
            UITheme.Stretch(text.rectTransform, 2f);
            UITheme.StyleButton(button);
            button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>Shows the Day before (-1) or after (+1) the one shown; after the last past Day comes the queue as it is now.</summary>
        public void Page(int step) {
            if (history.Count == 0) return;
            int index = (shownPast ?? history.Count) + step;
            shownPast = index >= history.Count ? (int?)null : Mathf.Max(0, index);
            Refresh();
        }

        // The Day just ended: keep what was planned for it (the queue empties as it resolves)
        private void OnTurnEnded(int day) {
            history.Add((day, lastPlanned));
            lastPlanned = new List<QueueView.Item>();
            Refresh();
        }

        // Clicking anywhere else closes it, like a menu
        private void Update() {
            if (!IsOpen || !GameInput.ClickedThisFrame) return;
            if (!PointerOn(panel) && !PointerOn(Toggle)) SetOpen(false);
        }

        private static bool PointerOn(RectTransform rect) =>
            rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, GameInput.PointerPosition, CanvasUtility.GetEventCamera(rect));

        public void SetOpen(bool open) {
            panel.gameObject.SetActive(open);
            if (open) {
                shownPast = null; // It opens on the queue as it is now
                Refresh();
            }
            else Tooltip.Existing?.HideText();
        }

        /// <summary>Lists the queue again, as the screen's player sees it now.</summary>
        public void Refresh() {
            if (gameManager?.Player1 == null || list == null) return;
            var viewer = FogOfWar.ViewerOf(gameManager.Player1);
            var live = QueueView.Items(viewer, gameManager.ActionsQueue, gameManager.ModifierManager);
            var turns = gameManager.TurnManager;
            if (turns == null || !turns.IsResolving) lastPlanned = live; // While resolving, the queue empties: keep the plan
            int count = live.Count(i => i.Heading == null && i.Number > 0); // What's foretold isn't queued yet
            toggleLabel.text = count > 0 ? $"Queue ({count})" : "Queue";

            Items = shownPast.HasValue ? history[shownPast.Value].Items : live;
            Lines = Items.Select(i => i.Heading != null ? $"<b>{i.Heading}</b>" : i.Number > 0 ? $"{i.Number}. {i.Text}" : i.Text).ToList();
            heading.text = shownPast.HasValue ? $"Day {history[shownPast.Value].Day}, resolved"
                : turns != null ? $"Action Queue, Day {turns.TurnNumber}" : "Action Queue";
            earlier.interactable = history.Count > 0 && (shownPast ?? history.Count) > 0;
            later.interactable = shownPast.HasValue;

            foreach (Transform row in list) Destroy(row.gameObject);
            foreach (var item in Items) AddRow(item);
            empty.text = shownPast.HasValue ? "<i>Nothing was planned that Day.</i>" : "<i>Nothing planned yet.</i>";
            empty.gameObject.SetActive(Items.Count == 0);
        }

        private void AddRow(QueueView.Item item) {
            if (item.Heading != null) {
                var heading = UITheme.NewText("Layer " + item.Heading, list, item.Heading, 22f, UITheme.Gold, TextAlignmentOptions.BottomLeft);
                heading.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
                heading.raycastTarget = false;
                return;
            }

            var row = UITheme.NewImage(ItemName + item.Number, list, UITheme.PanelLight, raycastTarget: true);
            UITheme.Round(row, UITheme.PanelLight, 1.5f);
            var rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.padding = new RectOffset(22, 12, 8, 8);
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            // A stripe down its left edge in its side's color: green for yours, red for theirs
            var stripe = UITheme.NewImage("Side", row.transform, item.Ours ? OursColor : TheirsColor);
            stripe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var stripeRect = stripe.rectTransform;
            stripeRect.anchorMin = Vector2.zero;
            stripeRect.anchorMax = new Vector2(0f, 1f);
            stripeRect.pivot = new Vector2(0f, 0.5f);
            stripeRect.sizeDelta = new Vector2(7f, -10f);
            stripeRect.anchoredPosition = new Vector2(6f, 0f);

            string number = item.Number > 0 ? $"<b>{item.Number}.</b> " : "";
            var text = UITheme.NewText("Text", row.transform, $"{number}{EffectIcons.Tag(item.Icon)} {item.Text}", 22f, UITheme.Parchment,
                TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;

            var hover = row.gameObject.AddComponent<QueueItemHover>();
            hover.Details = item.Details;
            hover.Action = item.Action;
        }

        private void OnDestroy() {
            if (gameMediator != null) {
                gameMediator.ActionsQueueChanged -= Refresh;
                gameMediator.TurnEnded -= OnTurnEnded;
            }
            FogOfWar.Changed -= Refresh;
        }
    }

    /// <summary>A queue item: hovering it lights it up and tells more about the action (see <see cref="QueueView.Item.Details"/>),
    /// and the board puts its arrows away to show that action's alone (see <see cref="BattlefieldArrowManager.Focus"/>).</summary>
    public class QueueItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        public string Details;
        public IGameAction Action;
        private bool showing;

        public void OnPointerEnter(PointerEventData eventData) {
            if (CardController.Dragged != null) return;
            GetComponent<Image>().color = UITheme.Hex(0x34466E);
            Tooltip.Shared.ShowTooltip(Details);
            BattlefieldArrowManager.Focus(Action);
            showing = true;
        }

        public void OnPointerExit(PointerEventData eventData) => Stop();

        private void OnDisable() => Stop();

        private void Stop() {
            if (!showing) return;
            showing = false;
            var image = GetComponent<Image>();
            if (image != null) image.color = UITheme.PanelLight;
            Tooltip.Existing?.HideText();
            if (BattlefieldArrowManager.Focused == Action) BattlefieldArrowManager.Focus(null);
        }
    }
}
