using UnityEngine;
using UnityEditor;

[System.Serializable]
public class TriStateField {
    public enum State {
        None,       // Neutral state
        Checked,    // Include in filter
        Crossed     // Exclude from filter
    }

    [SerializeField] private State currentState = State.None;

    public State CurrentState {
        get => currentState;
        set => currentState = value;
    }

    public void CycleState() {
        currentState = (State)(((int)currentState + 1) % 3);
    }

    public bool ShouldInclude(bool value) {
        return currentState switch {
            State.None => true,       // Pass through all values
            State.Checked => value,   // Only include true values
            State.Crossed => !value,  // Only include false values
            _ => true
        };
    }

    // Custom GUI for the TriStateField
    public static class CustomGUI {
        private static readonly GUIContent[] IconContents = {
            EditorGUIUtility.IconContent("Toggle Icon"),         // None
            EditorGUIUtility.IconContent("toggle on"),           // Checked
            EditorGUIUtility.IconContent("toggle mixed")         // Crossed
        };

        public static State DrawTriStateField(Rect position, State currentState, GUIContent label) {
            Rect buttonRect = EditorGUI.PrefixLabel(position, label);

            if (GUI.Button(buttonRect, IconContents[(int)currentState], EditorStyles.miniButton)) {
                currentState = (State)(((int)currentState + 1) % 3);
            }

            return currentState;
        }

        public static float GetHeight() {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}