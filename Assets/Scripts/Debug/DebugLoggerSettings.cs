using UnityEngine;
using System;
using System.Collections.Generic;
using static DebugLogger;

[CreateAssetMenu(fileName = "DebugLoggerSettings", menuName = "Debug/Logger Settings")]
public class DebugLoggerSettings : ScriptableObject {
    [SerializeField] private List<TagSettings> tagSettings = new List<TagSettings>();
    [SerializeField] private List<ClassFilter> classFilters = new List<ClassFilter>();
    [SerializeField] private bool showStackTrace = false;

    public List<TagSettings> TagSettings => tagSettings;
    public List<ClassFilter> ClassFilters => classFilters;
    public bool ShowStackTrace => showStackTrace;

    public void Initialize() {
        if (tagSettings == null || tagSettings.Count == 0) {
            tagSettings = new List<TagSettings>();
            foreach (LogTag tag in Enum.GetValues(typeof(LogTag))) {
                if (tag != LogTag.None && tag != LogTag.All &&
                    DefaultColors.ContainsKey(tag)) {
                    tagSettings.Add(new TagSettings {
                        tag = tag,
                        FilterState = TriStateFilter.Neutral,
                        color = DefaultColors[tag]
                    });
                }
            }
        }
        if (classFilters == null) {
            classFilters = new List<ClassFilter>();
        }
    }
}