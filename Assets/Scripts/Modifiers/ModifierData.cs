using UnityEngine;
using System.Collections.Generic;
using Enums;

[CreateAssetMenu(fileName = "NewModifier", menuName = "Game Data/Modifier Data")]
public class ModifierData : ScriptableObject {
    [Tooltip("Unique identifier for this modifier type. Auto-generated if empty.")]
    public string modifierId;
    public string displayName = "New Modifier";
    [TextArea] public string description = "Modifier description.";
    public Sprite icon; // Assign in Inspector for UI

    [Header("Behavior")]
    public ModifierDurationType durationType = ModifierDurationType.Permanent;
    [Min(1)] public int baseDuration = 1; // Only used if durationType is TurnBased
    public ModifierStackingType stackingType = ModifierStackingType.Stackable;
    [Min(1)] public int maxStacks = 1; // Only used if stackingType is Stackable

    [Header("Effects")]
    public List<StatAdjustment> statAdjustments = new List<StatAdjustment>();
    public List<string> tags = new List<string>(); // e.g., "Buff", "Debuff", "Poison"

    // Ensure modifierId is unique if created through editor
    void OnValidate() {
        if (string.IsNullOrEmpty(modifierId)) {
            modifierId = System.Guid.NewGuid().ToString();
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        // Ensure maxStacks is at least 1
        if (maxStacks < 1) maxStacks = 1;
        // NonStackable and RefreshDuration inherently have maxStacks of 1
        if (stackingType == ModifierStackingType.NonStackable || stackingType == ModifierStackingType.RefreshDuration) {
            maxStacks = 1;
        }
    }
}
