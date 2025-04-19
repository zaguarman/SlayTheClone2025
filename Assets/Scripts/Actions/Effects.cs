using System.Collections.Generic;
using UnityEngine;
using static Enums;

// Keep CardEffect as is
public class Effects : MonoBehaviour {
    public class CardEffect {
        public EffectType effectType;
        public EffectTrigger trigger;
        public List<EffectAction> actions = new List<EffectAction>();
    }

    // Modify EffectAction
    public class EffectAction {
        public ActionType actionType;
        public int value; // General purpose value (e.g., damage amount, heal amount, draw count)
        public TargetType targetType;
        public TargetModifier targetModifier = TargetModifier.None;

        // --- Buff Specific ---
        public bool buffAttack = true;
        public bool buffHealth = true;
        // public ModifierCalculationType calculationType = ModifierCalculationType.Flat; // Optional: Add if needed per-action

        // --- ApplyStatus Specific ---
        [Tooltip("Which status effect to apply (only used if actionType is ApplyStatus)")]
        public StatusEffectType statusEffectToApply = StatusEffectType.None;
        [Tooltip("Duration in turns (only used if actionType is ApplyStatus)")]
        public int statusDuration = 0; // 0 might mean permanent or invalid depending on status
        [Tooltip("Potency of the status effect (e.g., burn damage per turn)")]
        public int statusPotency = 0;
    }
}
