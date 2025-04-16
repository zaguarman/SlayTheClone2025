using UnityEngine;
using System.Collections.Generic;
using Enums;

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
    public ActionType actionType; // Use Enums.ActionType
    public int value;
    public TargetType targetType; // Use Enums.TargetType
    public TargetModifier targetModifier = TargetModifier.None; // Use Enums.TargetModifier

    // --- Option 1: Keep Buff flags for compatibility ---
    [Tooltip("For Buff action type (Legacy): Buff attack?")]
    public bool buffAttack = true;
    [Tooltip("For Buff action type (Legacy): Buff health?")]
    public bool buffHealth = true;

    // --- Option 2: Use ModifierData reference for ApplyModifier action ---
    [Tooltip("Assign ModifierData asset here if actionType is ApplyModifier")]
    public ModifierData modifierToApply;

    // --- Option 3: Add ID for RemoveModifier action ---
    // [Tooltip("Enter Modifier ID here if actionType is RemoveModifier")]
    // public string modifierIdToRemove;
}