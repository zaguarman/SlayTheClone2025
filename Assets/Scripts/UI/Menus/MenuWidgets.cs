using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>The pieces the menu screens are built from (match setup, card gallery), in the game's theme (see <see cref="UITheme"/>).</summary>
    public static class MenuWidgets {
        public static Color PanelColor => UITheme.Panel;
        public static Color ButtonColor => UITheme.PanelLight;
        public static readonly Color SelectedColor = UITheme.Hex(0x6B5222);
        public static Color TextColor => UITheme.Parchment;
        public static Color AccentColor => UITheme.Gold;

        /// <summary>A navy panel with a title, stacking what's added to it from the top.</summary>
        public static Transform AddColumn(Transform parent, string title) {
            var column = NewObject(title, parent);
            UITheme.StylePanel(column.AddComponent<Image>(), PanelColor);
            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 20);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperCenter;
            SetControls(layout, expandHeight: false);
            UITheme.StyleHeading(AddText(column.transform, title, 36, TextColor, FontStyles.Bold, height: 52), TextColor);
            return column.transform;
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
