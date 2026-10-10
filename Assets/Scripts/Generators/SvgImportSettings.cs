#if UNITY_EDITOR
using Unity.VectorGraphics;
using Unity.VectorGraphics.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configures how an SVG file is rasterized on import, keeping its full viewBox.
/// Settings are written through the importer's serialized fields: its C# property setters are not persisted.
/// </summary>
public static class SvgImportSettings {

    /// <returns>False if the path is not an SVG asset.</returns>
    public static bool Configure(string svgPath, SVGType svgType, int textureSize) {
        if (!(AssetImporter.GetAtPath(svgPath) is SVGImporter importer)) return false;

        var settings = new SerializedObject(importer);
        bool changed = SetInt(settings, "m_SvgType", (int)svgType)
            | SetInt(settings, "m_ViewportOptions", (int)ViewportOptions.PreserveViewport)
            | SetInt(settings, "m_KeepTextureAspectRatio", 1)
            | SetInt(settings, "m_TextureSize", textureSize)
            | SetInt(settings, "m_SampleCount", 4)
            | SetInt(settings, "m_FilterMode", (int)FilterMode.Bilinear);

        if (changed) {
            settings.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
        }
        return true;
    }

    private static bool SetInt(SerializedObject settings, string propertyName, int value) {
        var property = settings.FindProperty(propertyName);
        bool isBool = property.propertyType == SerializedPropertyType.Boolean;
        int current = isBool ? (property.boolValue ? 1 : 0) : property.intValue;
        if (current == value) return false;

        if (isBool) property.boolValue = value != 0;
        else property.intValue = value;
        return true;
    }
}
#endif
