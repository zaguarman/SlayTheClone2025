using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>
    /// The Queue button and the panel it opens: the actions waiting to resolve, in order, as the screen's player sees them
    /// (see <see cref="QueueView"/>, Fog of War included). It follows the queue as it changes, and the fog switch (F9).
    /// <see cref="BoardSkin"/> places both at the right edge of the table.
    /// </summary>
    public class QueuePanel : MonoBehaviour {
        public const string ToggleName = "QueueButton";
        public const string PanelName = "QueuePanel";
        private const int MaxLines = 22;

        private IGameManager gameManager;
        private IGameMediator gameMediator;
        private Button toggle;
        private TextMeshProUGUI toggleLabel;
        private RectTransform panel;
        private TextMeshProUGUI list;

        public RectTransform Toggle => (RectTransform)toggle.transform;
        public RectTransform Panel => panel;
        public bool IsOpen => panel != null && panel.gameObject.activeSelf;
        /// <summary>What the panel lists, one line per action and layer heading.</summary>
        public List<string> Lines { get; private set; } = new List<string>();

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

            var panelImage = UITheme.NewImage(PanelName, transform, UITheme.Panel);
            panel = panelImage.rectTransform;
            panel.localScale = scale;
            panel.sizeDelta = new Vector2(640f, 600f);
            UITheme.StylePanel(panelImage);

            var heading = UITheme.NewText("Heading", panel, "Action Queue", 30f, UITheme.Gold, TextAlignmentOptions.Top);
            UITheme.StyleHeading(heading);
            var headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0f, 1f);
            headingRect.anchorMax = new Vector2(1f, 1f);
            headingRect.pivot = new Vector2(0.5f, 1f);
            headingRect.sizeDelta = new Vector2(0f, 44f);
            headingRect.anchoredPosition = new Vector2(0f, -12f);

            list = UITheme.NewText("List", panel, "", 24f, UITheme.Parchment, TextAlignmentOptions.TopLeft);
            list.textWrappingMode = TextWrappingModes.Normal;
            list.overflowMode = TextOverflowModes.Truncate;
            UITheme.Stretch(list.rectTransform, 22f);
            list.rectTransform.offsetMax = new Vector2(-22f, -62f);
            panel.gameObject.SetActive(false);
        }

        public void SetOpen(bool open) {
            panel.gameObject.SetActive(open);
            if (open) Refresh();
        }

        /// <summary>Lists the queue again, as the screen's player sees it now.</summary>
        public void Refresh() {
            if (gameManager?.Player1 == null || list == null) return;
            var viewer = FogOfWar.ViewerOf(gameManager.Player1);
            Lines = QueueView.Lines(viewer, gameManager.ActionsQueue, gameManager.ModifierManager);
            int count = Lines.Count(line => !line.StartsWith("<b>"));
            toggleLabel.text = count > 0 ? $"Queue ({count})" : "Queue";

            var shown = Lines.Take(MaxLines).ToList();
            if (Lines.Count > MaxLines) shown.Add($"<i>...and {Lines.Count - MaxLines} more</i>");
            list.text = Lines.Count > 0 ? string.Join("\n", shown) : "<i>Nothing planned yet.</i>";
        }

        private void OnDestroy() {
            if (gameMediator != null) gameMediator.ActionsQueueChanged -= Refresh;
            FogOfWar.Changed -= Refresh;
        }
    }
}
