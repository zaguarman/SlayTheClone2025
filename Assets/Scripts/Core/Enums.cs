namespace SlayTheClone {
    using System; // Add this for Flags attribute

    public class Enums {
        // Card/Effect Enums
        public enum CardType { Creature, Spell, Enchantment }
        // The nine factions of the 'Faction Compendium'; None for cards made before factions existed
        public enum Faction { None, Myth, Statecraft, ShadowOps, Corporate, Tech, DigitalSociety, Zeitgeist, Mind, Utopia }
        public enum EffectType { Immediate, Triggered, Continuous, Timed, Status }
        public enum EffectTrigger { OnPlay, OnDeath, OnDamage, StartOfTurn, EndOfTurn, ActionAttempted, OnAboutToDie } // New triggers go last: cards store them as numbers
        public enum ActionType { Damage, Heal, Draw, Summon, Armor, Stun, ModifyStat, ApplyStatus } // Renamed Buff to ModifyStat
        public enum TargetType { Player, Enemy, AllCreatures, FriendlyCreatures, EnemyCreatures, Self }

        public enum StatusEffectType {
            None,
            Burned,     // Takes damage at end of turn
            Paralyzed   // Cannot perform actions (attack/move)
        }

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
