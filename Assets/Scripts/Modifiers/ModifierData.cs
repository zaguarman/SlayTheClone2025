using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Needed for Select/ToList
using Enums;
using static DebugLogger; // If logging is needed here

[CreateAssetMenu(fileName = "NewModifier", menuName = "Game Data/Modifier Data")]
public class ModifierData : ScriptableObject {
    [Tooltip("Unique identifier for this modifier type. Auto-generated if empty.")]
    public string modifierId;
    public string displayName = "New Modifier";
    [TextArea] public string description = "Modifier description.";
    public Sprite icon; // Keep icon here for editor assignment

    [Header("Behavior")]
    [Tooltip("-1 = Permanent, > 0 = Duration in Turns")]
    public int duration = -1; // Default to Permanent
    [Tooltip("-1 = Infinite Stacks, 0 = Non-Stackable, 1 = Refresh Duration, > 1 = Stackable with Limit")]
    public int maxStacks = 1; // Default to RefreshDuration style

    [Header("Effects")]
    public List<StatAdjustment> statAdjustments = new List<StatAdjustment>();
    public List<string> tags = new List<string>(); // e.g., "Buff", "Debuff", "Poison"

    // Method to convert this ScriptableObject to the runtime definition
    public ModifierDefinition ToModifierDefinition() {
        // Deep copy lists to avoid runtime modifying the ScriptableObject asset data
        var adjustmentsCopy = statAdjustments?.Select(adj => new StatAdjustment {
            statType = adj.statType,
            adjustmentType = adj.adjustmentType,
            value = adj.value
        }).ToList() ?? new List<StatAdjustment>();

        var tagsCopy = tags?.ToList() ?? new List<string>();

        return new ModifierDefinition(
            modifierId,
            displayName,
            description,
            duration,
            maxStacks,
            adjustmentsCopy,
            tagsCopy
        );
    }

    // Ensure modifierId is unique if created through editor
    // Updated validation logic for new rules
    void OnValidate() {
        if (string.IsNullOrEmpty(modifierId)) {
            modifierId = System.Guid.NewGuid().ToString();
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        // Validate maxStacks based on the new rules
        if (maxStacks < -1) maxStacks = -1; // Clamp minimum to -1 (infinite)

        // Validate duration
        if (duration < -1) duration = -1; // Clamp minimum to -1 (permanent)
        if (duration == 0) {
             // LogWarning($"Modifier '{displayName}' ({modifierId}): Duration of 0 is ambiguous. Use -1 for Permanent or > 0 for Turn-Based.", this);
             duration = 1; // Default to 1 turn if 0 is entered
        }
    }
}
