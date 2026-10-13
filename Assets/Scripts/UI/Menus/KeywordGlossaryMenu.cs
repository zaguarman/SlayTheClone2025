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
    /// on the right, whose keywords can be clicked to read theirs, and the cards that print it.
    /// </summary>
    public class KeywordGlossaryMenu : MonoBehaviour {
        public const string KeywordButton = "Keyword: ";
        public const string BackButtonName = "GlossaryBackButton";

        private DeckCatalog catalog;
        private GameObject titleScreen;
        private GameObject panel;
        private TextMeshProUGUI title;
        private TextMeshProUGUI body;
        private TextMeshProUGUI usedBy;
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private Dictionary<string, List<string>> cardsByKeyword;

        public bool IsOpen => panel != null && panel.activeSelf;
        /// <summary>The keyword shown.</summary>
        public string Shown { get; private set; }
        /// <summary>The explanation shown: its keywords are links that can be clicked.</summary>
        public TMP_Text Body => body;

        public void Setup(DeckCatalog deckCatalog, GameObject title) {
            catalog = deckCatalog;
            titleScreen = title;
        }

        public void Show() {
            if (panel == null) Build();
            if (titleScreen != null) titleScreen.SetActive(false);
            panel.SetActive(true);
            ShowKeyword(Shown ?? Keywords.All.First().Key);
        }

        public void Hide() {
            if (panel != null) panel.SetActive(false);
            if (titleScreen != null) titleScreen.SetActive(true);
        }

        /// <summary>Shows a keyword's explanation, and which cards print it.</summary>
        public void ShowKeyword(string key) {
            var entry = Keywords.Get(key);
            if (entry == null) return;
            Shown = key;
            title.text = entry.Title;
            body.text = Keywords.Describe(key);
            var cards = CardsWith(key);
            usedBy.text = cards.Count > 0 ? $"<b>On cards:</b> {string.Join(", ", cards)}" : "";
            foreach (var pair in buttons) ShowChosen(pair.Value, pair.Key == key);
        }

        // The cards on offer whose text prints the keyword
        private List<string> CardsWith(string key) {
            if (cardsByKeyword == null) {
                cardsByKeyword = new Dictionary<string, List<string>>();
                foreach (var asset in catalog != null ? catalog.AllCards : new List<CardDataScriptableObject>()) {
                    var data = asset.ToCardData();
                    foreach (var link in Keywords.LinksIn(CardTextFormatter.GetRulesText(data)).Distinct()) {
                        if (!cardsByKeyword.TryGetValue(link, out var list)) cardsByKeyword[link] = list = new List<string>();
                        list.Add(asset.cardName);
                    }
                    Destroy(data);
                }
            }
            return cardsByKeyword.TryGetValue(key, out var names) ? names : new List<string>();
        }

        private void Build() {
            panel = NewObject("KeywordGlossaryPanel", transform);
            Stretch(panel);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 40, 40);
            layout.spacing = 20;
            SetControls(layout, expandHeight: false);
            UITheme.StyleHeading(AddText(panel.transform, "Keywords", 64, AccentColor, FontStyles.Bold, height: 80), AccentColor);

            var columns = NewObject("Columns", panel.transform);
            var columnsLayout = columns.AddComponent<HorizontalLayoutGroup>();
            columnsLayout.spacing = 30;
            columnsLayout.childControlWidth = columnsLayout.childControlHeight = true;
            columnsLayout.childForceExpandWidth = false;
            columnsLayout.childForceExpandHeight = true;
            columns.AddComponent<LayoutElement>().flexibleHeight = 1;

            BuildList(columns.transform);
            BuildEntry(columns.transform);

            var footer = NewObject("Footer", panel.transform);
            var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = false;
            footer.AddComponent<LayoutElement>().preferredHeight = 72;
            AddButton(footer.transform, BackButtonName, "Back", 260, Hide);
        }

        // Every keyword, section by section, in a scrolling column
        private void BuildList(Transform parent) {
            var area = NewObject("List", parent);
            UITheme.StylePanel(area.AddComponent<Image>(), PanelColor);
            var element = area.AddComponent<LayoutElement>();
            element.preferredWidth = 470;
            element.flexibleWidth = 0;

            var viewport = NewObject("Viewport", area.transform);
            Stretch(viewport, 14);
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
                foreach (var entry in Keywords.All.Where(e => e.Group == section)) {
                    string key = entry.Key;
                    var button = AddButton(content.transform, KeywordButton + key, entry.Title, -1, () => ShowKeyword(key));
                    button.GetComponent<LayoutElement>().preferredHeight = 46;
                    button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;
                    buttons[key] = button;
                }
            }

            var scroll = area.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
        }

        // The chosen keyword: its name, its explanation (links click through), and the cards that print it
        private void BuildEntry(Transform parent) {
            var area = NewObject("Entry", parent);
            var image = area.AddComponent<Image>();
            UITheme.Round(image, UITheme.Parchment, 1.5f);
            UITheme.AddTrim(area.transform, UITheme.Gold, 1.5f);
            area.AddComponent<LayoutElement>().flexibleWidth = 1;
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
            usedBy = AddText(area.transform, "", 24, UITheme.Hex(0x5a5146), FontStyles.Italic, height: -1);
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
