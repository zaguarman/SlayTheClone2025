using System.Collections.Generic;
using Enums; // For StatType and StatAdjustmentType

// Plain C# class to hold modifier definition at runtime
public class ModifierDefinition {
    public string modifierId;
    public string displayName;
    public string description;
    // Icon handling might need a separate strategy (e.g., loading from Resources by name/ID)
    // public Sprite icon;

    public int duration; // -1 = Permanent, > 0 = TurnBased duration
    public int maxStacks; // -1 = Infinite Stacks, 0 = NonStackable, 1 = RefreshDuration, > 1 = Stackable with limit

    public List<StatAdjustment> statAdjustments = new List<StatAdjustment>();
    public List<string> tags = new List<string>();

    // Helper properties for clarity
    public bool IsPermanent => duration == -1;
    public bool IsTurnBased => duration > 0;
    public bool IsStackable => maxStacks != 0; // Includes infinite, refresh, and limited stacking
    public bool HasInfiniteStacks => maxStacks == -1;
    public bool IsRefreshOnly => maxStacks == 1; // Defined rule: maxStacks=1 means RefreshDuration
    public bool IsNonStackable => maxStacks == 0;

    // Constructor (optional, but can be useful)
    public ModifierDefinition(string id, string name, string desc, int dur, int stacks, List<StatAdjustment> adjustments, List<string> tagsList) {
        modifierId = id;
        displayName = name;
        description = desc;
        duration = dur;
        maxStacks = stacks;
        statAdjustments = adjustments ?? new List<StatAdjustment>();
        tags = tagsList ?? new List<string>();
    }

    // Default constructor for flexibility
    public ModifierDefinition() { }
}
