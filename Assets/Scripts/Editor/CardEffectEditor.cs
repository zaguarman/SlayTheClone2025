#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using static Enums;

[CustomPropertyDrawer(typeof(CardEffect))]
public class CardEffectDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Indent child properties
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // Draw foldable header
        SerializedProperty triggerProp = property.FindPropertyRelative("trigger");
        string headerText = $"Effect: {triggerProp.enumDisplayNames[triggerProp.enumValueIndex]}";
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), 
            property.isExpanded, headerText, true);

        if (property.isExpanded)
        {
            // Draw properties with indentation
            EditorGUI.indentLevel = indent + 1;
            float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            // Draw effect type and trigger
            SerializedProperty effectTypeProp = property.FindPropertyRelative("effectType");
            EditorGUI.PropertyField(
                new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight),
                effectTypeProp, new GUIContent("Effect Type"));
            yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            EditorGUI.PropertyField(
                new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight),
                triggerProp, new GUIContent("Trigger"));
            yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            // Draw actions array
            SerializedProperty actionsProp = property.FindPropertyRelative("actions");
            EditorGUI.PropertyField(
                new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight),
                actionsProp, new GUIContent("Actions"), true);
        }

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        
        if (property.isExpanded)
        {
            // Base height for effect type and trigger
            height += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2;
            
            // Add height for actions array
            SerializedProperty actionsProp = property.FindPropertyRelative("actions");
            height += EditorGUI.GetPropertyHeight(actionsProp, true);
        }
        
        return height;
    }
}

[CustomPropertyDrawer(typeof(EffectAction))]
public class EffectActionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw foldable header
        SerializedProperty actionTypeProp = property.FindPropertyRelative("actionType");
        SerializedProperty valueProp = property.FindPropertyRelative("value");
        string headerText = $"{actionTypeProp.enumDisplayNames[actionTypeProp.enumValueIndex]} ({valueProp.intValue})";
        
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), 
            property.isExpanded, headerText, true);

        if (property.isExpanded)
        {
            // Draw properties with indentation
            float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            // Draw action type
            EditorGUI.PropertyField(
                new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight),
                actionTypeProp, new GUIContent("Action Type"));
            yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            // Draw value
            EditorGUI.PropertyField(
                new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight),
                valueProp, new GUIContent("Value"));
            yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            // Draw target type
            SerializedProperty targetTypeProp = property.FindPropertyRelative("targetType");
            EditorGUI.PropertyField(
                new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight),
                targetTypeProp, new GUIContent("Target Type"));
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        
        if (property.isExpanded)
        {
            // Add height for action type, value, and target type
            height += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3;
        }
        
        return height;
    }
}
#endif