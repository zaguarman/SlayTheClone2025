using System;
using System.Collections.Generic;
using UnityEngine;

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
    }
}