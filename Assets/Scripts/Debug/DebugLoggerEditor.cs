#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using static DebugLogger;

[CustomEditor(typeof(DebugLogger))]
public class DebugLoggerEditor : Editor {
    private SerializedProperty settingsProp;
    private Editor settingsEditor;

    private void OnEnable() {
        settingsProp = serializedObject.FindProperty("settings");
    }

    public override void OnInspectorGUI() {
        serializedObject.Update();
        EditorGUILayout.PropertyField(settingsProp);
        if (settingsProp.objectReferenceValue != null) {
            CreateCachedEditor(settingsProp.objectReferenceValue, null, ref settingsEditor);
            settingsEditor.OnInspectorGUI();
        }
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(DebugLoggerSettings))]
public class DebugLoggerSettingsEditor : Editor {
    private SerializedProperty tagSettingsProp;
    private SerializedProperty classFiltersProp;
    private SerializedProperty showStackTraceProp;
    private bool showTagSettings = true;
    private bool showClassFilters = true;
    private Vector2 classListScrollPosition;
    private Vector2 tagListScrollPosition;
    private string classSearchString = "";
    private string tagSearchString = "";
    private GUIStyle buttonStyle;
    private GUIStyle nameStyle;
    private float rowHeight = 25;

    private readonly Color includeColor = new Color(0.5f, 1f, 0.5f);
    private readonly Color neutralColor = new Color(0.8f, 0.8f, 0.8f);
    private readonly Color excludeColor = new Color(1f, 0.5f, 0.5f);

    private void OnEnable() {
        tagSettingsProp = serializedObject.FindProperty("tagSettings");
        classFiltersProp = serializedObject.FindProperty("classFilters");
        showStackTraceProp = serializedObject.FindProperty("showStackTrace");

        if (!Application.isPlaying && (tagSettingsProp == null || tagSettingsProp.arraySize == 0)) {
            InitializeDefaultTagSettings();
        }
    }

    // Moved initialization to OnInspectorGUI to ensure it runs inside GUI context
    private void InitializeStyles() {
        if (buttonStyle == null) {
            buttonStyle = new GUIStyle(GUI.skin.button) {
                fixedWidth = 80
            };
        }

        if (nameStyle == null) {
            nameStyle = new GUIStyle(EditorStyles.label) {
                alignment = TextAnchor.MiddleLeft
            };
        }
    }

    public override void OnInspectorGUI() {
        // Initialize styles at the beginning of OnInspectorGUI
        InitializeStyles();

        serializedObject.Update();

        DrawHeaderTitle();
        DrawStackTraceAndButtons();
        DrawTagSettings();
        DrawClassFilters();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeaderTitle() {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug Logger Settings", EditorStyles.boldLabel);
    }

    private void DrawStackTraceAndButtons() {
        EditorGUILayout.PropertyField(showStackTraceProp);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private void DrawTagSettings() {
        showTagSettings = EditorGUILayout.Foldout(showTagSettings, "Tag Settings", true);
        if (!showTagSettings) return;

        EditorGUI.indentLevel++;
        tagSearchString = EditorGUILayout.TextField("Search", tagSearchString ?? "", GUILayout.Height(rowHeight));

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Cycle All Tags", GUILayout.Height(rowHeight))) {
            CycleAllTagsStates();
        }

        EditorGUILayout.Space();

        DrawTagList();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Reset Colors", GUILayout.Width(100))) {
            ResetTagColors();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel--;
    }

    private void DrawTagList() {
        if (tagSettingsProp == null) return;

        for (int i = 0; i < tagSettingsProp.arraySize; i++) {
            SerializedProperty tagSetting = tagSettingsProp.GetArrayElementAtIndex(i);
            if (tagSetting == null) continue;

            SerializedProperty tagValueProp = tagSetting.FindPropertyRelative("tagValue");
            SerializedProperty filterStateProp = tagSetting.FindPropertyRelative("filterState");
            SerializedProperty colorProp = tagSetting.FindPropertyRelative("color");

            if (tagValueProp == null || filterStateProp == null || colorProp == null) continue;

            var currentTag = (LogTag)tagValueProp.intValue;
            string tagName = currentTag.ToString();

            if (!string.IsNullOrEmpty(tagSearchString) &&
                !tagName.ToLower().Contains(tagSearchString.ToLower())) {
                continue;
            }

            EditorGUILayout.BeginHorizontal();

            // Column 1 - Filter state button
            var currentState = (TriStateFilter)filterStateProp.enumValueIndex;
            DrawTriStateButton(ref currentState, filterStateProp);

            // Column 2 - Tag name (with fixed width)
            EditorGUILayout.LabelField(tagName, nameStyle, GUILayout.Width(90), GUILayout.Height(rowHeight));

            // Column 3 - Color picker
            EditorGUILayout.PropertyField(colorProp, GUIContent.none, GUILayout.Height(rowHeight));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }
    }

    private void DrawClassFilters() {
        showClassFilters = EditorGUILayout.Foldout(showClassFilters, "Class Filters", true);
        if (!showClassFilters) return;

        EditorGUI.indentLevel++;
        classSearchString = EditorGUILayout.TextField("Search", classSearchString ?? "", GUILayout.Height(rowHeight));

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Cycle All Classes", GUILayout.Height(rowHeight))) {
            CycleAllClassStates();
        }

        EditorGUILayout.Space();

        DrawClassList();
        EditorGUI.indentLevel--;
    }

    private void DrawClassList() {
        var filteredClasses = AvailableClasses
            .Where(c => string.IsNullOrEmpty(classSearchString) ||
                       c.ToLower().Contains(classSearchString.ToLower()));

        foreach (var className in filteredClasses) {
            EditorGUILayout.BeginHorizontal();

            // Left column - Filter state button
            var currentState = GetClassFilterState(className);
            DrawTriStateButton(ref currentState, null);
            if (GUI.changed) {
                SetClassFilterState(className, currentState);
            }

            // Right column - Class name
            EditorGUILayout.LabelField(className, nameStyle, GUILayout.Height(rowHeight));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }
    }

    private void DrawTriStateButton(ref TriStateFilter state, SerializedProperty filterStateProp) {
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = GetFilterStateColor(state);

        if (GUILayout.Button(GetFilterStateText(state), buttonStyle, GUILayout.Height(rowHeight))) {
            state = GetNextState(state);
            if (filterStateProp != null) {
                filterStateProp.enumValueIndex = (int)state;
            }
        }

        GUI.backgroundColor = originalColor;
    }

    private string GetFilterStateText(TriStateFilter state) => state switch {
        TriStateFilter.Include => "Include",
        TriStateFilter.Exclude => "Exclude",
        _ => "Neutral"
    };

    private Color GetFilterStateColor(TriStateFilter state) => state switch {
        TriStateFilter.Include => includeColor,
        TriStateFilter.Exclude => excludeColor,
        _ => neutralColor
    };

    private TriStateFilter GetNextState(TriStateFilter currentState) => currentState switch {
        TriStateFilter.Neutral => TriStateFilter.Include,
        TriStateFilter.Include => TriStateFilter.Exclude,
        TriStateFilter.Exclude => TriStateFilter.Neutral,
        _ => TriStateFilter.Neutral
    };

    private void CycleAllTagsStates() {
        bool hasInclude = false;
        bool hasExclude = false;
        bool hasNeutral = false;

        for (int i = 0; i < tagSettingsProp.arraySize; i++) {
            var tagSetting = tagSettingsProp.GetArrayElementAtIndex(i);
            var filterStateProp = tagSetting.FindPropertyRelative("filterState");
            var currentState = (TriStateFilter)filterStateProp.enumValueIndex;

            switch (currentState) {
                case TriStateFilter.Include:
                    hasInclude = true;
                    break;
                case TriStateFilter.Exclude:
                    hasExclude = true;
                    break;
                case TriStateFilter.Neutral:
                    hasNeutral = true;
                    break;
            }
        }

        TriStateFilter targetState;
        if (hasInclude || (!hasExclude && !hasNeutral)) {
            targetState = TriStateFilter.Exclude;
        } else if (hasExclude) {
            targetState = TriStateFilter.Neutral;
        } else {
            targetState = TriStateFilter.Include;
        }

        SetAllTagStates(targetState);
    }

    private void CycleAllClassStates() {
        bool hasInclude = false;
        bool hasExclude = false;
        bool hasNeutral = false;

        foreach (var className in AvailableClasses) {
            var state = GetClassFilterState(className);
            switch (state) {
                case TriStateFilter.Include:
                    hasInclude = true;
                    break;
                case TriStateFilter.Exclude:
                    hasExclude = true;
                    break;
                case TriStateFilter.Neutral:
                    hasNeutral = true;
                    break;
            }
        }

        TriStateFilter targetState;
        if (hasInclude || (!hasExclude && !hasNeutral)) {
            targetState = TriStateFilter.Exclude;
        } else if (hasExclude) {
            targetState = TriStateFilter.Neutral;
        } else {
            targetState = TriStateFilter.Include;
        }

        SetAllClassStates(targetState);
    }

    private void SetAllTagStates(TriStateFilter state) {
        if (tagSettingsProp == null) return;

        for (int i = 0; i < tagSettingsProp.arraySize; i++) {
            var tagSetting = tagSettingsProp.GetArrayElementAtIndex(i);
            if (tagSetting != null) {
                var filterStateProp = tagSetting.FindPropertyRelative("filterState");
                if (filterStateProp != null) {
                    filterStateProp.enumValueIndex = (int)state;
                }
            }
        }
    }

    private void SetAllClassStates(TriStateFilter state) {
        foreach (var className in AvailableClasses) {
            SetClassFilterState(className, state);
        }
    }

    private TriStateFilter GetClassFilterState(string className) {
        if (classFiltersProp == null) return TriStateFilter.Neutral;

        for (int i = 0; i < classFiltersProp.arraySize; i++) {
            var filter = classFiltersProp.GetArrayElementAtIndex(i);
            var classNameProp = filter.FindPropertyRelative("className");
            var filterStateProp = filter.FindPropertyRelative("filterState");

            if (classNameProp.stringValue == className) {
                return (TriStateFilter)filterStateProp.enumValueIndex;
            }
        }

        return TriStateFilter.Neutral;
    }

    private void SetClassFilterState(string className, TriStateFilter state) {
        if (classFiltersProp == null) return;

        // Try to find existing filter
        for (int i = 0; i < classFiltersProp.arraySize; i++) {
            var filter = classFiltersProp.GetArrayElementAtIndex(i);
            var classNameProp = filter.FindPropertyRelative("className");

            if (classNameProp.stringValue == className) {
                var filterStateProp = filter.FindPropertyRelative("filterState");
                filterStateProp.enumValueIndex = (int)state;
                return;
            }
        }

        // If not found and state is not neutral, add new filter
        if (state != TriStateFilter.Neutral) {
            classFiltersProp.InsertArrayElementAtIndex(classFiltersProp.arraySize);
            var newFilter = classFiltersProp.GetArrayElementAtIndex(classFiltersProp.arraySize - 1);
            newFilter.FindPropertyRelative("className").stringValue = className;
            newFilter.FindPropertyRelative("filterState").enumValueIndex = (int)state;
        }
    }

    private void InitializeDefaultTagSettings() {
        serializedObject.Update();
        tagSettingsProp.ClearArray();

        var orderedTags = new[] {
            LogTag.UI,
            LogTag.Actions,
            LogTag.Effects,
            LogTag.Creatures,
            LogTag.Players,
            LogTag.Cards,
            LogTag.Combat,
            LogTag.Initialization
        };

        foreach (var tag in orderedTags) {
            if (DefaultColors.ContainsKey(tag)) {
                tagSettingsProp.InsertArrayElementAtIndex(tagSettingsProp.arraySize);
                var element = tagSettingsProp.GetArrayElementAtIndex(tagSettingsProp.arraySize - 1);

                element.FindPropertyRelative("tagValue").intValue = (int)tag;
                element.FindPropertyRelative("filterState").enumValueIndex = (int)TriStateFilter.Neutral;
                element.FindPropertyRelative("color").colorValue = DefaultColors[tag];
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void ResetTagColors() {
        if (tagSettingsProp == null) return;

        for (int i = 0; i < tagSettingsProp.arraySize; i++) {
            SerializedProperty tagSetting = tagSettingsProp.GetArrayElementAtIndex(i);
            if (tagSetting == null) continue;

            SerializedProperty tagValueProp = tagSetting.FindPropertyRelative("tagValue");
            SerializedProperty colorProp = tagSetting.FindPropertyRelative("color");

            if (tagValueProp == null || colorProp == null) continue;

            var currentTag = (LogTag)tagValueProp.intValue;
            if (DefaultColors.TryGetValue(currentTag, out Color defaultColor)) {
                colorProp.colorValue = defaultColor;
            }
        }
    }
}
#endif