using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    [Serializable]
    public class TagSettings {
        [SerializeField] private int tagValue;
        [SerializeField] private TriStateFilter filterState = TriStateFilter.Neutral;
        public Color color;

        public LogTag Tag {
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
            "UIComponent"
        };
        [SerializeField] private DebugLoggerSettings settings;

        private Dictionary<LogTag, string> tagColorMap;
        private HashSet<string> enabledClasses;
        private static DebugLogger instance;
        #endregion

        #region Unity Lifecycle
        private void Awake() {
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
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

        /// <summary>
        /// Drops plain logs (warnings and errors still go through). Tests that play whole turns turn it on: the game
        /// logs thousands of lines a turn, and writing them to the console is most of what a turn costs.
        /// </summary>
        public static bool MuteInfo { get; set; }

        public static void Log(
            object message,
            LogTag tags,
            [CallerFilePath] string sourceFilePath = "") {
            if (MuteInfo) return;
            instance?.LogWithType(LogType.Log, message, tags, sourceFilePath);
        }

        // Warnings and errors still reach the console when there's no logger in the scene (in tests, for instance)
        public static void LogWarning(
            object message,
            LogTag tags,
            [CallerFilePath] string sourceFilePath = "") {
            if (instance != null) instance.LogWithType(LogType.Warning, message, tags, sourceFilePath);
            else Debug.LogWarning($"[{tags}] {message}");
        }

        // Errors are never filtered out, so they can't go unnoticed
        public static void LogError(
            object message,
            LogTag tags,
            [CallerFilePath] string sourceFilePath = "") {
            if (instance != null) instance.LogWithType(LogType.Error, message, tags, sourceFilePath);
            else Debug.LogError($"[{tags}] {message}");
        }

        private void LogWithType(
            LogType logType,
            object message,
            LogTag tags,
            string sourceFilePath) {
            if (logType != LogType.Error && !ShouldLog(tags, sourceFilePath)) return;

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

            tagColorMap = new Dictionary<LogTag, string>();
            foreach (var setting in settings.TagSettings) {
                if (!tagColorMap.ContainsKey(setting.Tag)) {
                    tagColorMap[setting.Tag] = setting.HexColor;
                }
            }

            InitializeStackTraceLogTypes();
        }

        private bool ShouldLog(LogTag tags, string sourceFilePath) {
            if (tagColorMap == null) {
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

            var activeTags = GetActiveTags(tags).ToList();

            bool hasIncludedTags = false;
            bool hasExcludedTags = false;

            foreach (var tag in activeTags) {
                var tagSetting = settings.TagSettings.FirstOrDefault(ts => ts.Tag == tag);
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
                settings.TagSettings.FirstOrDefault(ts => ts.Tag == tag)?.FilterState == TriStateFilter.Neutral);
        }

        private string FormatMessage(
            object message,
            LogTag tags,
            string sourceFilePath) {
            return $"{GetClassName(sourceFilePath)}: [{ColorizeTags(tags)}] {message}";
        }

        private string GetClassName(string sourceFilePath) {
            if (string.IsNullOrEmpty(sourceFilePath)) return "Unknown";
            return System.IO.Path.GetFileNameWithoutExtension(sourceFilePath);
        }

        // The single flags set in a combination (excluding None and All)
        private static IEnumerable<LogTag> GetActiveTags(LogTag tags) {
            return Enum.GetValues(typeof(LogTag))
                .Cast<LogTag>()
                .Where(tag => tag != LogTag.None && tag != LogTag.All && (tags & tag) != 0);
        }

        private string ColorizeTags(LogTag tags) {
            return string.Join("|", GetActiveTags(tags).Select(tag =>
                tagColorMap != null && tagColorMap.TryGetValue(tag, out string color) ? $"<color={color}>{tag}</color>" : tag.ToString()));
        }
        #endregion
    }
}
