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
        public bool modifyAttack = true; // Flag to indicate if Attack should be modified
        public bool modifyHealth = true; // Flag to indicate if Health should be modified
        public bool modifySpeed = false; // Flag to indicate if Speed should be modified

        // --- ApplyStatus Specific ---
        [Tooltip("Which status effect to apply (only used if actionType is ApplyStatus)")]
        public StatusEffectType statusEffectToApply = StatusEffectType.None;
        [Tooltip("Duration in turns (only used if actionType is ApplyStatus)")]
        public int statusDuration = 0; // 0 might mean permanent or invalid depending on status
        [Tooltip("Potency of the status effect (e.g., burn damage per turn)")]
        public int statusPotency = 0;
    }
}
