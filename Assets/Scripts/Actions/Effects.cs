using System.Collections.Generic;
using UnityEngine;
using Enums;

public class Effects : MonoBehaviour {
    public class CardEffect {
        public EffectType effectType;
        public EffectTrigger trigger;
        public List<EffectAction> actions = new List<EffectAction>();
    }

    public class EffectAction {
        public ActionType actionType;
        public int value;
        public TargetType targetType;
        public TargetModifier targetModifier = TargetModifier.None; // Target modifier for spread damage effects

        // --- CHANGED: Use Modifier ID string ---
        public string modifierIdToApply;
    }
}
