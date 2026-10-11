using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The game's look in one place: a dark card table with gold trim, like the title screen.
    /// Colors, the procedurally drawn sprites every panel uses (so the project needs no UI texture assets),
    /// and helpers that style buttons, panels and labels the same way everywhere.
    /// </summary>
    public static class UITheme {
        #region Palette
        public static readonly Color TableDark = Hex(0x120C18);
        public static readonly Color TableFelt = Hex(0x2B1E36);
        public static readonly Color Panel = Hex(0x1C1424, 0.96f);
        public static readonly Color PanelLight = Hex(0x2C2138);
        public static readonly Color Gold = Hex(0xE8B84A);
        public static readonly Color GoldDim = Hex(0x8A6A2A);
        public static readonly Color Parchment = Hex(0xF2E6C8);
        public static readonly Color TextMuted = Hex(0xB9A98A);
        public static readonly Color Ink = Hex(0x1C1A18);

        // Pokémon-style health bar: green, yellow below half, red below a quarter
        public static readonly Color HealthHigh = Hex(0x4CD06A);
        public static readonly Color HealthMid = Hex(0xF2C84B);
        public static readonly Color HealthLow = Hex(0xE8503A);

        // Battlefield mats and their slots
        public static readonly Color Mat = Hex(0x0E0A13, 0.55f);
        public static readonly Color MatHover = Hex(0x2A2036, 0.6f);
        public static readonly Color MatValid = Hex(0x1E4A2E, 0.65f);
        public static readonly Color MatInvalid = Hex(0x4A1E22, 0.65f);
        public static readonly Color SlotEmpty = Hex(0xE8D8B0, 0.06f);
        public static readonly Color SlotCombat = Hex(0xE8503A, 0.45f);

        /// <summary>Each faction's color, like a Magic color identity: mat trim, plaque emblem and deck accents.</summary>
        public static Color FactionColor(Faction faction) => faction switch {
            Faction.Corporate => Hex(0x4FB38A),     // Money green
            Faction.Statecraft => Hex(0xC9463D),    // Propaganda red
            Faction.Zeitgeist => Hex(0xD65DD1),     // Vaporwave magenta
            Faction.Myth => Hex(0xC9A23D),
            Faction.ShadowOps => Hex(0x6C7A89),
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
        #endregion

        #region Sprites
        private const int CornerRadius = 24; // Pixels; a sliced sprite shows its corners at this many UI units
        private static Sprite rounded;
        private static Sprite roundedOutline;
        private static Sprite softCircle;
        private static Sprite vignette;
        private static Sprite felt;
        private static Sprite verticalShine;

        /// <summary>A white rounded rectangle, sliced. Corner size can be scaled with Image.pixelsPerUnitMultiplier.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(CornerRadius, 0);

        /// <summary>The outline of <see cref="Rounded"/>, 3 pixels wide.</summary>
        public static Sprite RoundedOutline => roundedOutline != null ? roundedOutline : roundedOutline = MakeRounded(CornerRadius, 3);

        /// <summary>A white disc fading out to its edge: glows, and round badges when drawn small.</summary>
        public static Sprite SoftCircle => softCircle != null ? softCircle : softCircle = MakeRadial(64, (d) => Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));

        /// <summary>Transparent in the middle, darkening to the edges.</summary>
        public static Sprite Vignette => vignette != null ? vignette : vignette = MakeRadial(128, (d) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.35f, d)));

        /// <summary>Faint speckles to tile over a flat color so it reads as cloth.</summary>
        public static Sprite Felt => felt != null ? felt : felt = MakeFelt(128);

        /// <summary>Light at the top fading to nothing: a glossy highlight for buttons and bars.</summary>
        public static Sprite VerticalShine => verticalShine != null ? verticalShine : verticalShine = MakeVerticalShine(32);

        private static Sprite MakeRounded(int radius, int outline) {
            int size = radius * 2 + 4;
            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size / 2f;
            float inner = half - 1f - radius; // Half-extent of the straight part

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
            int border = radius + 2;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite MakeRadial(int size, System.Func<float, float> alphaAtDistance) {
            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alphaAtDistance(d)) * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeFelt(int size) {
            var texture = NewTexture(size, size);
            texture.wrapMode = TextureWrapMode.Repeat;
            var random = new System.Random(7); // Same cloth every run
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) {
                byte shade = (byte)(random.Next(2) == 0 ? 0 : 255);
                pixels[i] = new Color32(shade, shade, shade, (byte)random.Next(0, 14));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeVerticalShine(int height) {
            var texture = NewTexture(1, height);
            for (int y = 0; y < height; y++) {
                float t = y / (height - 1f); // 0 at the bottom
                texture.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Pow(t, 2.2f)));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f), 100f);
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

        /// <summary>A glossy highlight over the top half of the image.</summary>
        public static Image AddShine(Transform target, float alpha = 0.18f) {
            var shine = NewImage("Shine", target, new Color(1f, 1f, 1f, alpha));
            shine.sprite = VerticalShine;
            var rect = shine.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 0f);
            rect.offsetMax = new Vector2(-6f, -3f);
            return shine;
        }

        public enum ButtonKind { Normal, Primary }

        /// <summary>
        /// A rounded, gold-trimmed button. Primary buttons (End Turn, Start) are gold with dark text;
        /// the others are dark with parchment text. Hovering brightens, pressing darkens.
        /// </summary>
        public static void StyleButton(Button button, ButtonKind kind = ButtonKind.Normal) {
            if (button == null) return;
            bool primary = kind == ButtonKind.Primary;

            var image = button.image != null ? button.image : button.GetComponent<Image>();
            if (image == null) image = button.gameObject.AddComponent<Image>();
            Round(image, primary ? Gold : PanelLight, 1.5f);
            button.targetGraphic = image;

            var colors = button.colors;
            // Rests a little dimmed so hovering can brighten it to its true color
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.68f, 0.66f, 0.64f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            if (button.transform.Find("Trim") == null) {
                AddTrim(button.transform, primary ? Hex(0xFFF0B8, 0.9f) : GoldDim, 1.5f);
                AddShine(button.transform, primary ? 0.35f : 0.12f);
            }

            foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true)) {
                label.color = primary ? Ink : Parchment;
                label.fontStyle |= FontStyles.Bold;
                label.raycastTarget = false;
                label.enableWordWrapping = false;
                label.transform.SetAsLastSibling(); // Above the trim and shine
            }
        }

        /// <summary>A rounded dark panel with a gold border.</summary>
        public static void StylePanel(Image image, Color? fill = null, Color? trim = null) {
            if (image == null) return;
            Round(image, fill ?? Panel);
            if (image.transform.Find("Trim") == null) AddTrim(image.transform, trim ?? GoldDim).transform.SetAsFirstSibling();
        }

        /// <summary>Title-style lettering: small caps, spaced out, with a soft drop shadow.</summary>
        public static void StyleHeading(TMP_Text text, Color? color = null) {
            if (text == null) return;
            text.color = color ?? Gold;
            text.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            text.characterSpacing = 4f;
            AddDropShadow(text);
        }

        /// <summary>A soft shadow under the letters, so text reads on any background. Gives the text its own material.</summary>
        public static void AddDropShadow(TMP_Text text, float offset = 0.6f) {
            var material = text.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.75f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -offset);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.4f);
            text.fontMaterial = material;
        }
        #endregion

        #region Building
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
