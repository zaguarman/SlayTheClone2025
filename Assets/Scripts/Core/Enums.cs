namespace SlayTheClone {
    using System; // Add this for Flags attribute

    public class Enums {
        // Card/Effect Enums
        public enum CardType { Creature, Spell, Enchantment }
        // The nine factions of the 'Faction Compendium'; None for cards made before factions existed
        public enum Faction { None, Myth, Statecraft, ShadowOps, Corporate, Tech, DigitalSociety, Zeitgeist, Mind, Utopia }
        public enum EffectType { Immediate, Triggered, Continuous, Timed, Status }
        public enum EffectTrigger { OnPlay, OnDeath, OnDamage, StartOfTurn, EndOfTurn, ActionAttempted, OnAboutToDie, OnRevealed, OnReplaced } // New triggers go last: cards store them as numbers
        public enum ActionType { Damage, Heal, Draw, Summon, Armor, Stun, ModifyStat, ApplyStatus, Cleanse, PlaceSlotEffect, CleanseSlot, CreateWatchdog, DelayDeployment, Reveal, SetTrend,
            CopyStats, StealStatChanges, SwapPositions, GrantAbility, Destroy,
            TakeControl, Attack, Funnel, PassHarmfulStatuses, StealGoodStatuses, ReturnToHand, FakeDeath, Vanish, SummonDecoys,
            WipeWatchdogs, CutDraws, Revive, SendFor, Earthquake, Resurrect, Grace, Consolidate, CopyStatuses, EndDay } // Renamed Buff to ModifyStat. New ones go last: cards store them as numbers

        /// <summary>
        /// What a creature must be for an effect to land on it (checked when it lands), e.g. only a Revealed one is Destroyed.
        /// NextToEmptySlot: a slot beside it is empty. Outnumbered: its side has fewer creatures than the other. AttackAtMost:
        /// its Attack is the effect's value or less. Deploying: it isn't ready yet (Deployment Time). Online: it's a Digital
        /// Society card (Cyberpsychosis hits the terminally online harder). New ones go last
        /// </summary>
        public enum TargetRequisite { None, Revealed, NextToEmptySlot, Outnumbered, AttackAtMost, Deploying, Online, OutsideHeadquarters }
        public enum TargetType { Player, Enemy, AllCreatures, FriendlyCreatures, EnemyCreatures, Self }

        // Send For: the kind of card it can fetch from the deck
        public enum SendForFilter { Any, Creature, Spell }

        // Movement Keywords printed on creatures (GDD 2.4): Leapfrog jumps over creatures, Swap trades places with a friendly one
        [Flags]
        public enum MovementKeyword { None = 0, Leapfrog = 1 << 0, Swap = 1 << 1 }

        // A creature's one ability ('Abilities Compendium'). New abilities go last: cards store them as numbers
        public enum CreatureAbility {
            None, Contrarian, Extremist, Unbothered, NoU, Spite, Underground, TooMainstream, ZeroDay, Unpatchable,
            SignalJammer, Copypasta, IdentityTheft, HiveMind, InsiderJob, ReturnToSender, PennyStock, Undercover, Handover, Recoil,
            Bodyguard, Wannabe, CreditGrab, SprayAndPray, ChainMail, DoubleTap, ShellGame, SafeSpace, Ambush, ExtremePrejudice,
            AllergicToLight, FallGuy, Brink, CollateralDamage, SorryImLate, HeProtec, Irreplaceable
        }

        // Watchdogs (GDD 3.11): what they watch, whose, what they do, and when they look
        public enum WatchedAction { Attack, Spell, Move, Ability, Summon, Status, Intelligence } // Intelligence: a Reveal, or Revealed landing
        public enum WatchdogSide { Enemy, Friendly, Any }
        public enum WatchdogEffect { Nullify, Redirect, Reorder, Reveal, Backfire } // Backfire: nullified, and the attacker takes its own Attack
        public enum WatchdogTiming { OnWatch, Briefing }
        public enum BriefingPick { First, Strongest, Every }

        // What a planned move does (see MovementRules.KindOf)
        // Swap: with the friendly creature one step away (melee range), or at any distance with the Swap keyword. Push was
        // archived (the user, 2026-10-16): Documentation/Archived/Push.txt
        public enum MoveKind { Walk, Leapfrog, Swap }

        public enum StatusEffectType {
            None,
            Burned,     // Takes damage at end of turn
            Exhausted,  // The stun: Cannot Act, abilities disabled, and Exposed (+1 damage taken) while it lasts (Status Effects Compendium)
            Doomed,     // Death Sentence: blocks other statuses, +1 permanent Attack each Dawn, then Last Rites and destroyed
            Blessed,    // Persistent ward: the next status that would land is negated and Blessed loses a stack (up to 3)
            HeatSignature, // Persistent Heat stacks: random and redirected attacks go to the creature with the most Heat
            Concealed,     // Its planned actions are Concealed Actions: '???' to the other side, and Watchdogs can't match them
            Revealed,      // Cancels Concealed and Undercover: its planned actions can be seen
            Encrypted,     // Watchdogs ignore its actions (no charge is spent)
            Untouchable,   // Takes no damage, for its Days counting the one it lands (Bodyguard, Presidential Pardon)
            Cooldown,      // Its Active ability rests: it can't be used while this lasts
            Brainwashed,   // When it runs out, the creature changes sides, for good, with its ability lost
            KillOrder,     // Hidden from its creature's side: when it runs out, the creature is destroyed
            BorrowedTime,  // It can't die while it lasts; when it runs out, it dies if it's at 0 Health
            Licensed,      // Its next attack on a creature destroys it, then the licence is spent (lasts until used)
            Composed,      // Exhausted can't land on it (this Day only, like Untouchable)
            Blackout,      // Its ability and effects are switched off
            Survivor,      // From full Health, a hit that would kill it leaves it at 1, then it's spent (lasts until used)
            Rooted         // It can't move, and nothing can move or swap it (a building going up: Ministry for Equality)
        }

        // Conditions on battlefield slots ('Slot Effects Compendium'). One per slot; a new one replaces the old
        public enum SlotEffectType {
            None,
            Landmine,       // Hidden. The first creature to enter takes X damage, then it's consumed
            BarbedSnare,    // Hidden. The first creature to leave takes X damage, then it's consumed
            Shield, // Visible. The creature here is immune to combat damage, for X Days
            ScorchedEarth   // Visible. The creature here can't trigger its effects, entry effects included (its ability still works)
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
            Strongest = 1 << 4, // The one with the highest Attack, picked when it happens (ties at random)
            Opposite = 1 << 5,  // A creature's effect: the creature facing it, on the other side (none there: nothing)
            Beside = 1 << 6,    // Only the creatures beside where it lands, not the one there (Holy Hand Grenade's splash)
            AlliesOfTarget = 1 << 7, // The other creatures on the side of the one it lands on (False Flag Operation rallies them)
            DroppedSide = 1 << 8,    // The creatures of the side it's played on, either one (Team Building Exercise)
            Others = 1 << 9,         // A creature's effect on a group, or a pick from one: every creature but itself (Demagogue)
        }

        // Trends (GDD 4.2): what the world is into right now, one rule for the whole board for some Days, until another Trend replaces it. New ones go last
        public enum Trend {
            None,
            Zeitgeist, // Each Day, whoever has fewer creatures draws 1 more card
            RushHour,  // The slowest act first
            SurvivalOfTheFittest, // Each Dusk, the creature with the least Health on the board, either side, takes 3 damage
            VolcanoLair,  // Each Day, whoever set it keeps their strongest creature Untouchable while another of theirs stands
            Casino        // Each Dawn the house's wheel spins the Day's weather, the same for both sides (see CasinoRoll)
        }

        // Casino: the house's weather for the Day, like a Pokémon battle's weather, the same for everyone
        public enum CasinoRoll { None, Heatwave, Rain, Hail, Tailwind, RushHour, Windfall }
    }
}
