using UnityEngine;
using System.Collections.Generic;
using Enums;

// Base card scriptable object
public abstract class CardDataScriptableObject : ScriptableObject {
    public string cardId; // Added cardId field
    public string cardName;
    public string description;
    public CardType cardType;
    [SerializeField] protected List<CardEffect> effects = new List<CardEffect>();

    // Initialize cardId if it's empty
    protected virtual void OnEnable() {
        if (string.IsNullOrEmpty(cardId)) {
            cardId = System.Guid.NewGuid().ToString();
        }
    }

    // Method to convert this scriptable object to a runtime CardData
    public abstract CardData ToCardData();

    // Helper method to get a copy of the effects list
    protected List<CardEffect> CopyEffects() {
        List<CardEffect> effectsCopy = new List<CardEffect>();
        foreach (var effect in effects) { // Iterate through the serialized effects
            var effectCopy = new CardEffect { // Create runtime CardEffect
                effectType = effect.effectType,
                trigger = effect.trigger,
                actions = new List<EffectAction>()
            };
            foreach (var action in effect.actions) { // Iterate through serialized actions
                // Create runtime EffectAction and copy all fields, INCLUDING the modifier reference
                effectCopy.actions.Add(new EffectAction {
                    actionType = action.actionType,
                    value = action.value,
                    targetType = action.targetType,
                    targetModifier = action.targetModifier,
                    modifierToApply = action.modifierToApply // <<< COPY THE REFERENCE
                });
            }
            effectsCopy.Add(effectCopy);
        }
        return effectsCopy;
    }
}