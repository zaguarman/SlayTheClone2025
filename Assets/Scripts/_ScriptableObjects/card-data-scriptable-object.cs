using UnityEngine;
using System.Collections.Generic;
using static Enums;

// Base card scriptable object
public abstract class CardDataScriptableObject : ScriptableObject {
    public string cardName;
    public string description;
    public CardType cardType;
    [SerializeField] protected List<CardEffect> effects = new List<CardEffect>();

    // Method to convert this scriptable object to a runtime CardData
    public abstract CardData ToCardData();

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
                    targetType = action.targetType
                });
            }

            effectsCopy.Add(effectCopy);
        }

        return effectsCopy;
    }
}

// Creature card scriptable object
[CreateAssetMenu(fileName = "NewCreature", menuName = "Cards/Creature")]
public class CreatureCardScriptableObject : CardDataScriptableObject {
    public int attack;
    public int health;

    private void OnEnable() {
        cardType = CardType.Creature;
    }

    public override CardData ToCardData() {
        CreatureData data = ScriptableObject.CreateInstance<CreatureData>();
        data.cardName = cardName;
        data.description = description;
        data.cardType = cardType;
        data.attack = attack;
        data.health = health;
        data.effects = CopyEffects();
        return data;
    }
}

// Spell card scriptable object
[CreateAssetMenu(fileName = "NewSpell", menuName = "Cards/Spell")]
public class SpellCardScriptableObject : CardDataScriptableObject {
    public TargetType defaultTargetType;

    private void OnEnable() {
        cardType = CardType.Spell;
    }

    public override CardData ToCardData() {
        SpellData data = ScriptableObject.CreateInstance<SpellData>();
        data.cardName = cardName;
        data.description = description;
        data.cardType = cardType;
        data.defaultTargetType = defaultTargetType;
        data.effects = CopyEffects();
        return data;
    }
}
