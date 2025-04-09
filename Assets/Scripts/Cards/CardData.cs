using UnityEngine;
using System.Collections.Generic;
using static Enums;

public abstract class CardData : ScriptableObject {
    public string cardId; // Added unique ID field
    public string cardName;
    public string description;
    public CardType cardType;
    public List<CardEffect> effects = new List<CardEffect>();

    // OnEnable can be used to initialize cardId if null
    public virtual void OnEnable() {
        if (string.IsNullOrEmpty(cardId)) {
            cardId = System.Guid.NewGuid().ToString();
        }
    }
}

public class CreatureData : CardData {
    public int attack;
    public int health;

    public override void OnEnable() {
        base.OnEnable();
        cardType = CardType.Creature;
    }
}

[System.Serializable]
public class CardEffect {
    public EffectType effectType;
    public EffectTrigger trigger;
    public List<EffectAction> actions = new List<EffectAction>();
}

[System.Serializable]
public class EffectAction {
    public ActionType actionType;
    public int value;
    public TargetType targetType;
    public bool buffAttack = true; // For Buff action type: whether to buff attack
    public bool buffHealth = true; // For Buff action type: whether to buff health
}