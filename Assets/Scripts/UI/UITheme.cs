using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The game's look in one place: a midnight-navy table with brass trim, drawn in flat colors and crisp lines
    /// (no blurs or glows). Colors, the procedurally drawn sprites every panel uses (so the project needs no UI
    /// texture assets), and helpers that style buttons, panels and labels the same way everywhere.
    /// </summary>
    public static class UITheme {
        #region Palette
        public static readonly Color TableDark = Hex(0x0A0F1C);
        public static readonly Color TableBase = Hex(0x111A2E);
        public static readonly Color Panel = Hex(0x172137, 0.97f);
        public static readonly Color PanelLight = Hex(0x223050);
        public static readonly Color Gold = Hex(0xE2B04A);
        public static readonly Color GoldDim = Hex(0x7D6430);
        public static readonly Color Parchment = Hex(0xF3E9D2);
        public static readonly Color TextMuted = Hex(0xA3ADC4);
        public static readonly Color Ink = Hex(0x141414);
        public static readonly Color HardShadow = new Color(0f, 0f, 0f, 0.5f);

        // Pokémon-style health bar: green, yellow below half, red below a quarter
        public static readonly Color HealthHigh = Hex(0x4CC46A);
        public static readonly Color HealthMid = Hex(0xF0C243);
        public static readonly Color HealthLow = Hex(0xE04B3A);

        // Battlefield mats and their slots
        public static readonly Color Mat = Hex(0x0A0F1C, 0.82f);
        public static readonly Color MatHover = Hex(0x18223A, 0.88f);
        public static readonly Color MatValid = Hex(0x15362A, 0.88f);
        public static readonly Color MatInvalid = Hex(0x3A181C, 0.88f);
        public static readonly Color SlotEmpty = Hex(0x000000, 0.28f); // Recessed into the mat
        public static readonly Color SlotCombat = Hex(0xE04B3A, 0.4f);

        /// <summary>Each faction's color, like a Magic color identity: its side of the table, mat trim and emblem.</summary>
        public static Color FactionColor(Faction faction) => faction switch {
            Faction.Corporate => Hex(0x3FBF8F),     // Money green
            Faction.Statecraft => Hex(0xD9483B),    // Propaganda red
            Faction.Zeitgeist => Hex(0xD95DD0),     // Vaporwave magenta
            Faction.Myth => Hex(0xC9A23D),
            Faction.ShadowOps => Hex(0x7C8A99),
            Faction.Tech => Hex(0x3DA9D6),
            Faction.DigitalSociety => Hex(0x5D7BD6),
            Faction.Mind => Hex(0x9A6CD6),
            Faction.Utopia => Hex(0xE8E0A0),
            _ => Gold
        };

        /// <summary>A one-glyph emblem for a faction, shown on the player's plaque.</summary>
        public static string FactionGlyph(Faction faction) => faction switch {
            Faction.Corporate => "$",
            Faction.Statecraft => "§",
            Faction.Zeitgeist => "#",
            Faction.None => "~",
            _ => CardTextFormatter.FactionName(faction).Substring(0, 1)
        };

        public static Color HealthColor(float fraction) =>
            fraction > 0.5f ? HealthHigh : fraction > 0.25f ? HealthMid : HealthLow;

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        #endregion

        #region Sprites
        private const int CornerRadius = 16; // Pixels; a sliced sprite shows its corners at this many UI units
        private const int LatticeSize = 56;
        private static Sprite rounded;
        private static Sprite roundedOutline;
        private static Sprite cornerBrackets;
        private static Sprite lattice;

        /// <summary>A white rounded rectangle, sliced. Corner size can be scaled with Image.pixelsPerUnitMultiplier.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(CornerRadius, 0);

        /// <summary>The outline of <see cref="Rounded"/>, 3 pixels wide.</summary>
        public static Sprite RoundedOutline => roundedOutline != null ? roundedOutline : roundedOutline = MakeRounded(CornerRadius, 3);

        /// <summary>Only the corners of <see cref="RoundedOutline"/>: the zone markers of a playmat.</summary>
        public static Sprite CornerBrackets => cornerBrackets != null ? cornerBrackets : cornerBrackets = MakeBrackets(64, 22, 10, 3);

        /// <summary>A tile of thin diagonal lines crossing into diamonds, for a patterned table.</summary>
        public static Sprite Lattice => lattice != null ? lattice : lattice = MakeLattice(LatticeSize);

        // Same outline, but drawn only inside the corner squares of a bigger tile, whose middle (the part a sliced
        // sprite stretches) stays empty
        private static Sprite MakeBrackets(int size, int arm, int radius, int outline) {
            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size / 2f;
            float inner = half - 1f - radius;
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float px = Mathf.Abs(x + 0.5f - half) - inner;
                    float py = Mathf.Abs(y + 0.5f - half) - inner;
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - outside) * Mathf.Clamp01(outside + outline + 0.5f);
                    bool inCorner = (x < arm || x >= size - arm) && (y < arm || y >= size - arm);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inCorner ? alpha * 255 : 0));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(arm, arm, arm, arm));
        }

        private static Sprite MakeRounded(int radius, int outline) {
            int size = radius * 2 + 4;
            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size / 2f;
            float inner = half - 1f - radius; // Half-extent of the straight part
            int border = radius + 2;

            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    // Signed distance from the rounded rectangle's edge, negative inside
                    float px = Mathf.Abs(x + 0.5f - half) - inner;
                    float py = Mathf.Abs(y + 0.5f - half) - inner;
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;

                    float alpha = Mathf.Clamp01(0.5f - outside);
                    if (outline > 0) alpha *= Mathf.Clamp01(outside + outline + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite MakeLattice(int size) {
            var texture = NewTexture(size, size);
            texture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    // Distance to the nearest line of each diagonal; the lines cross the tile's corners, so tiles join up
                    float a = Mathf.Repeat(x + y + 1f, size);
                    float b = Mathf.Repeat(x - y, size);
                    float distance = Mathf.Min(Mathf.Min(a, size - a), Mathf.Min(b, size - b)) / 1.414f;
                    float alpha = Mathf.Clamp01(1f - distance / 0.8f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D NewTexture(int width, int height) {
            return new Texture2D(width, height, TextureFormat.RGBA32, false) {
                name = "UITheme",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
        }
        #endregion

        #region Styling
        /// <summary>Gives the image the rounded sprite in the given color. A larger cornerScale makes the corners smaller.</summary>
        public static Image Round(Image image, Color color, float cornerScale = 1f) {
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = cornerScale;
            image.color = color;
            return image;
        }

        /// <summary>A rounded border drawn over the image, as a child that never takes clicks.</summary>
        public static Image AddTrim(Transform target, Color color, float cornerScale = 1f, float inset = 0f, string name = "Trim") {
            var trim = NewImage(name, target, color);
            trim.sprite = RoundedOutline;
            trim.type = Image.Type.Sliced;
            trim.pixelsPerUnitMultiplier = cornerScale;
            Stretch(trim.rectTransform, inset);
            return trim;
        }

        /// <summary>A flat, hard-edged shadow straight below the graphic, like a printed sticker on the table.</summary>
        public static void AddHardShadow(Graphic graphic, float depth = 5f) {
            if (graphic == null) return;
            var shadow = graphic.GetComponent<Shadow>();
            if (shadow == null) shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = HardShadow;
            shadow.effectDistance = new Vector2(0f, -depth);
            shadow.useGraphicAlpha = true;
        }

        public enum ButtonKind { Normal, Primary }

        /// <summary>
        /// A flat, brass-trimmed button standing on a hard shadow. Primary buttons (End Turn, Start) are brass with
        /// dark text; the others are navy with parchment text. Hovering brightens, pressing darkens.
        /// </summary>
        public static void StyleButton(Button button, ButtonKind kind = ButtonKind.Normal) {
            if (button == null) return;
            bool primary = kind == ButtonKind.Primary;

            var image = button.image != null ? button.image : button.GetComponent<Image>();
            if (image == null) image = button.gameObject.AddComponent<Image>();
            Round(image, primary ? Gold : PanelLight, 1.5f);
            button.targetGraphic = image;
            AddHardShadow(image);

            var colors = button.colors;
            // Rests a little dimmed so hovering can brighten it to its true color
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.68f, 0.66f, 0.64f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            var trim = button.transform.Find("Trim");
            if (trim == null) trim = AddTrim(button.transform, Color.white, 1.5f).transform;
            trim.GetComponent<Image>().color = primary ? Hex(0xFFF0B8) : GoldDim;

            foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true)) {
                label.color = primary ? Ink : Parchment;
                label.fontStyle |= FontStyles.Bold;
                label.raycastTarget = false;
                label.enableWordWrapping = false;
                label.transform.SetAsLastSibling(); // Above the trim
            }
        }

        /// <summary>A navy panel with a brass border, on a hard shadow.</summary>
        public static void StylePanel(Image image, Color? fill = null, Color? trim = null) {
            if (image == null) return;
            Round(image, fill ?? Panel, 1.5f);
            AddHardShadow(image, 6f);
            if (image.transform.Find("Trim") == null) AddTrim(image.transform, trim ?? GoldDim, 1.5f).transform.SetAsFirstSibling();
        }

        /// <summary>Title-style lettering: small caps, spaced out, on a hard drop shadow.</summary>
        public static void StyleHeading(TMP_Text text, Color? color = null) {
            if (text == null) return;
            text.color = color ?? Gold;
            text.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            text.characterSpacing = 4f;
            AddDropShadow(text);
        }

        /// <summary>A crisp shadow under the letters, so text reads on any background. Gives the text its own material.</summary>
        public static void AddDropShadow(TMP_Text text, float offset = 0.7f) {
            var material = text.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.8f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -offset);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            text.fontMaterial = material;
        }
        #endregion

        #region Building
        /// <summary>
        /// The table: a flat navy field under a faint diamond lattice. Added under everything else in the parent and
        /// stretched over it; the caller can resize it.
        /// </summary>
        public static RectTransform AddBackdrop(Transform parent, string name = "Backdrop") {
            var cloth = NewImage(name, parent, TableBase);
            var rect = cloth.rectTransform;
            rect.SetAsFirstSibling();
            Stretch(rect);

            var pattern = NewImage("Lattice", rect, new Color(1f, 1f, 1f, 0.035f));
            pattern.sprite = Lattice;
            pattern.type = Image.Type.Tiled;
            Stretch(pattern.rectTransform);
            return rect;
        }

        /// <summary>A small square turned on its corner: the ornament at the ends of lines.</summary>
        public static Image NewDiamond(string name, Transform parent, Color color, float size) {
            var diamond = NewImage(name, parent, color);
            diamond.rectTransform.sizeDelta = new Vector2(size, size);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return diamond;
        }

        public static Image NewImage(string name, Transform parent, Color color, bool raycastTarget = false) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center) {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            return label;
        }

        public static void Stretch(RectTransform rect, float inset = 0f) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Places the rect inside its parent: anchored at (anchorX, anchorY), at an offset, with a fixed size.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size) {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
        #endregion
    }
}
