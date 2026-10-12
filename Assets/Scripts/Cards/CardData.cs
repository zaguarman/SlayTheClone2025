using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public abstract class CardData : ScriptableObject {
        public string cardId; // Unique per card definition
        public string cardName;
        public string description;
        public CardType cardType;
        public Faction faction;
        public Sprite artwork;
        public int priority; // Printed Priority (Action): a spell's Spell Action, a creature's attacks (GDD 3.7)
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
        public int turnsUntilOperative; // Turns it spends on the board before it can act (0 = from the next turn)
        public MovementKeyword movement; // Leapfrog, Swap (GDD 2.4 Movement Keywords)

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

        // Deep copy, so each card instance can own its effects without sharing them with its definition
        public CardEffect Clone() {
            return new CardEffect {
                effectType = effectType,
                trigger = trigger,
                actions = actions.Select(action => action.Clone()).ToList()
            };
        }
    }

    [System.Serializable]
    public class EffectAction { // Add new fields here too
        public ActionType actionType;
        public int value;
        public TargetType targetType;
        public TargetModifier targetModifier = TargetModifier.None;

        // ModifyStat Specific
        public bool modifyAttack = true;
        public bool modifyHealth = true;
        public bool modifySpeed = false;
        // public ModifierCalculationType calculationType = ModifierCalculationType.Flat;

        // ApplyStatus Specific
        public StatusEffectType statusEffectToApply = StatusEffectType.None;
        public int statusDuration = 0;
        public int statusPotency = 0;

        public EffectAction Clone() => (EffectAction)MemberwiseClone(); // Value-type fields only
    }
}
