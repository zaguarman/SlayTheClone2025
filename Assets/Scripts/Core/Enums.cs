namespace Zeitgeist {
    using System; // Add this for Flags attribute

    public class Enums {
        // Card/Effect Enums
        public enum CardType { Creature, Spell, Enchantment }
        // The nine factions of the 'Faction Compendium'; None for cards made before factions existed
        // The subfactions (the user, 2026-10-17: "as Slay the Spire's Ironclad is three archetypes at once", four factions hold
        // these as their archetypes; see Umbrella and Umbrellas). In the game's words these are subfactions and the Umbrella is
        // the faction; the code keeps the old name for them. Online (6) was Digital Society and Offline was Zeitgeist (a
        // name the game took); PopCulture is a dead value, the merge's name for a day, kept so Mind and Utopia keep their numbers
        public enum Faction { None, Myth, Statecraft, ShadowOps, Corporate, Tech, Online, PopCulture, Mind, Utopia, Offline }

        /// <summary>The factions as the player picks them (the user, 2026-10-17): each holds two or three subfactions, mixed freely in a deck.</summary>
        public enum Umbrella { None, Establishment, Capital, Enlightenment, DigitalSociety }
        public enum EffectType { Immediate, Triggered, Continuous, Timed, Status }
        public enum EffectTrigger { OnPlay, OnDeath, OnDamage, StartOfTurn, EndOfTurn, ActionAttempted, OnAboutToDie, OnRevealed, OnReplaced,
            OnDiscarded } // OnDiscarded: Laid Off, given up from the hand (the Dawn discard, a discard cost) // New triggers go last: cards store them as numbers
        public enum ActionType { Damage, Heal, Draw, Summon, Armor, Stun, ModifyStat, ApplyStatus, Cleanse, PlaceSlotEffect, CleanseSlot, CreateWatchdog, DelayDeployment, Reveal, SetTrend,
            CopyStats, StealStatChanges, SwapPositions, GrantAbility, Destroy,
            TakeControl, Attack, Funnel, PassHarmfulStatuses, StealGoodStatuses, ReturnToHand, FakeDeath, Vanish, SummonDecoys,
            WipeWatchdogs, CutDraws, Revive, SendFor, Earthquake, Resurrect, Grace, Consolidate, CopyStatuses, EndDay, Rush, Defect, SwapStats,
            SwapSlotEffects, Equip,
            Push, Pull, Shuffle, Spin,
            Mimic, TradeStats, Seal,
            DoubleDown, SplitPain, SwapAbilities } // 3rd Parallel Job, Parabiosis, Job Rotation (2026-10-17). // Mimic (Monkey See, Monkey Do), TradeStats (Freaky Friday), Seal (the DMCA): the Pokémon picks, 2026-10-17. Renamed Buff to ModifyStat. New ones go last: cards store them as numbers. Push/Pull/Shuffle/Spin: moves

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
            AllergicToLight, FallGuy, Brink, CollateralDamage, SorryImLate, HeProtec, Irreplaceable, Turncoat,
            // The meta picks (2026-10-16): statuses that help their owner, losses as fuel, priority, pivots, Armor as damage, strips, Trends
            Hormesis, Martyrdom, Grudge, SuckerPunch, HitAndRun, ShieldBash, StripSearch, OnTrend,
            // Gear only (held items, 2026-10-16): Hover keeps it off Slot Effects, Self-Repair heals it each Dawn
            Hover, SelfRepair,
            // The Pokémon picks (the user, 2026-10-17): Moody, a dodge (the politician's), Metronome, Wonder Guard
            MoodSwings, NextQuestion, WildCard, Immaculate,
            // The second batch (the user, 2026-10-17): Rocky Helmet, Protect, Rapid Spin, Sleep Talk, Assault Vest, the Choice items, Shadow Tag,
            // Synchronize, Berserk, Dancer, Neutralizing Gas, Perish Body, Good as Gold, Toxic Debris, and healing that hurts
            Litigious, HunkerDown, BombDisposal, Autopilot, RiotGear, Fixated, Clingy, Empathy, Berserk, BackupDancer, JammingField,
            PharaohsCurse, Teflon, Shrapnel, DoYourOwnResearch
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
            Rooted,        // It can't move, and nothing can move or swap it (a building going up: Ministry for Equality)
            Burnout,       // When it runs out, the creature is destroyed (Overtime: it lasts through the first Day it's ready)
            Shielded,      // Takes no combat damage, for its Days counting the one it lands (the user: a creature's, not a slot's)
            Toxic,         // Merged into Burned (the user, 2026-10-17): no card uses it; kept so the numbers after it stay put
            Provoked,       // Taunt and Follow Me: its attacks go to the creature facing it, and it can't use its Active ability
            Looping,       // Groundhog Day (Encore): it repeats the plan it had when the loop closed, each Day, and can plan nothing else
            Stalled,       // Filibuster (Quash): it acts last in its layer this Day
            Deniable,      // Plausible Deniability (Wide Guard): effects that hit more than one creature skip it
            Leeched,       // Tithe (Leech Seed): each Dusk it takes X and the enemy player heals X
            MutuallyAssured // Destiny Bond (Mutually Assured Destruction, Insurance Policy, Hostage Situation): the creature that kills it dies too
        }

        // Conditions on battlefield slots ('Slot Effects Compendium'). One per slot; a new one replaces the old
        public enum SlotEffectType {
            None,
            Landmine,       // Hidden. The first creature to enter takes X damage, then it's consumed
            BarbedSnare,    // Hidden. The first creature to leave takes X damage, then it's consumed
            Shield, // Visible. The creature here is immune to combat damage, for X Days
            ScorchedEarth,  // Visible. The creature here can't trigger its effects, entry effects included (its ability still works)
            Checkpoint,      // Visible. Every creature that enters takes X damage; it stays (a side hazard: placed on a whole side)
            Prayer          // Visible. When its Days run out, the creature standing here heals X (Thoughts and Prayers, Pokémon\'s Wish)
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
            Casino,        // Each Dawn the house's wheel spins the Day's weather, the same for both sides (see CasinoRoll)
            LiminalSpace  // Every creature\'s Attack and Health trade places while it trends, and back when it ends (Mind, 2026-10-17)
        }

        // Casino: the house's weather for the Day, like a Pokémon battle's weather, the same for everyone
        public enum CasinoRoll { None, Heatwave, Rain, Hail, Tailwind, RushHour, Windfall }
    }
}
