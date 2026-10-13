using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The pieces the menu screens are built from (match setup, card gallery, deck builder, keyword glossary), in the game's theme (see
    /// <see cref="UITheme"/>). Every screen has the same frame: a title, columns that take the space left, and a footer.
    /// Lists scroll inside their column, and a choice's text shrinks to fit its button rather than spill out of it.
    /// </summary>
    public static class MenuWidgets {
        // The same sizes on every menu screen
        public const float TitleSize = 64f, TitleHeight = 80f, ColumnSpacing = 30f, FooterHeight = 72f, BackWidth = 260f;
        public const float ChoiceHeight = 84f; // A choice button: a name and a line under it
        private static RectOffset ScreenPadding => new RectOffset(60, 60, 40, 40);

        /// <summary>A full-screen menu: its title, then a row of columns that takes the space left. Add a footer last.</summary>
        public static (GameObject panel, Transform columns) AddScreen(Transform root, string name, string title) {
            var panel = NewObject(name, root);
            Stretch(panel);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = ScreenPadding;
            layout.spacing = 20;
            SetControls(layout, expandHeight: false);
            UITheme.StyleHeading(AddText(panel.transform, title, TitleSize, AccentColor, FontStyles.Bold, height: TitleHeight), AccentColor);

            var columns = NewObject("Columns", panel.transform);
            var columnsLayout = columns.AddComponent<HorizontalLayoutGroup>();
            columnsLayout.spacing = ColumnSpacing;
            columnsLayout.childControlWidth = columnsLayout.childControlHeight = true;
            columnsLayout.childForceExpandWidth = false;
            columnsLayout.childForceExpandHeight = true;
            columns.AddComponent<LayoutElement>().flexibleHeight = 1;
            return (panel, columns.transform);
        }

        /// <summary>The row of buttons at the bottom of a screen, centered.</summary>
        public static Transform AddFooter(Transform panel) {
            var footer = NewObject("Footer", panel);
            var layout = footer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            footer.AddComponent<LayoutElement>().preferredHeight = FooterHeight;
            return footer.transform;
        }

        /// <summary>
        /// A list that takes the rest of its column and scrolls when it's longer. The wheel doesn't scroll it: it picks the
        /// next choice (see <see cref="WheelStepper"/>), and <see cref="KeepInView"/> follows. Returns where its items go.
        /// </summary>
        public static Transform AddList(Transform column, out ScrollRect scroll) {
            var area = NewObject("List", column);
            var element = area.AddComponent<LayoutElement>();
            element.flexibleHeight = 1;
            element.minHeight = 0;

            var viewport = NewObject("Viewport", area.transform);
            Stretch(viewport);
            viewport.AddComponent<RectMask2D>();
            viewport.AddComponent<Image>().color = Color.clear; // Catches the pointer between the buttons

            var content = NewObject("Content", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(2, 2, 2, 8); // Room for the buttons' shadows
            SetControls(layout, expandHeight: false);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = area.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 0f;
            AddScrollbar(scroll);
            return content.transform;
        }

        // A slim brass bar at the side, only while the list is longer than its column: there's more below
        private static void AddScrollbar(ScrollRect scroll) {
            var track = NewObject("Scrollbar", scroll.transform);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(1f, 0f);
            trackRect.anchorMax = Vector2.one;
            trackRect.pivot = new Vector2(1f, 0.5f);
            trackRect.sizeDelta = new Vector2(8f, 0f);
            UITheme.Round(track.AddComponent<Image>(), UITheme.Hex(0x0B1220), 0.5f);

            var area = NewObject("Sliding Area", track.transform);
            Stretch(area);
            var handle = NewObject("Handle", area.transform);
            Stretch(handle);
            var handleImage = UITheme.Round(handle.AddComponent<Image>(), UITheme.GoldDim, 0.5f);

            var bar = track.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = (RectTransform)handle.transform;
            bar.targetGraphic = handleImage;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = 6f;
        }

        /// <summary>Scrolls the list just enough for the item to be fully in view.</summary>
        public static void KeepInView(ScrollRect list, RectTransform item) {
            if (list == null || item == null) return;
            Canvas.ForceUpdateCanvases();
            var view = WorldRect(list.viewport);
            var shown = WorldRect(item);
            float margin = Mathf.Min(shown.height * 0.5f, 24f);
            float shift = shown.yMax > view.yMax ? view.yMax - shown.yMax - margin
                        : shown.yMin < view.yMin ? view.yMin - shown.yMin + margin : 0f;
            if (Mathf.Approximately(shift, 0f)) return;
            list.StopMovement();
            list.content.position += new Vector3(0f, shift, 0f);
            list.verticalNormalizedPosition = Mathf.Clamp01(list.verticalNormalizedPosition);
        }

        private static Rect WorldRect(RectTransform rect) {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>
        /// A choice in a list: its name on one line and a smaller line under it (rich text). Both shrink to fit the button
        /// rather than spill out of it; the line under the name wraps to two lines if it must.
        /// </summary>
        public static Button AddChoice(Transform parent, string name, string title, string subtitle, Action onClick) {
            var button = AddButton(parent, name, title, -1, onClick);
            button.GetComponent<LayoutElement>().preferredHeight = ChoiceHeight;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14;
            label.fontSizeMax = 28;
            label.overflowMode = TextOverflowModes.Truncate;
            if (string.IsNullOrEmpty(subtitle)) return button;

            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.offsetMin = new Vector2(14f, 0f);
            labelRect.offsetMax = new Vector2(-14f, -6f);
            label.alignment = TextAlignmentOptions.Bottom;

            var under = AddText(button.transform, subtitle, 19, UITheme.Hex(0xC9C0AE), FontStyles.Normal, height: -1);
            var underRect = under.rectTransform;
            underRect.anchorMin = Vector2.zero;
            underRect.anchorMax = new Vector2(1f, 0.5f);
            underRect.offsetMin = new Vector2(14f, 6f);
            underRect.offsetMax = new Vector2(-14f, -2f);
            under.alignment = TextAlignmentOptions.Top;
            under.textWrappingMode = TextWrappingModes.Normal;
            under.enableAutoSizing = true;
            under.fontSizeMin = 12;
            under.fontSizeMax = 19;
            under.overflowMode = TextOverflowModes.Truncate;
            return button;
        }
        /// <summary>A strip of the faction's color down the button's left edge.</summary>
        public static void AddFactionMark(Button button, Faction faction) {
            var mark = NewObject("FactionMark", button.transform);
            var rect = (RectTransform)mark.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(5f, 9f);
            rect.offsetMax = new Vector2(11f, -9f);
            UITheme.Round(mark.AddComponent<Image>(), UITheme.FactionColor(faction), 0.5f).raycastTarget = false;
        }

        public static Color PanelColor => UITheme.Panel;
        public static Color ButtonColor => UITheme.PanelLight;
        public static readonly Color SelectedColor = UITheme.Hex(0x6B5222);
        public static Color TextColor => UITheme.Parchment;
        public static Color AccentColor => UITheme.Gold;

        /// <summary>A navy panel with a title (none if null), stacking what's added to it from the top.</summary>
        public static Transform AddColumn(Transform parent, string title) {
            var column = NewObject(title ?? "Column", parent);
            UITheme.StylePanel(column.AddComponent<Image>(), PanelColor);
            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 20);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperCenter;
            SetControls(layout, expandHeight: false);
            if (title != null) UITheme.StyleHeading(AddText(column.transform, title, 36, TextColor, FontStyles.Bold, height: 52), TextColor);
            return column.transform;
        }

        /// <summary>A one-line text field: parchment text on a dark brass-trimmed well, and a faint hint while it's empty.</summary>
        public static TMP_InputField AddInputField(Transform parent, string name, string hint, float height) {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = height;
            var field = go.GetComponent<TMP_InputField>();
            var well = UITheme.Round(go.GetComponent<Image>(), UITheme.Hex(0x0B1220), 1.5f);
            UITheme.AddTrim(go.transform, UITheme.GoldDim, 1.5f);
            field.targetGraphic = well;
            var colors = field.colors;
            colors.highlightedColor = new Color(1.4f, 1.3f, 1.1f, 1f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            field.colors = colors;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = AccentColor;
            field.caretWidth = 2;
            field.selectionColor = UITheme.WithAlpha(AccentColor, 0.35f);
            ((RectTransform)field.textViewport).offsetMin = new Vector2(16f, 6f);
            ((RectTransform)field.textViewport).offsetMax = new Vector2(-16f, -6f);

            var text = (TextMeshProUGUI)field.textComponent;
            text.fontSize = height * 0.5f;
            text.color = TextColor;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var placeholder = (TextMeshProUGUI)field.placeholder;
            placeholder.text = hint;
            placeholder.fontSize = height * 0.5f;
            placeholder.color = UITheme.WithAlpha(TextColor, 0.35f);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            return field;
        }

        public static Button AddButton(Transform parent, string name, string label, float width, Action onClick) {
            var go = NewObject(name, parent);
            var image = UITheme.Round(go.AddComponent<Image>(), ButtonColor, 1.5f);
            UITheme.AddTrim(go.transform, UITheme.GoldDim, 1.5f);
            UITheme.AddHardShadow(image);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.3f, 1.1f, 1f); // Tints the button's own color, like the title screen buttons
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 72;

            var text = AddText(go.transform, label, 28, TextColor, FontStyles.Normal, height: -1);
            Stretch(text.gameObject, margin: 12);
            return button;
        }

        /// <summary>Shows whether a choice button is the chosen one: brass-trimmed and darker gold.</summary>
        public static void ShowChosen(Button button, bool chosen) {
            button.image.color = chosen ? SelectedColor : ButtonColor;
            var trim = button.transform.Find("Trim");
            if (trim != null) trim.GetComponent<Image>().color = chosen ? AccentColor : UITheme.GoldDim;
        }

        public static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color, FontStyles style, float height) {
            var go = NewObject("Text", parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            if (height > 0) go.AddComponent<LayoutElement>().preferredHeight = height;
            return tmp;
        }

        public static void SetControls(HorizontalOrVerticalLayoutGroup layout, bool expandHeight) {
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = expandHeight;
        }

        public static GameObject NewObject(string name, Transform parent) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void Stretch(GameObject go, float margin = 0) {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }
    }
}
