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
        /// <summary>Additional cost: this many other cards from the hand, at random, are given up to play it (0: none).</summary>
        public int discardCost;
        /// <summary>The discard cost is picked by hand (a calculated play) rather than at random (a chaotic one).</summary>
        public bool discardChosen;
        /// <summary>Single Use: once played, it's gone for the rest of the combat instead of going to the discard pile.</summary>
        public bool singleUse;
        /// <summary>Sacrifice N (Myth's cost): its side's N weakest creatures die when it resolves, before it does anything.</summary>
        public int sacrifice;
        /// <summary>Doom N (a cost): its side's weakest creature that isn't Doomed yet is Doomed N Days when it resolves.</summary>
        public int doomCost;
        /// <summary>Prophecy N (a spell): it comes true at the Dawn N Days after it's played.</summary>
        public int prophecy;
        /// <summary>Prophecy N-M: when above <see cref="prophecy"/>, it comes true a random number of Days in that range (Margin Call).</summary>
        public int prophecyLatest;
        /// <summary>Gamble (a spell): only one of its effects happens, picked at random when it resolves.</summary>
        public bool gamble;

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
        /// <summary>A token: made by an effect, never a card in a deck. When it leaves play, it's gone.</summary>
        public bool token;
        /// <summary>What the other side sees it as, until it's revealed (a decoy). Null: itself.</summary>
        public CreatureData disguise;
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

        // SendFor Specific: the kind of card it fetches (value: only from the top this many cards of the deck, 0 = all of it)
        public SendForFilter sendFor = SendForFilter.Any;
        public bool fromDiscard;    // SendFor: from the discard pile instead of the deck (Console Memory Card: load your save)

        // What the target must be for it to land (Destroy: "if it's Revealed")
        public TargetRequisite requires = TargetRequisite.None;

        // SummonDecoys Specific: the token it summons (value is how many). The asset is kept for the card text; the runtime
        // copy is made with the card's data (see CardDataScriptableObject), so games on other threads can create tokens
        public CreatureCardScriptableObject decoy;
        [System.NonSerialized] public CreatureData decoyData;

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
        public bool damaging;       // Only the ones that deal damage (Yesn't: damaging spells)
    }
}
