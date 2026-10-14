#if UNITY_EDITOR
using System.IO;
using Unity.VectorGraphics.Editor;
using UnityEditor;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Links every card asset to its illustration by matching file name. The pixel art comes first:
    /// Assets/Scriptables/Cards/&lt;Theme&gt;/&lt;Card&gt;.asset  ->  Assets/Art/PixelCards/&lt;Card&gt;.png (Tools/pixel_art.py)
    /// and a card without one keeps its old vector art:
    /// Assets/Scriptables/Cards/&lt;Theme&gt;/&lt;Card&gt;.asset  ->  Assets/Art/Cards/&lt;Theme&gt;/&lt;Card&gt;.svg (Tools/generate_card_art.py)
    /// The vector art a pixel picture replaced is archived in ArtArchive/Cards, outside Assets.
    /// </summary>
    public static class CardArtAssigner {
        private const string CardsFolder = "Assets/Scriptables/Cards";
        private const string ArtFolder = "Assets/Art/Cards";
        private const string PixelArtFolder = "Assets/Art/PixelCards";
        private const int ArtTextureSize = 768; // Longest edge of the rasterized vector art
        private const int PixelArtTextureSize = 2048; // Room for the 1152 x 648 pixel art at full size

        [MenuItem("Cards/Assign Card Art")]
        public static void AssignCardArt() {
            int pixel = 0;
            int vector = 0;
            int missing = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:CardDataScriptableObject", new[] { CardsFolder })) {
                var cardPath = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDataScriptableObject>(cardPath);

                var sprite = LoadPixelArt($"{PixelArtFolder}/{Path.GetFileNameWithoutExtension(cardPath)}.png");
                if (sprite != null) {
                    pixel++;
                } else {
                    // UI Images need the SVG rasterized into a sprite
                    var artPath = Path.ChangeExtension(cardPath.Replace(CardsFolder, ArtFolder), ".svg");
                    SvgImportSettings.Configure(artPath, SVGType.TexturedSprite, ArtTextureSize);
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
                    if (sprite == null) {
                        Debug.LogWarning($"No card art for {card.cardName} (expected {artPath})");
                        missing++;
                        continue;
                    }
                    vector++;
                }

                if (card.artwork != sprite) {
                    card.artwork = sprite;
                    EditorUtility.SetDirty(card);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Card art assigned: {pixel} pixel art, {vector} vector art ({missing} missing)");
        }

        /// <summary>The card's pixel art as a sprite, imported once as one: mipmapped so it shrinks cleanly onto a
        /// card, no alpha (the picture is opaque). Null when the card has no pixel art yet.</summary>
        private static Sprite LoadPixelArt(string path) {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return null;

            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                || !importer.mipmapEnabled || importer.maxTextureSize != PixelArtTextureSize) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = PixelArtTextureSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
