#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Zeitgeist {
    /// <summary>
    /// The coin glyphs the main font lacks live in the static fallback font asset (Ψ λ → ∞ ☺, and since 2026-10-17 the four
    /// factions' Ω Φ). UI > Add Fallback Glyphs bakes <see cref="Glyphs"/> into it: a glyph not in it draws as a box (the user:
    /// "fix these font emojis missing"); CardDataTests checks every coin glyph is drawable.
    /// </summary>
    public static class FallbackGlyphs {
        public const string Glyphs = "Ψλ→∞☺ΩΦ";
        private const string AssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";
        private const string SourceFont = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        [MenuItem("UI/Add Fallback Glyphs")]
        public static void Add() {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (font == null) {
                Debug.LogError($"No fallback font at {AssetPath}");
                return;
            }
            // The editor's reference to the .ttf, which a dynamic atlas draws its glyphs from
            var serialized = new SerializedObject(font);
            var editorRef = serialized.FindProperty("m_SourceFontFile_EditorRef");
            if (editorRef != null && editorRef.objectReferenceValue == null) {
                editorRef.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            bool added = font.TryAddCharacters(Glyphs, out string missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(font);
            if (font.atlasTexture != null) EditorUtility.SetDirty(font.atlasTexture);
            AssetDatabase.SaveAssets();
            Debug.Log(added ? $"The fallback font draws {Glyphs}" : $"The fallback font couldn't take: {missing}");
        }
    }
}
#endif
