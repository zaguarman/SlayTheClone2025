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
        public bool buffAttack = true; // For Buff action type: whether to buff attack
        public bool buffHealth = true; // For Buff action type: whether to buff health
    }
}
