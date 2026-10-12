namespace SlayTheClone {
    using System; // Add this for Flags attribute

    public class Enums {
        // Card/Effect Enums
        public enum CardType { Creature, Spell, Enchantment }
        // The nine factions of the 'Faction Compendium'; None for cards made before factions existed
        public enum Faction { None, Myth, Statecraft, ShadowOps, Corporate, Tech, DigitalSociety, Zeitgeist, Mind, Utopia }
        public enum EffectType { Immediate, Triggered, Continuous, Timed, Status }
        public enum EffectTrigger { OnPlay, OnDeath, OnDamage, StartOfTurn, EndOfTurn, ActionAttempted, OnAboutToDie } // New triggers go last: cards store them as numbers
        public enum ActionType { Damage, Heal, Draw, Summon, Armor, Stun, ModifyStat, ApplyStatus, ModifyPriority, Cleanse, PlaceSlotEffect, CleanseSlot, CreateWatchdog } // Renamed Buff to ModifyStat
        public enum TargetType { Player, Enemy, AllCreatures, FriendlyCreatures, EnemyCreatures, Self }

        // Movement Keywords printed on creatures (GDD 2.4): Leapfrog jumps over creatures, Swap trades places with a friendly one
        [Flags]
        public enum MovementKeyword { None = 0, Leapfrog = 1 << 0, Swap = 1 << 1 }

        // A creature's one ability ('Abilities Compendium'). New abilities go last: cards store them as numbers
        public enum CreatureAbility {
            None, Contrarian, Extremist, Unbothered, NoU, Spite, Underground, TooMainstream, ZeroDay, Unpatchable,
            SignalJammer, Copypasta, IdentityTheft, HiveMind, InsiderJob, ReturnToSender, Technician
        }

        // Watchdogs (GDD 3.11): what they watch, whose, what they do, and when they look
        public enum WatchedAction { Attack, Spell, Move, Ability, Summon, Status }
        public enum WatchdogSide { Enemy, Friendly, Any }
        public enum WatchdogEffect { Nullify, Redirect, Reorder, Reveal }
        public enum WatchdogTiming { OnWatch, Briefing }
        public enum BriefingPick { First, Strongest, Every }

        // What a planned move does (see MovementRules.KindOf)
        public enum MoveKind { Walk, Push, Leapfrog, Swap }

        public enum StatusEffectType {
            None,
            Burned,     // Takes damage at end of turn
            Exhausted,  // The stun: Cannot Act, abilities disabled, and Exposed (+1 damage taken) while it lasts (Status Effects Compendium)
            Doomed,     // Death Sentence: blocks other statuses, +1 permanent Attack each Dawn, then Last Rites and destroyed
            Blessed     // Persistent ward: the next status that would land is negated and Blessed loses a stack (up to 3)
        }

        // Conditions on battlefield slots ('Slot Effects Compendium'). One per slot; a new one replaces the old
        public enum SlotEffectType {
            None,
            Landmine,       // Hidden. The first creature to enter takes X damage, then it's consumed
            BarbedSnare,    // Hidden. The first creature to leave takes X damage, then it's consumed
            TemporalShield, // Visible. The creature here is immune to combat damage, for X Days
            ScorchedEarth   // Visible. Passive abilities of the creature here don't trigger
        }

        // What a Cleanse removes (GDD Cleanse - Specificity): statuses and stat modifications are independent
        public enum CleanseTarget { Statuses, StatModifications, Both }

        // Targeting Modifiers
        [Flags] // Make sure System is imported
        public enum TargetModifier {
            None = 0,
            WithAdjacent = 1 << 0,
            AllSameTypeTargets = 1 << 1,
            Random = 1 << 2,
            Chained = 1 << 3,
        }

        // Weather Types
        public enum WeatherType {
            Clear,
            Rainy,
            Sunny
        }
    }
}
