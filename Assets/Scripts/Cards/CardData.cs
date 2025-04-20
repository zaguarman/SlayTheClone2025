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
    public int speed = 1; // Default speed to 1

    public override void OnEnable() {
        base.OnEnable();
        cardType = CardType.Creature;
        if (speed <= 0) speed = 1; // Ensure speed is at least 1 on enable
    }
}

[System.Serializable]
public class CardEffect {
    public EffectType effectType;
    public EffectTrigger trigger;
    public List<EffectAction> actions = new List<EffectAction>();
}

[System.Serializable]
public class EffectAction { // Add new fields here too
    public ActionType actionType;
    public int value;
    public TargetType targetType;
    public TargetModifier targetModifier = TargetModifier.None;

    // Buff Specific
    public bool buffAttack = true;
    public bool buffHealth = true;
    public bool buffSpeed = false; // Added flag for speed buffing
    // public ModifierCalculationType calculationType = ModifierCalculationType.Flat;

    // ApplyStatus Specific
    public StatusEffectType statusEffectToApply = StatusEffectType.None;
    public int statusDuration = 0;
    public int statusPotency = 0;
}