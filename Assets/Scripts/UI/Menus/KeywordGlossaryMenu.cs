using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static SlayTheClone.MenuWidgets;

namespace SlayTheClone {
    /// <summary>
    /// The keyword glossary, opened from the title screen: every keyword by section on the left; the chosen one's explanation
    /// on the right, whose keywords can be clicked to read theirs, and the cards that print it (worked out from the cards
    /// themselves each time, so it follows any change to them); hover a card's name to see the card. Each tick of the mouse
    /// wheel goes to the next keyword (down) or the previous one (up), and the list follows. It opens on the first keyword.
    /// The field above the list filters it: only the keywords whose name has what's typed stay listed (Enter shows the first).
    /// </summary>
    public class KeywordGlossaryMenu : MenuScreen {
        public const string KeywordButton = "Keyword: ";
        public const string BackButtonName = "GlossaryBackButton";
        public const string FilterName = "KeywordFilter";
        private const float FilterHeight = 52f;
        private const string CardLink = "card:";
        private const float ListWidth = CardGalleryMenu.SideColumnWidth; // The same as the gallery's faction list

        private TextMeshProUGUI title;
        private TextMeshProUGUI body;
        private TextMeshProUGUI usedBy;
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private readonly List<string> order = new List<string>(); // The keywords as listed, top to bottom
        private ScrollRect list;
        private TMP_InputField filter;
        private readonly Dictionary<string, GameObject> headings = new Dictionary<string, GameObject>();
        private CardPeek cardPeek;
        private string hoveredName; // The card whose name the pointer is on, and since when
        private float hoveredSince;

        /// <summary>The keyword shown.</summary>
        public string Shown { get; private set; }
        /// <summary>The explanation shown: its keywords are links that can be clicked.</summary>
        public TMP_Text Body => body;
        /// <summary>The cards that print the keyword, as links that show the card when hovered.</summary>
        public TMP_Text UsedBy => usedBy;
        /// <summary>The field that filters the list by name.</summary>
        public TMP_InputField Filter => filter;
        /// <summary>The keywords listed now, top to bottom: all of them, or those the filter leaves.</summary>
        public List<string> Listed => order.Where(k => buttons[k].gameObject.activeSelf).ToList();
        /// <summary>The card shown while its name is hovered, if any.</summary>
        public CardController CardPreview => cardPeek?.Shown;

        /// <summary>Shows a keyword's explanation, and which cards print it.</summary>
        public void ShowKeyword(string key) {
            var entry = Keywords.Get(key);
            if (entry == null) return;
            Shown = key;
            title.text = entry.Title;
            body.text = Keywords.Describe(key);
            var cards = CardsWith(key);
            usedBy.text = cards.Count > 0
                ? $"<b>On cards:</b> {string.Join(", ", cards.Select(c => $"<link=\"{CardLink}{c.cardName}\"><u>{c.cardName}</u></link>"))}"
                : "";
            ShowCardPreview(null);
            foreach (var pair in buttons) ShowChosen(pair.Value, pair.Key == key);
            KeepInView(key);
        }

        /// <summary>Lists only the keywords whose name has the text (any case), and their sections; empty lists them all.
        /// If the one shown is filtered out, the first one left is shown.</summary>
        public void FilterBy(string text) {
            text = text?.Trim() ?? "";
            foreach (var pair in buttons) {
                var entry = Keywords.Get(pair.Key);
                pair.Value.gameObject.SetActive(text.Length == 0 || entry.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            foreach (var pair in headings) {
                pair.Value.SetActive(buttons.Any(b => b.Value.gameObject.activeSelf && Keywords.Get(b.Key).Group == pair.Key));
            }
            var listed = Listed;
            if (listed.Count > 0 && !listed.Contains(Shown)) ShowKeyword(listed[0]);
            else if (Shown != null) KeepInView(Shown);
        }

        // Scrolls the list just enough for the chosen keyword's button to be fully visible
        private void KeepInView(string key) {
            if (buttons.TryGetValue(key, out var button)) MenuWidgets.KeepInView(list, (RectTransform)button.transform);
        }

        /// <summary>The cards on offer whose printed text has the keyword, read from the cards as they are now.</summary>
        public List<CardDataScriptableObject> CardsWith(string key) {
            var found = new List<CardDataScriptableObject>();
            foreach (var asset in Catalog != null ? Catalog.AllCards : new List<CardDataScriptableObject>()) {
                var data = asset.ToCardData();
                if (Keywords.LinksIn(CardTextFormatter.GetRulesText(data)).Contains(key)) found.Add(asset);
                Destroy(data);
            }
            return found;
        }

        protected override void Update() {
            base.Update();
            if (!IsOpen || usedBy == null) return;
            int link = TMP_TextUtilities.FindIntersectingLink(usedBy, GameInput.PointerPosition, null);
            string id = link >= 0 ? usedBy.textInfo.linkInfo[link].GetLinkID() : null;
            string name = id != null && id.StartsWith(CardLink) ? id.Substring(CardLink.Length) : null;
            // Shown once the pointer rests on the name, like a hovered card; the wheel hides it until it's still again
            if (Tooltip.WheelMoved) {
                hoveredSince = Time.unscaledTime;
                ShowCardPreview(null);
            }
            if (name != hoveredName) {
                hoveredName = name;
                hoveredSince = Time.unscaledTime;
                if (name == null) ShowCardPreview(null);
            }
            float delay = CardPreview != null ? Tooltip.SwitchDelay : Tooltip.HoverDelay;
            if (name != null && Time.unscaledTime - hoveredSince >= delay) ShowCardPreview(name, GameInput.PointerPosition);
        }

        /// <summary>Shows the named card enlarged next to the pointer; null hides it.</summary>
        public void ShowCardPreview(string cardName, Vector2 near = default) {
            if (cardPeek == null) return;
            cardPeek.Show(cardName != null ? Catalog.AllCards.FirstOrDefault(c => c.cardName == cardName) : null, near);
        }

        protected override GameObject Build() {
            var screen = AddScreen(transform, "KeywordGlossaryPanel", "Keywords");
            var columns = screen.columns;
            BuildList(columns);
            BuildEntry(columns);
            AddButton(AddFooter(screen.panel.transform), BackButtonName, "Back", BackWidth, Hide);
            cardPeek = new CardPeek(screen.panel.transform, Catalog.CardPrefab);
            return screen.panel;
        }

        protected override void Opened() => ShowKeyword(Keywords.All.First().Key);

        protected override void Forget() {
            cardPeek?.Hide();
            cardPeek = null;
            hoveredName = null;
            title = body = usedBy = null;
            buttons.Clear();
            headings.Clear();
            order.Clear();
            list = null;
            filter = null;
            Shown = null;
        }

        // Every keyword, section by section, in a scrolling column
        private void BuildList(Transform parent) {
            var area = NewObject("List", parent);
            UITheme.StylePanel(area.AddComponent<Image>(), PanelColor);
            // A fixed width: picking a keyword never resizes the list
            var element = area.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = ListWidth;
            element.flexibleWidth = 0;

            // The filter, at the top
            filter = AddInputField(area.transform, FilterName, "Find a keyword...", FilterHeight);
            var filterRect = (RectTransform)filter.transform;
            filterRect.anchorMin = new Vector2(0f, 1f);
            filterRect.anchorMax = Vector2.one;
            filterRect.pivot = new Vector2(0.5f, 1f);
            filterRect.offsetMin = new Vector2(14f, -14f - FilterHeight);
            filterRect.offsetMax = new Vector2(-14f, -14f);
            filter.onValueChanged.AddListener(FilterBy);
            filter.onSubmit.AddListener(_ => { if (Listed.Count > 0) ShowKeyword(Listed[0]); });

            var viewport = NewObject("Viewport", area.transform);
            Stretch(viewport, 14);
            ((RectTransform)viewport.transform).offsetMax = new Vector2(-14f, -14f - FilterHeight - 10f);
            viewport.AddComponent<RectMask2D>();
            viewport.AddComponent<Image>().color = Color.clear;

            var content = NewObject("Content", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var listLayout = content.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 6;
            listLayout.padding = new RectOffset(6, 6, 6, 6);
            SetControls(listLayout, expandHeight: false);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var section in Keywords.Groups) {
                var heading = AddText(content.transform, section, 30, AccentColor, FontStyles.Bold | FontStyles.SmallCaps, height: 44);
                heading.alignment = TextAlignmentOptions.BottomLeft;
                headings[section] = heading.gameObject;
                foreach (var entry in Keywords.All.Where(e => e.Group == section)) {
                    string key = entry.Key;
                    var button = AddButton(content.transform, KeywordButton + key, entry.Title, -1, () => ShowKeyword(key));
                    button.GetComponent<LayoutElement>().preferredHeight = 46;
                    button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;
                    buttons[key] = button;
                    order.Add(key);
                }
            }

            var scroll = area.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            WheelScroll.AddTo(scroll);
            list = scroll;
        }

        // The chosen keyword: its name, its explanation (links click through), and the cards that print it
        private void BuildEntry(Transform parent) {
            var area = NewObject("Entry", parent);
            var image = area.AddComponent<Image>();
            UITheme.Round(image, UITheme.Parchment, 1.5f);
            UITheme.AddTrim(area.transform, UITheme.Gold, 1.5f);
            var element = area.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = 0; // Takes what's left, whatever the text
            element.flexibleWidth = 1;
            var layout = area.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 30, 30);
            layout.spacing = 18;
            SetControls(layout, expandHeight: false);

            title = AddText(area.transform, "", 52, UITheme.Ink, FontStyles.Bold | FontStyles.SmallCaps, height: 70);
            title.alignment = TextAlignmentOptions.Left;
            body = AddText(area.transform, "", 34, UITheme.Ink, FontStyles.Normal, height: -1);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.raycastTarget = true; // Its keywords are clicked
            body.gameObject.AddComponent<KeywordLinkClicks>().OnKeyword = ShowKeyword;
            usedBy = AddText(area.transform, "", 26, UITheme.Hex(0x5a5146), FontStyles.Italic, height: -1);
            usedBy.alignment = TextAlignmentOptions.TopLeft;
            usedBy.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    /// <summary>Clicking a keyword link in a text calls back with its keyword.</summary>
    public class KeywordLinkClicks : MonoBehaviour, IPointerClickHandler {
        public Action<string> OnKeyword;

        public void OnPointerClick(PointerEventData eventData) {
            var text = GetComponent<TMP_Text>();
            int link = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
            if (link >= 0) OnKeyword?.Invoke(text.textInfo.linkInfo[link].GetLinkID());
        }
    }
}
