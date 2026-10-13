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
        /// <summary>Gift: it's played into an empty enemy slot and joins their side; still your card, it comes back to your discard pile.</summary>
        public bool gift;
        public int speed = 1; // Default speed to 1
        public int turnsUntilOperative; // Turns it spends on the board before it can act (0 = from the next turn)
        public MovementKeyword movement; // Leapfrog, Swap (GDD 2.4 Movement Keywords)
        public CreatureAbility ability; // Its one ability ('Abilities Compendium')

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
        public bool oncePerCombat; // It can only happen once per combat for this card (GDD: effects that save a creature must be limited)
        public List<EffectAction> actions = new List<EffectAction>();

        // Deep copy, so each card instance can own its effects without sharing them with its definition
        public CardEffect Clone() {
            return new CardEffect {
                effectType = effectType,
                trigger = trigger,
                oncePerCombat = oncePerCombat,
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
        public int statusPotency = 0; // Burn's damage; Blessed's stacks

        // Cleanse Specific: what it removes, and whether it's a Purge (good and bad) instead of a Cleanse (only the bad)
        public CleanseTarget cleanse = CleanseTarget.Statuses;
        public bool purge;

        // PlaceSlotEffect Specific: value is its damage (Landmine, Barbed Snare), statusDuration its Days (0 = until replaced)
        public SlotEffectType slotEffect = SlotEffectType.None;

        // CreateWatchdog Specific: statusDuration is its Days (0 = until its charges are used)
        public WatchdogSpec watchdog;

        // SetTrend Specific: the Trend it makes trending; statusDuration is its Days (0 = until another Trend replaces it)
        [UnityEngine.Serialization.FormerlySerializedAs("world")] public Trend trend = Trend.None;

        // GrantAbility Specific: the ability the target gains, replacing its own until its card is reshuffled
        public CreatureAbility ability = CreatureAbility.None;

        // What the target must be for it to land (Destroy: "if it's Revealed")
        public TargetRequisite requires = TargetRequisite.None;

        public EffectAction Clone() => (EffectAction)MemberwiseClone(); // Value-type fields only
    }

    /// <summary>"Watchdog: [condition] -> [effect], N charges (timing)" (GDD 3.11). A struct, so cloning an effect copies it.</summary>
    [System.Serializable]
    public struct WatchdogSpec {
        public WatchedAction watches;
        public WatchdogSide side;
        public int atMost;          // "Attack 3 or less"; 0 = any
        public WatchdogEffect effect;
        public int charges;         // 0 counts as 1
        public WatchdogTiming timing;
        public BriefingPick pick;   // Briefing only
        public bool moveEarlier;    // Briefing Reorder only: earlier instead of later
    }
}
