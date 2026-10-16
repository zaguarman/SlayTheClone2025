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
    /// One list runs through every Day (the user): each under its title ("Day 2"), with what watched over it (Watchdogs,
    /// traps), oldest first, as the screen knows it now (what played out, or a Reveal showed, is no longer ???). It opens on
    /// the Day being planned; the mouse wheel goes back and on one action at a time, so the pointer can rest on an action
    /// and step through what happened.
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
        private TextMeshProUGUI heading;
        private ScrollRect scroll;
        private bool scrollToToday; // On opening, and when a Day ends: the Day being planned at the top

        // Past Days: what the queue held when each was ended (and what was foretold then), described again whenever shown, so
        // it says what's been seen since: what played out, and what a Reveal or a Watchdog showed
        private class PastDay {
            public int Day;
            public List<(IGameAction Action, ResolutionLayer Layer)> Planned;
            public List<QueueView.Item> Foretold;
            public List<Watchdog> Watchdogs = new List<Watchdog>();                          // On watch that Day
            public List<(BoardSlot, SlotEffect)> Traps = new List<(BoardSlot, SlotEffect)>(); // In the slots that Day
            // As it played out (the user): when each action ran, and the attacks nobody planned (a Last Rites strike, an effect's)
            public Dictionary<IGameAction, int> Ran = new Dictionary<IGameAction, int>();
            public List<IGameAction> Unplanned = new List<IGameAction>();
        }
        private int ranCount;
        private readonly List<PastDay> history = new List<PastDay>();
        private PastDay lastPlanned = new PastDay { Planned = new List<(IGameAction, ResolutionLayer)>(), Foretold = new List<QueueView.Item>() };
        private readonly HashSet<IGameAction> played = new HashSet<IGameAction>(); // Actions that ran: everyone saw them
        private readonly HashSet<(IPlayer, IGameAction)> revealed = new HashSet<(IPlayer, IGameAction)>(); // Shown to a player by a Reveal or a Watchdog

        public RectTransform Toggle => (RectTransform)toggle.transform;
        public RectTransform Panel => panel;
        public bool IsOpen => panel != null && panel.gameObject.activeSelf;
        /// <summary>What the panel lists, one line per action and layer heading.</summary>
        public List<string> Lines { get; private set; } = new List<string>();
        /// <summary>The rows shown, headings included, top to bottom.</summary>
        public List<QueueView.Item> Items { get; private set; } = new List<QueueView.Item>();
        /// <summary>The past Days kept, oldest first.</summary>
        public IReadOnlyList<int> PastDays => history.Select(h => h.Day).ToList();

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
            mediator.BattlefieldStateChanged += view.OnBattlefieldStateChanged; // A creature showing what it is
            mediator.TurnEnded += view.OnTurnEnded;
            manager.ActionsQueue?.OnActionExecuting.AddListener(view.OnActionExecuting);
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

            scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 0f; // The wheel steps an action at a time (Update)
            panel.gameObject.SetActive(false);
        }

        // The tops of the rows in the list, down from its top, for the action rows (or the Days' titles)
        private List<float> RowTops(bool days) {
            var content = scroll.content;
            var corners = new Vector3[4];
            var tops = new List<float>();
            foreach (RectTransform row in content) {
                bool isDay = row.name.StartsWith(DayName);
                if (days ? !isDay : row.GetComponent<QueueItemHover>() == null) continue;
                row.GetWorldCorners(corners);
                tops.Add(-content.InverseTransformPoint(corners[1]).y);
            }
            tops.Sort();
            return tops;
        }

        private float MaxOffset => Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);

        private void ScrollTo(float offset) {
            scroll.StopMovement();
            var content = scroll.content;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(offset, 0f, MaxOffset));
        }

        /// <summary>
        /// One action on (a notch down, positive) or back (negative): the list moves so the next action's top is at the top of
        /// the panel, so the pointer resting on an action rests on the next one.
        /// </summary>
        public void Step(int actions) {
            if (actions == 0 || scroll == null) return;
            float now = scroll.content.anchoredPosition.y;
            var tops = RowTops(days: false);
            for (int i = 0; i < Mathf.Abs(actions); i++) {
                now = actions > 0
                    ? tops.Where(t => t > now + 0.5f).DefaultIfEmpty(MaxOffset).First()
                    : tops.Where(t => t < now - 0.5f).DefaultIfEmpty(0f).Last();
                now = Mathf.Clamp(now, 0f, MaxOffset);
            }
            ScrollTo(now);
        }

        // The Day just ended: keep what was planned for it (the queue empties as it resolves)
        private void OnTurnEnded(int day) {
            lastPlanned.Day = day;
            history.Add(lastPlanned);
            lastPlanned = new PastDay { Planned = new List<(IGameAction, ResolutionLayer)>(), Foretold = new List<QueueView.Item>() };
            scrollToToday = true;
            Refresh();
        }

        // Running now, on the Day being played out (its Dusk included: the Day ends after it)
        private void OnActionExecuting(IGameAction action) {
            played.Add(action);
            if (!lastPlanned.Ran.ContainsKey(action)) lastPlanned.Ran[action] = ranCount++;
            if (action is BattlefieldCombatAction && !lastPlanned.Unplanned.Contains(action) && !lastPlanned.Planned.Any(p => p.Action == action)) {
                lastPlanned.Unplanned.Add(action);
            }
        }

        private void OnBattlefieldStateChanged(IPlayer player) => Refresh();

        // What a Reveal or a Watchdog has shown either player, kept: the queue forgets it once the Day has played out
        private void RememberReveals(IActionsQueue queue, IEnumerable<IGameAction> actions) {
            if (queue == null) return;
            foreach (var action in actions) {
                foreach (var player in new[] { gameManager.Player1, gameManager.Player2 }) {
                    if (player != null && queue.IsRevealedTo(player, action)) revealed.Add((player, action));
                }
            }
        }

        // Clicking anywhere else closes it, like a menu; the wheel over it steps an action at a time
        private void Update() {
            if (!IsOpen) return;
            float notches = GameInput.WheelNotches;
            if (!Mathf.Approximately(notches, 0f) && PointerOn(scroll.viewport)) Step(notches < 0f ? 1 : -1); // Down: on
            if (GameInput.ClickedThisFrame && !PointerOn(panel) && !PointerOn(Toggle)) SetOpen(false);
        }

        private static bool PointerOn(RectTransform rect) =>
            rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, GameInput.PointerPosition, CanvasUtility.GetEventCamera(rect));

        public void SetOpen(bool open) {
            panel.gameObject.SetActive(open);
            if (open) {
                scrollToToday = true; // It opens on the Day being planned
                Refresh();
            }
            else Tooltip.Existing?.HideText();
        }

        /// <summary>Lists the queue again, as the screen's player sees it now.</summary>
        public void Refresh() {
            if (gameManager?.Player1 == null || list == null) return;
            var viewer = FogOfWar.ViewerOf(gameManager.Player1);
            var queue = gameManager.ActionsQueue;
            var live = QueueView.Items(viewer, queue, gameManager.ModifierManager);
            var turns = gameManager.TurnManager;
            bool resolving = turns != null && turns.IsResolving;
            if (queue != null && !resolving) { // While resolving, the queue empties: keep the plan, and what watched over it
                lastPlanned.Planned = queue.GetPendingActions().Select(a => (a, queue.LayerOfPending(a))).ToList();
                lastPlanned.Foretold = QueueView.Foretold(viewer);
                lastPlanned.Watchdogs = queue.Watchdogs.ToList();
                lastPlanned.Traps = new[] { gameManager.Player1, gameManager.Player2 }.Where(p => p != null).SelectMany(p => p.Battlefield)
                    .Where(s => s.Effect != null && s.Effect.IsHidden).Select(s => (s, s.Effect)).ToList();
            }
            RememberReveals(queue, lastPlanned.Planned.Select(p => p.Action));
            int count = live.Count(i => i.Heading == null && i.Number > 0); // What's foretold isn't queued yet
            toggleLabel.text = count > 0 ? $"Queue ({count})" : "Queue";
            if (!IsOpen) return; // The list is built when it's seen: every Day of a long game, on every change, costs

            // Every Day, oldest first, each under its title; the Day being planned last (playing out: its plan, as seen so far)
            var items = new List<QueueView.Item>();
            foreach (var day in history) AddDay(items, day.Day, Past(day, viewer), day, viewer, "<i>Nothing was planned that Day.</i>");
            AddDay(items, turns?.TurnNumber ?? 0, resolving ? Past(lastPlanned, viewer) : live, lastPlanned, viewer, "<i>Nothing planned yet.</i>");
            Items = items;
            Lines = Items.Select(i => i.Heading != null ? $"<b>{i.Heading}</b>" : i.Number > 0 ? $"{i.Number}. {i.Text}" : i.Text).ToList();
            heading.text = "Action Queue";

            float offset = scroll.content.anchoredPosition.y;
            foreach (var row in list.Cast<Transform>().ToList()) {
                row.SetParent(null, false); // Out of the layout now, gone at the end of the frame
                Destroy(row.gameObject);
            }
            foreach (var item in Items) AddRow(item);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            if (scrollToToday && IsOpen) {
                scrollToToday = false;
                ScrollTo(RowTops(days: true).LastOrDefault());
            }
            else ScrollTo(offset); // Where it was, as the list grows
        }

        // A Day in the list: its title, what watched over it, then its actions (or a note that there were none)
        private static void AddDay(List<QueueView.Item> items, int day, List<QueueView.Item> actions, PastDay watched, IPlayer viewer, string none) {
            items.Add(new QueueView.Item { Heading = $"Day {day}", DaySeparator = true });
            items.AddRange(QueueView.OnWatch(viewer, watched.Watchdogs, watched.Traps));
            if (actions.Count == 0) items.Add(new QueueView.Item { Text = none, Note = true });
            items.AddRange(actions);
        }

        // A past Day as the screen knows it now, in the order it played out: ties as they fell, the attacks it brought among
        // them; what never ran (cancelled, stopped) stays where it was planned
        private List<QueueView.Item> Past(PastDay day, IPlayer viewer) {
            var items = QueueView.Actions(viewer, gameManager.ActionsQueue, gameManager.ModifierManager, AsItRan(day),
                played.Contains, (player, action) => revealed.Contains((player, action)));
            if (day.Foretold.Count > 0) {
                items.Add(new QueueView.Item { Heading = "Foretold" });
                items.AddRange(day.Foretold.Where(i => i.Heading == null));
            }
            return items;
        }

        private static List<(IGameAction Action, ResolutionLayer Layer)> AsItRan(PastDay day) {
            var keyed = new List<(float Key, (IGameAction, ResolutionLayer) Entry)>();
            float last = -1f;
            foreach (var entry in day.Planned) {
                last = day.Ran.TryGetValue(entry.Action, out int ran) ? ran : last + 0.001f; // Never ran: right after the one before it
                keyed.Add((last, entry));
            }
            foreach (var attack in day.Unplanned) {
                if (day.Ran.TryGetValue(attack, out int ran)) keyed.Add((ran, (attack, ActionsQueue.LayerOf(attack))));
            }
            return keyed.OrderBy(k => k.Key).Select(k => k.Entry).ToList(); // Stable: ties keep the plan's order
        }

        private const string DayName = "Day ";

        private void AddRow(QueueView.Item item) {
            if (item.DaySeparator) { // A Day's title: larger, a gold rule under it
                var day = UITheme.NewText(DayName + item.Heading, list, item.Heading, 28f, UITheme.Gold, TextAlignmentOptions.BottomLeft);
                day.fontStyle = FontStyles.Bold | FontStyles.SmallCaps; // Not StyleHeading: its shadow needs a material the closed panel's text hasn't got yet
                day.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
                day.raycastTarget = false;
                var rule = UITheme.NewImage("Rule", day.transform, UITheme.WithAlpha(UITheme.Gold, 0.6f));
                rule.rectTransform.anchorMin = Vector2.zero;
                rule.rectTransform.anchorMax = new Vector2(1f, 0f);
                rule.rectTransform.pivot = new Vector2(0.5f, 1f);
                rule.rectTransform.sizeDelta = new Vector2(0f, 2f);
                return;
            }
            if (item.Note) {
                var note = UITheme.NewText("Note", list, item.Text, 22f, UITheme.Parchment, TextAlignmentOptions.Left);
                note.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
                note.raycastTarget = false;
                return;
            }
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

            // The rows' hit areas meet across the gap between them: the pointer going from one to the next never lands between
            row.raycastPadding = new Vector4(0f, -list.GetComponent<VerticalLayoutGroup>().spacing / 2f, 0f, -list.GetComponent<VerticalLayoutGroup>().spacing / 2f);
            var hover = row.gameObject.AddComponent<QueueItemHover>();
            hover.Details = item.Details;
            hover.Action = item.Action;
        }

        private void OnDestroy() {
            if (gameMediator != null) {
                gameMediator.ActionsQueueChanged -= Refresh;
                gameMediator.BattlefieldStateChanged -= OnBattlefieldStateChanged;
                gameMediator.TurnEnded -= OnTurnEnded;
            }
            gameManager?.ActionsQueue?.OnActionExecuting.RemoveListener(OnActionExecuting);
            FogOfWar.Changed -= Refresh;
        }
    }

    /// <summary>A queue item: hovering it lights it up and tells more about the action (see <see cref="QueueView.Item.Details"/>),
    /// and the board puts its arrows away to show that action's alone (see <see cref="BattlefieldArrowManager.Focus"/>).</summary>
    public class QueueItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        public string Details;
        public IGameAction Action;
        private bool showing;
        private static QueueItemHover hovered; // The row under the pointer, if any
        private const float ReleaseGrace = 0.15f; // Leaving a row: the arrows wait this long for the next row before all come back

        public void OnPointerEnter(PointerEventData eventData) {
            if (CardController.Dragged != null) return;
            GetComponent<Image>().color = UITheme.Hex(0x34466E);
            Tooltip.Shared.ShowTooltip(Details);
            BattlefieldArrowManager.Focus(Action);
            hovered = this;
            showing = true;
        }

        // Off the row: the board's arrows come back unless the pointer is on another row by then (no flicker going down the list)
        public void OnPointerExit(PointerEventData eventData) {
            if (!Stop()) return;
            var panel = GetComponentInParent<QueuePanel>();
            if (panel != null && panel.isActiveAndEnabled) panel.StartCoroutine(ReleaseSoon(Action));
            else Release(Action);
        }

        // The panel closing or the list being redrawn: the arrows come back now
        private void OnDisable() {
            // Its release waiting (the pointer just left) dies with the panel: let go now, or the board keeps its arrows alone
            if (Stop() || BattlefieldArrowManager.Focused == Action) Release(Action);
        }

        private bool Stop() {
            if (!showing) return false;
            showing = false;
            if (hovered == this) hovered = null;
            var image = GetComponent<Image>();
            if (image != null) image.color = UITheme.PanelLight;
            Tooltip.Existing?.HideText();
            return true;
        }

        private static System.Collections.IEnumerator ReleaseSoon(IGameAction action) {
            yield return new WaitForSecondsRealtime(ReleaseGrace);
            if (hovered == null) Release(action);
        }

        private static void Release(IGameAction action) {
            if (BattlefieldArrowManager.Focused == action) BattlefieldArrowManager.Focus(null);
        }
    }
}
