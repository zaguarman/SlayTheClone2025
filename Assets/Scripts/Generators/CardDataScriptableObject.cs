using UnityEngine;
using System.Collections.Generic;
using static Enums;

// Base card scriptable object
public abstract class CardDataScriptableObject : ScriptableObject {
    public string cardId; // Added cardId field
    public string cardName;
    public string description;
    public CardType cardType;
    public Sprite artwork; // Shown in the card's art box; assigned by Tools > Cards > Assign Card Art
    [SerializeField] protected List<CardEffect> effects = new List<CardEffect>();

    // Initialize cardId if it's empty
    protected virtual void OnEnable() {
        if (string.IsNullOrEmpty(cardId)) {
            cardId = System.Guid.NewGuid().ToString();
        }
    }

    // Method to convert this scriptable object to a runtime CardData
    public abstract CardData ToCardData();

    // Copies the fields shared by every card type onto its runtime data
    protected void CopyCommonFieldsTo(CardData data) {
        data.cardId = cardId;
        data.cardName = cardName;
        data.description = description;
        data.cardType = cardType;
        data.artwork = artwork;
        data.effects = CopyEffects();
    }

    // Helper method to get a copy of the effects list
    protected List<CardEffect> CopyEffects() {
        List<CardEffect> effectsCopy = new List<CardEffect>();
        foreach (var effect in effects) {
            var effectCopy = new CardEffect {
                effectType = effect.effectType,
                trigger = effect.trigger,
                actions = new List<EffectAction>()
            };
            foreach (var action in effect.actions) {
                effectCopy.actions.Add(new EffectAction {
                    actionType = action.actionType,
                    value = action.value,
                    targetType = action.targetType,
                    targetModifier = action.targetModifier,
                    modifyAttack = action.modifyAttack,
                    modifyHealth = action.modifyHealth,
                    modifySpeed = action.modifySpeed,
                    statusEffectToApply = action.statusEffectToApply,
                    statusDuration = action.statusDuration,
                    statusPotency = action.statusPotency
                });
            }
            effectsCopy.Add(effectCopy);
        }
        return effectsCopy;
    }
}