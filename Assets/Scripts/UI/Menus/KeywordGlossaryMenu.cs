using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Zeitgeist.MenuWidgets;

namespace Zeitgeist {
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
        public const string SectionButton = "Section: ";
        public const string PreviousButtonName = "GlossaryPrevious";
        public const string NextButtonName = "GlossaryNext";
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
        private readonly Dictionary<string, GameObject> headings = new Dictionary<string, GameObject>(); // The section headers
        private readonly Dictionary<string, GameObject> groups = new Dictionary<string, GameObject>(); // A section's keywords, shown while it's open
        private readonly Dictionary<string, (TextMeshProUGUI label, int count)> sectionLabels = new Dictionary<string, (TextMeshProUGUI, int)>();
        private TextMeshProUGUI crumb; // The shown keyword's section, over its title
        private CardPeek cardPeek;
        private string hoveredName; // The card whose name the pointer is on, and since when
        private float hoveredSince;

        /// <summary>The keyword shown.</summary>
        public string Shown { get; private set; }
        /// <summary>The section whose keywords are listed; null when none is open (the search opens every section with a match).</summary>
        public string OpenSection { get; private set; }
        private bool Filtering => !string.IsNullOrEmpty(filter?.text?.Trim());
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
            if (crumb != null) crumb.text = entry.Group;
            if (!Filtering && OpenSection != entry.Group) Open(entry.Group, scrollToIt: false);
            KeepInView(key);
        }

        /// <summary>The keyword after (or before) the one shown, among those listed, round the end.</summary>
        public void ShowNext(int step = 1) {
            var listed = Listed;
            if (listed.Count == 0) return;
            int at = listed.IndexOf(Shown);
            ShowKeyword(listed[((at < 0 ? 0 : at + step) % listed.Count + listed.Count) % listed.Count]);
        }

        /// <summary>The keywords on the four factions' starter decks' cards (their rules texts' links and their abilities): what a first run teaches.</summary>
        public HashSet<string> StarterKeywords() {
            var keys = new HashSet<string>();
            if (Catalog == null) return keys;
            foreach (var umbrella in Umbrellas.All) {
                var starter = AdventureDecks.Starter(umbrella, Catalog.AllCards);
                foreach (var card in starter.Cards.Distinct()) {
                    foreach (var key in Keywords.LinksIn(CardTextFormatter.GetRulesText(card.ToCardData()))) keys.Add(key);
                    if (card is CreatureCardScriptableObject { ability: not Enums.CreatureAbility.None } creature && Keywords.Exists(Abilities.NameOf(creature.ability))) keys.Add(Abilities.NameOf(creature.ability));
                }
                Destroy(starter);
            }
            return keys;
        }

        /// <summary>Opens the section (closing the one open) and, unless told otherwise, scrolls its header to the top.</summary>
        public void Open(string section, bool scrollToIt = true) {
            if (!groups.ContainsKey(section)) return;
            OpenSection = section;
            ShowSections();
            if (scrollToIt) JumpTo(section);
        }

        // The header clicked: open it, or close it if it's the one open (while searching, the search decides what shows)
        private void Toggle(string section) {
            if (OpenSection == section && !Filtering) {
                OpenSection = null;
                ShowSections();
            }
            else Open(section);
        }

        // Which sections show their keywords: the open one; while searching, every one with a match (the others' headers hide too)
        private void ShowSections() {
            foreach (var pair in groups) {
                bool matches = buttons.Any(b => b.Value.gameObject.activeSelf && Keywords.Get(b.Key).Group == pair.Key);
                pair.Value.SetActive(Filtering ? matches : pair.Key == OpenSection);
                headings[pair.Key].SetActive(!Filtering || matches);
            }
            LabelSections();
        }

        // "+ Statuses 21" closed, "– Statuses 21" open (alpha has no closing tag in TMP: it's set back)
        private void LabelSections() {
            foreach (var pair in sectionLabels) {
                bool open = groups.TryGetValue(pair.Key, out var group) && group.activeSelf;
                pair.Value.label.text = $"{(open ? "\u2013" : "+")}  {pair.Key}  <size=65%><alpha=#99>{pair.Value.count}<alpha=#FF></size>";
            }
        }

        /// <summary>Lists only the keywords whose name has the text (any case), and their sections; empty lists them all.
        /// If the one shown is filtered out, the first one left is shown.</summary>
        public void FilterBy(string text) {
            text = text?.Trim() ?? "";
            foreach (var pair in buttons) {
                var entry = Keywords.Get(pair.Key);
                pair.Value.gameObject.SetActive(text.Length == 0 || entry.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            ShowSections();
            var listed = Listed;
            if (listed.Count > 0 && !listed.Contains(Shown)) ShowKeyword(listed[0]);
            else if (Shown != null) KeepInView(Shown);
        }

        // Scrolls the list just enough for the chosen keyword's button to be fully visible
        private void KeepInView(string key) {
            if (buttons.TryGetValue(key, out var button)) MenuWidgets.KeepInView(list, (RectTransform)button.transform);
        }

        /// <summary>Scrolls the list so the section's header sits at the top.</summary>
        public void JumpTo(string section) {
            if (!headings.TryGetValue(section, out var heading) || list == null) return;
            Canvas.ForceUpdateCanvases();
            var content = list.content;
            var headingRect = (RectTransform)heading.transform;
            float fromTop = -content.InverseTransformPoint(headingRect.TransformPoint(new Vector3(0f, headingRect.rect.yMax, 0f))).y;
            float scrollable = content.rect.height - list.viewport.rect.height;
            list.verticalNormalizedPosition = scrollable <= 0f ? 1f : Mathf.Clamp01(1f - fromTop / scrollable);
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
            // The keyboard and the wheel (the user, 2026-10-17): Up and Down walk the keywords listed (Left and Right did the sections,
            // "weird and very confusing", gone); the wheel over the list walks them too, and over the entry turns its pages; on the
            // list's right edge (its last tenth) the wheel scrolls the list itself. Typing in the search takes the keys
            if (filter == null || !filter.isFocused) {
                if (GameInput.KeyPressed(Key.DownArrow)) ShowNext(1);
                else if (GameInput.KeyPressed(Key.UpArrow)) ShowNext(-1);
            }
            float wheel = GameInput.WheelNotches;
            var pointer = GameInput.PointerPosition;
            bool overEntry = title != null && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)title.transform.parent, pointer, null);
            bool overList = list != null && RectTransformUtility.RectangleContainsScreenPoint(list.viewport, pointer, null) && !OnScrollEdge(pointer);
            if (wheel != 0f && (overEntry || overList)) ShowNext(wheel < 0f ? 1 : -1);
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
            var screen = AddScreen(transform, "KeywordGlossaryPanel", "Glossary");
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
            groups.Clear();
            sectionLabels.Clear();
            crumb = null;
            OpenSection = null;
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

            // Each section is a header that opens and closes its keywords (the user, 2026-10-17: a glossary you can get about
            // in): one open at a time, its count beside its name, its keywords under it in a tighter list
            // The keywords a new player meets first (the dossier, 2026-10-17): the ones on the four starter decks' cards, starred
            // and listed first in their sections
            var starters = StarterKeywords();
            foreach (var section in Keywords.Groups) {
                var entries = Keywords.All.Where(e => e.Group == section).OrderBy(e => starters.Contains(e.Key) ? 0 : 1).ToList();
                var header = AddButton(content.transform, SectionButton + section, section, -1, () => Toggle(section));
                header.GetComponent<LayoutElement>().preferredHeight = 44;
                var headerText = header.GetComponentInChildren<TextMeshProUGUI>();
                headerText.fontSize = 22;
                headerText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                headerText.alignment = TextAlignmentOptions.Left;
                headerText.color = AccentColor;
                headings[section] = header.gameObject;
                sectionLabels[section] = (headerText, entries.Count);
                var group = NewObject("Keywords", content.transform);
                var groupLayout = group.AddComponent<VerticalLayoutGroup>();
                groupLayout.spacing = 4;
                groupLayout.padding = new RectOffset(14, 0, 0, 8);
                SetControls(groupLayout, expandHeight: false);
                groups[section] = group;
                foreach (var entry in entries) {
                    string key = entry.Key;
                    var button = AddButton(group.transform, KeywordButton + key, starters.Contains(key) ? entry.Title + " <color=#B8B0A0>♦</color>" : entry.Title, -1, () => ShowKeyword(key));
                    button.GetComponent<LayoutElement>().preferredHeight = 40;
                    var text = button.GetComponentInChildren<TextMeshProUGUI>();
                    text.fontSize = 21;
                    text.alignment = TextAlignmentOptions.Left;
                    buttons[key] = button;
                    order.Add(key);
                }
                group.SetActive(false);
            }
            LabelSections();

            var scroll = area.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            WheelScroll.AddTo(scroll).ScrollsAt = OnScrollEdge; // Only the list's right edge scrolls it; elsewhere the wheel walks the keywords
            list = scroll;
        }

        /// <summary>The pointer is on the list's right edge, its last tenth: where the wheel scrolls the list rather than walk the keywords.</summary>
        public bool OnScrollEdge(Vector2 pointer) {
            if (list == null || list.viewport == null) return false;
            var viewport = list.viewport;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer, null, out var local)) return false;
            return (local.x - viewport.rect.xMin) / viewport.rect.width >= 0.9f;
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

            // Over the title: the keyword's section, and Previous and Next to walk the list as it's listed
            var nav = NewObject("Nav", area.transform);
            var navLayout = nav.AddComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 10;
            navLayout.childAlignment = TextAnchor.MiddleLeft;
            navLayout.childControlWidth = navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = navLayout.childForceExpandHeight = false;
            nav.AddComponent<LayoutElement>().preferredHeight = 36;
            crumb = AddText(nav.transform, "", 20, UITheme.Hex(0x5a5146), FontStyles.SmallCaps, height: 36);
            crumb.alignment = TextAlignmentOptions.Left;
            crumb.GetComponent<LayoutElement>().flexibleWidth = 1;
            foreach (var (name, label, step) in new[] { (PreviousButtonName, "Previous", -1), (NextButtonName, "Next", 1) }) {
                var arrow = AddButton(nav.transform, name, label, 130, () => ShowNext(step));
                arrow.GetComponent<LayoutElement>().preferredHeight = 34;
                arrow.GetComponentInChildren<TextMeshProUGUI>().fontSize = 19;
            }

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
