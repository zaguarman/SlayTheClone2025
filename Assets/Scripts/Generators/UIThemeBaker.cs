#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Zeitgeist {
    /// <summary>
    /// Makes the <see cref="UITheme"/> show in the editor, without playing: bakes the theme's sprites and heading material
    /// into Resources/UITheme as assets, restyles the card prefab, then styles the scenes the way the game does when it
    /// starts (the table and its controls, the title screen, the pause menu) and saves them. Running it again restyles in place.
    /// </summary>
    public static class UIThemeBaker {
        private const string AssetFolder = "Assets/Resources/" + UITheme.ResourcesFolder;

        [MenuItem("UI/Apply Theme To Scenes")]
        public static void ApplyToScenes() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                Debug.LogWarning("Stop playing before applying the theme to the scenes.");
                return;
            }
            var openScenes = Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt).ToList();
            if (openScenes.Any(s => s.isDirty)) {
                Debug.LogWarning("Save (or discard) the changes to the open scenes before applying the theme to every scene.");
                return;
            }

            BakeAssets();
            StyleCardPrefab();
            string returnTo = EditorSceneManager.GetActiveScene().path;
            foreach (var buildScene in EditorBuildSettings.scenes.Where(s => s.enabled)) {
                var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                ApplyToOpenScenes();
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Theme applied to {buildScene.path}");
            }
            if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        }

        [MenuItem("UI/Apply Theme To Open Scene")]
        public static void ApplyToOpenScene() {
            BakeAssets();
            StyleCardPrefab();
            ApplyToOpenScenes();
        }

        /// <summary>
        /// Lays the open scene's table out again, as the game does when it starts, without baking the theme: what the
        /// Battlefield Tuning sliders say shows in the editor without playing. The scene is marked to save.
        /// </summary>
        public static void RedressOpenTable() {
            var references = Object.FindFirstObjectByType<GameReferences>(FindObjectsInactive.Include);
            var gameUI = Object.FindFirstObjectByType<GameUI>(FindObjectsInactive.Include);
            if (references == null || gameUI == null || !gameUI.TryGetComponent<BoardSkin>(out var skin)) return;
            skin.Dress(references, MatchSetup.DefaultPlayerName, references.Player2Name);
            EditorSceneManager.MarkSceneDirty(gameUI.gameObject.scene);
        }

        private static void ApplyToOpenScenes() {
            var references = Object.FindFirstObjectByType<GameReferences>(FindObjectsInactive.Include);
            var gameUI = Object.FindFirstObjectByType<GameUI>(FindObjectsInactive.Include);
            if (references != null && gameUI != null) {
                var skin = gameUI.GetComponent<BoardSkin>();
                if (skin == null) skin = gameUI.gameObject.AddComponent<BoardSkin>();
                skin.Dress(references, MatchSetup.DefaultPlayerName, references.Player2Name);
            }
            foreach (var menu in Object.FindObjectsByType<MainMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None)) menu.ApplyTheme();
            foreach (var menu in Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None)) menu.ApplyTheme();

            for (int i = 0; i < EditorSceneManager.sceneCount; i++) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetSceneAt(i));
        }

        /// <summary>
        /// Restyles the card face: a dark rim around a frame in the card's faction color (set per card by CardController),
        /// parchment title and text boxes, a brass-edged art window, the type line on a band of the faction's color,
        /// a small brass square for the Deployment Time in the card's top-left corner (like Slay the Spire's cost orb, it
        /// stays in sight when the hand's cards overlap), and a brass plate for the stats.
        /// </summary>
        public static void StyleCardPrefab() {
            var root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
            try {
                var card = root.GetComponent<CardController>();
                var parchment = UITheme.Parchment;
                var rim = UITheme.Hex(0x0B101C);

                UITheme.Round(root.GetComponent<Image>(), rim, 2.5f);
                var shadow = root.GetComponent<Shadow>();
                if (shadow != null) {
                    shadow.effectColor = UITheme.HardShadow;
                    shadow.effectDistance = new Vector2(0f, -6f);
                }

                var frame = Part(root, "Frame");
                UITheme.Round(frame, UITheme.CardFrameColor(Enums.Faction.None), 3f);
                EnsureTrim(frame.transform, UITheme.WithAlpha(UITheme.Gold, 0.55f), 3f, 0f);

                var titleBar = Part(root, "TitleBar");
                UITheme.Round(titleBar, parchment, 5f);
                EnsureTrim(titleBar.transform, UITheme.GoldDim, 5f, 0f);

                // The Deployment Time box (an hourglass and the Days), in the top-left corner inside the card: the hand's cards overlap from the right
                var pip = root.transform.Find("ReadyPip") != null ? Part(root, "ReadyPip") : Part(root, "TitleBar/ReadyPip");
                var pipRect = pip.rectTransform;
                pipRect.SetParent(root.transform, false);
                pipRect.anchorMin = pipRect.anchorMax = new Vector2(0f, 1f);
                pipRect.pivot = new Vector2(0.5f, 0.5f);
                pipRect.sizeDelta = new Vector2(CardController.PipWidth, CardController.PipSize);
                pipRect.anchoredPosition = new Vector2(CardController.PipInset + CardController.PipWidth / 2f, -(CardController.PipInset + CardController.PipSize / 2f));
                pipRect.SetAsLastSibling();
                // Part of the title bar: its left end in gold, the bar's own corners and trim, no outline or shadow of its own
                UITheme.Round(pip, UITheme.Gold, 5f);
                EnsureTrim(pip.transform, UITheme.GoldDim, 5f, 0f);
                var pipShadow = pip.GetComponent<Shadow>();
                if (pipShadow != null) Object.DestroyImmediate(pipShadow, true);
                var readyText = Part<TextMeshProUGUI>(root, "ReadyPip/ReadyText");
                UITheme.Stretch(readyText.rectTransform, 0f);
                readyText.alignment = TextAlignmentOptions.Center;
                readyText.fontStyle = FontStyles.Bold;
                readyText.enableAutoSizing = false;
                readyText.fontSize = 15f;
                readyText.color = UITheme.Ink;
                Part<TextMeshProUGUI>(root, "TitleBar/NameText").alignment = TextAlignmentOptions.Midline; // Centered
                var nameRect = Part<TextMeshProUGUI>(root, "TitleBar/NameText").rectTransform;
                nameRect.offsetMin = new Vector2(4f, nameRect.offsetMin.y); // The whole bar; CardController makes room for the square when it shows
                nameRect.offsetMax = new Vector2(-4f, nameRect.offsetMax.y);

                var artFrame = Part(root, "ArtFrame");
                UITheme.Round(artFrame, UITheme.Ink, 8f);
                EnsureTrim(artFrame.transform, UITheme.GoldDim, 8f, 0f).transform.SetAsLastSibling();

                var typeBar = Part(root, "TypeBar");
                UITheme.Round(typeBar, UITheme.FactionColor(Enums.Faction.None), 5f);
                EnsureTrim(typeBar.transform, UITheme.WithAlpha(UITheme.Ink, 0.6f), 5f, 0f);
                var typeText = Part<TextMeshProUGUI>(root, "TypeBar/TypeText");
                typeText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                typeText.color = UITheme.Ink;

                var textBox = Part(root, "TextBox");
                UITheme.Round(textBox, parchment, 5f);
                EnsureTrim(textBox.transform, UITheme.WithAlpha(UITheme.GoldDim, 0.6f), 5f, 0f);

                var statsBox = Part(root, "StatsBox");
                UITheme.Round(statsBox, UITheme.Gold, 3f);
                var outline = statsBox.GetComponent<Outline>();
                if (outline != null) Object.DestroyImmediate(outline, true);
                EnsureTrim(statsBox.transform, UITheme.Ink, 3f, 0f);
                UITheme.AddHardShadow(statsBox, 2f);

                foreach (var path in new[] { "TitleBar/NameText", "ReadyPip/ReadyText", "TypeBar/TypeText", "TextBox/RulesText", "StatsBox/StatsText" }) {
                    var text = Part<TextMeshProUGUI>(root, path);
                    text.color = UITheme.Ink;
                    text.transform.SetAsLastSibling(); // Above the trims
                }

                var serialized = new SerializedObject(card);
                serialized.FindProperty("frameImage").objectReferenceValue = frame;
                serialized.FindProperty("typeBarImage").objectReferenceValue = typeBar;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            }
            finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private const string CardPrefabPath = "Assets/Prefabs/CardPrefab.prefab";

        private static Image Part(GameObject root, string path) => Part<Image>(root, path);

        private static T Part<T>(GameObject root, string path) where T : Component {
            var part = root.transform.Find(path);
            if (part == null || !part.TryGetComponent(out T component)) throw new System.InvalidOperationException($"The card prefab has no {typeof(T).Name} at {path}");
            return component;
        }

        // A brass (or other) border over the part, made once and recolored on later runs
        private static Image EnsureTrim(Transform part, Color color, float cornerScale, float inset) {
            var existing = part.Find("Trim");
            var trim = existing != null ? existing.GetComponent<Image>() : UITheme.AddTrim(part, color, cornerScale, inset);
            trim.color = color;
            trim.pixelsPerUnitMultiplier = cornerScale;
            return trim;
        }

        /// <summary>Writes the theme's sprites as PNG sprite assets and its heading material, then has the theme use them.</summary>
        public static void BakeAssets() {
            Directory.CreateDirectory(AssetFolder);
            foreach (var (name, border, tiles) in UITheme.SpriteNames) {
                string path = $"{AssetFolder}/{name}.png";
                var texture = UITheme.Draw(name);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            var font = TMP_Settings.defaultFontAsset;
            if (font != null) {
                string materialPath = $"{AssetFolder}/{UITheme.HeadingMaterialName}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) {
                    material = new Material(font.material) { name = UITheme.HeadingMaterialName };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                else {
                    material.shader = font.material.shader;
                    material.CopyPropertiesFromMaterial(font.material);
                }
                UITheme.SetDropShadow(material);
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
            UITheme.ForgetSprites(); // Use the assets from now on, not sprites drawn earlier
        }
    }
}
#endif
