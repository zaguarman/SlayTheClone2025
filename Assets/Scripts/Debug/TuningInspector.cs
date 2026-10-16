#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// The tuning assets' sliders (<see cref="ArrowTuning"/>, <see cref="RouletteTuning"/>), each with its own reset button
    /// beside it (the user): back to the value it shipped with, that one only. The button is greyed out while the value is
    /// the original, and its tooltip says what the original is.
    /// </summary>
    public static class TuningInspector {
        private const float ButtonWidth = 24f;

        private const string HintsKey = "SlayTheClone.TuningInspector.Hints";

        public static void DrawWithResets(SerializedObject tuning, Object originals) {
            var shipped = new SerializedObject(originals);
            tuning.Update();
            // What each one does, written under it (the user): Unity's own tooltips only show over a focused window
            bool hints = EditorPrefs.GetBool(HintsKey, true);
            bool wanted = EditorGUILayout.ToggleLeft("Show what each one does", hints);
            if (wanted != hints) EditorPrefs.SetBool(HintsKey, wanted);
            var hintStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = new Color(0.62f, 0.62f, 0.62f) } };
            var property = tuning.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                if (property.propertyPath == "m_Script") continue;
                var original = shipped.FindProperty(property.propertyPath);
                var row = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(property, true));
                EditorGUI.PropertyField(new Rect(row.x, row.y, row.width - ButtonWidth - 4f, row.height), property, true);
                // On the field's own line: below its header, if it has one
                var button = new Rect(row.xMax - ButtonWidth, row.yMax - EditorGUIUtility.singleLineHeight, ButtonWidth, EditorGUIUtility.singleLineHeight);
                bool isOriginal = original == null || SerializedProperty.DataEquals(property, original);
                using (new EditorGUI.DisabledScope(isOriginal)) {
                    if (GUI.Button(button, new GUIContent("↺", $"Back to {Describe(original)}, the value it shipped with"), EditorStyles.miniButton)) {
                        tuning.CopyFromSerializedProperty(original);
                    }
                }
                if (wanted && !string.IsNullOrEmpty(property.tooltip)) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(property.tooltip, hintStyle);
                    EditorGUI.indentLevel--;
                }
            }
            tuning.ApplyModifiedProperties();
        }

        private static string Describe(SerializedProperty original) {
            if (original == null) return "the original";
            switch (original.propertyType) {
                case SerializedPropertyType.Float: return original.floatValue.ToString("0.###");
                case SerializedPropertyType.Integer: return original.intValue.ToString();
                case SerializedPropertyType.Boolean: return original.boolValue ? "on" : "off";
                case SerializedPropertyType.Enum: return original.enumDisplayNames[original.enumValueIndex];
                default: return "the original";
            }
        }
    }
}
#endif
