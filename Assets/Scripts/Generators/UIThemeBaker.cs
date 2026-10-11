#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Makes the <see cref="UITheme"/> show in the editor, without playing: bakes the theme's sprites and heading material
    /// into Resources/UITheme as assets, then styles the scenes the way the game does when it starts (the table and its
    /// controls, the title screen, the pause menu) and saves them. Running it again restyles in place.
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
            ApplyToOpenScenes();
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
