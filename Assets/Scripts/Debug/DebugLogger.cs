using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using static DebugLogger;

[Serializable]
public class TagSettings {
    [SerializeField] private int tagValue;
    [SerializeField] private TriStateFilter filterState = TriStateFilter.Neutral;
    public Color color;

    public LogTag tag {
        get => (LogTag)tagValue;
        set => tagValue = (int)value;
    }

    public TriStateFilter FilterState {
        get => filterState;
        set => filterState = value;
    }

    public string HexColor => "#" + ColorUtility.ToHtmlStringRGB(color);
}

[Serializable]
public class ClassFilter {
    public string className;
    [SerializeField] private TriStateFilter filterState = TriStateFilter.Neutral;

    public TriStateFilter FilterState {
        get => filterState;
        set => filterState = value;
    }
}

public class DebugLogger : MonoBehaviour {
    #region Enums
    [Flags]
    public enum LogTag {
        None = 0,
        UI = 1,
        Actions = 2,
        Effects = 4,
        Creatures = 8,
        Players = 16,
        Cards = 32,
        Combat = 64,
        Initialization = 128,
        Turns = 256,
        All = ~0
    }

    public enum TriStateFilter {
        Include,    // Explicitly include items with this flag
        Neutral,    // Don't consider this flag in filtering
        Exclude     // Explicitly exclude items with this flag
    }
    #endregion

    #region Fields
    public static readonly Dictionary<LogTag, Color> DefaultColors = new Dictionary<LogTag, Color>() {
        { LogTag.UI, GetColorFromHex("#80FFFF") },
        { LogTag.Actions, GetColorFromHex("#FFE066") },
        { LogTag.Effects, GetColorFromHex("#FF99FF") },
        { LogTag.Creatures, GetColorFromHex("#90EE90") },
        { LogTag.Players, GetColorFromHex("#ADD8E6") },
        { LogTag.Cards, GetColorFromHex("#E0E0E0") },
        { LogTag.Combat, GetColorFromHex("#FF9999") },
        { LogTag.Initialization, GetColorFromHex("#DEB887") },
        { LogTag.Turns, GetColorFromHex("#FFA500") },
    };

    public static readonly string[] AvailableClasses = new string[] {
        "ActionsQueue",
        "ArrowIndicator",
        "BattlefieldArrowManager",
        "BattlefieldCombatHandler",
        "BattlefieldUI",
        "Card",
        "CardContainer",
        "CardController",
        "CardData",
        "CardDealingService",
        "CardDropZone",
        "CardFactory",
        "ContainerSettings",
        "Creature",
        "Deck",
        "Entity",
        "GameActions",
        "GameBootstrap",
        "GameManager",
        "GameMediator",
        "GameReferences",
        "GameUI",
        "HandUI",
        "HealthHandler",
        "Player",
        "PlayerUI",
        "Target",
        "TestSetup",
        "UIComponent"
    };
    [SerializeField] private DebugLoggerSettings settings;

    private Dictionary<LogTag, string> _tagColorMap;
    private HashSet<string> _enabledClasses;
    private static DebugLogger _instance;
    #endregion

    #region Unity Lifecycle
    private void Awake() {
        if (_instance != null && _instance != this) {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        InitializeLogger();
    }

    private void InitializeStackTraceLogTypes() {
        var stackTraceType = settings.ShowStackTrace ? StackTraceLogType.ScriptOnly : StackTraceLogType.None;

        Application.SetStackTraceLogType(LogType.Log, stackTraceType);
        Application.SetStackTraceLogType(LogType.Warning, stackTraceType);
        Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.Full);
        Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.Full);
        Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.Full);
    }

    private void OnValidate() {
        if (Application.isPlaying) {
            InitializeLogger();
        }
    }
    #endregion

    #region Methods
    private static Color GetColorFromHex(string hex) {
        Color color;
        ColorUtility.TryParseHtmlString(hex, out color);
        return color;
    }

    public static void Log(
        object message,
        LogTag tags,
        [CallerFilePath] string sourceFilePath = "") {
        _instance?.LogWithType(LogType.Log, message, tags, sourceFilePath);
    }

    public static void LogWarning(
        object message,
        LogTag tags,
        [CallerFilePath] string sourceFilePath = "") {
        _instance?.LogWithType(LogType.Warning, message, tags, sourceFilePath);
    }

    public static void LogError(
        object message,
        LogTag tags,
        [CallerFilePath] string sourceFilePath = "") {
        _instance?.LogWithType(LogType.Error, message, tags, sourceFilePath);
    }
    private void LogWithType(
        LogType logType,
        object message,
        LogTag tags,
        string sourceFilePath) {
        if (!ShouldLog(tags, sourceFilePath)) return;

        string formattedMessage = FormatMessage(message, tags, sourceFilePath);

        switch (logType) {
            case LogType.Log:
                Debug.Log(formattedMessage);
                break;
            case LogType.Warning:
                Debug.LogWarning(formattedMessage);
                break;
            case LogType.Error:
                Debug.LogError(formattedMessage);
                break;
        }
    }

    private void InitializeLogger() {
        if (settings == null) {
            Debug.LogError("DebugLoggerSettings asset is not assigned!");
            return;
        }

        settings.Initialize();

        _tagColorMap = new Dictionary<LogTag, string>();
        foreach (var setting in settings.TagSettings) {
            if (!_tagColorMap.ContainsKey(setting.tag)) {
                _tagColorMap[setting.tag] = setting.HexColor;
            }
        }

        InitializeStackTraceLogTypes();
    }

    private bool ShouldLog(LogTag tags, string sourceFilePath) {
        if (_tagColorMap == null) {
            InitializeLogger();
        }

        string className = GetClassName(sourceFilePath);

        // Check class filtering
        var classFilter = settings.ClassFilters.FirstOrDefault(cf => cf.className == className);
        if (classFilter != null) {
            if (classFilter.FilterState == TriStateFilter.Exclude) return false;
            if (classFilter.FilterState == TriStateFilter.Include) return true;
            // If Neutral, continue with tag filtering
        }

        // Skip tag filtering if LogTag.All is specified
        if (tags == LogTag.All) return true;

        // Get all the active tags from the input (excluding None and All)
        var activeTags = Enum.GetValues(typeof(LogTag))
            .Cast<LogTag>()
            .Where(tag => tag != LogTag.None && tag != LogTag.All && (tags & tag) != 0);

        bool hasIncludedTags = false;
        bool hasExcludedTags = false;

        foreach (var tag in activeTags) {
            var tagSetting = settings.TagSettings.FirstOrDefault(ts => ts.tag == tag);
            if (tagSetting != null) {
                switch (tagSetting.FilterState) {
                    case TriStateFilter.Include:
                        hasIncludedTags = true;
                        break;
                    case TriStateFilter.Exclude:
                        hasExcludedTags = true;
                        break;
                        // Neutral tags don't affect the decision
                }
            }
        }

        // If any tag is explicitly excluded, don't log
        if (hasExcludedTags) return false;

        // If we have any included tags, log
        if (hasIncludedTags) return true;

        // If we only have neutral tags, log
        return !activeTags.Any() || activeTags.All(tag =>
            settings.TagSettings.FirstOrDefault(ts => ts.tag == tag)?.FilterState == TriStateFilter.Neutral);
    }

    private string FormatMessage(
        object message,
        LogTag tags,
        string sourceFilePath) {
        string className = GetClassName(sourceFilePath);
        string tagList = GetTagList(tags);
        string coloredTags = ColorizeTags(tagList, tags);

        return $"{className}: [{coloredTags}] {message}";
    }

    private string GetClassName(string sourceFilePath) {
        if (string.IsNullOrEmpty(sourceFilePath)) return "Unknown";
        return System.IO.Path.GetFileNameWithoutExtension(sourceFilePath);
    }

    private string GetTagList(LogTag tags) {
        return string.Join("|",
            Enum.GetValues(typeof(LogTag))
                .Cast<LogTag>()
                .Where(tag => tag != LogTag.None && tag != LogTag.All && (tags & tag) != 0)
                .Select(tag => tag.ToString())
        );
    }

    private string ColorizeTags(string tagList, LogTag tags) {
        string[] individualTags = tagList.Split('|');

        return string.Join("|", individualTags.Select(tag =>
            Enum.TryParse(tag, out LogTag currentTag) && _tagColorMap.TryGetValue(currentTag, out string color)
                ? $"<color={color}>{tag}</color>"
                : tag
        ));
    }
    #endregion
}