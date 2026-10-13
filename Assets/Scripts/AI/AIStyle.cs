using System.Collections.Generic;

namespace SlayTheClone {
    /// <summary>
    /// A way to play for the Hard AI: how much it cares for each thing a card or an attack can do, against the others. The
    /// same planner with other weights goes face, controls the board, goes wide, hexes, turtles, spies or draws. 1 is the
    /// Balanced Hard AI; 2 counts it double, 0.5 half.
    /// </summary>
    public sealed class AIStyle {
        public string Name { get; private set; }
        public string Description { get; private set; }

        /// <summary>Damage to the other player.</summary>
        public float Face { get; private set; } = 1;
        /// <summary>Our own Health: under 1, it's a resource to spend (self-damage for cards or power).</summary>
        public float OwnHealth { get; private set; } = 1;
        /// <summary>Damaging and killing their creatures.</summary>
        public float Removal { get; private set; } = 1;
        /// <summary>Our creatures: summons, buffs, and losing them.</summary>
        public float Board { get; private set; } = 1;
        /// <summary>Statuses and debuffs on their creatures, stuns, Red Tape, traps.</summary>
        public float Statuses { get; private set; } = 1;
        /// <summary>Drawing cards.</summary>
        public float Cards { get; private set; } = 1;
        /// <summary>Information: Reveals, Watchdogs, Concealed and Encrypted.</summary>
        public float Intel { get; private set; } = 1;
        /// <summary>Keeping ours alive: Armor, healing, Bless, Untouchable, Temporal Shields, Cleanse.</summary>
        public float Protection { get; private set; } = 1;
        /// <summary>How eager it is to race: over 1 it goes face sooner, under 1 it holds the board longer.</summary>
        public float Aggression { get; private set; } = 1;

        public override string ToString() => Name;

        public static readonly AIStyle Balanced = new AIStyle {
            Name = "Balanced", Description = "Weighs everything the same: the plain Hard AI."
        };
        public static readonly AIStyle Rusher = new AIStyle {
            Name = "Rusher", Description = "Aggro: damage to the player first, its own Health spent freely, races early.",
            Face = 1.5f, OwnHealth = 0.8f, Removal = 0.9f, Protection = 0.8f, Aggression = 1.5f
        };
        public static readonly AIStyle Controller = new AIStyle {
            Name = "Controller", Description = "Control: kills and disables their creatures, goes face only once the board is safe.",
            Removal = 1.6f, Statuses = 1.3f, Face = 0.7f, Aggression = 0.6f
        };
        public static readonly AIStyle Swarm = new AIStyle {
            Name = "Swarm", Description = "Goes wide: summons, buffs for its creatures and cards to keep the row full.",
            Board = 1.6f, Cards = 1.2f, Removal = 0.8f, Aggression = 1.2f
        };
        public static readonly AIStyle Hexer = new AIStyle {
            Name = "Hexer", Description = "Statuses and debuffs: Burn, stuns, Doomed, Heat, then the hits that use them.",
            Statuses = 2f, Removal = 1.2f, Face = 0.9f
        };
        public static readonly AIStyle Bastion = new AIStyle {
            Name = "Bastion", Description = "Defence: protects its creatures and its Health, wins late.",
            Protection = 2f, OwnHealth = 2f, Board = 1.3f, Face = 0.8f, Aggression = 0.7f
        };
        public static readonly AIStyle Spymaster = new AIStyle {
            Name = "Spymaster", Description = "Intelligence: Reveals, Watchdogs, Concealed and Encrypted, and cards.",
            Intel = 3f, Cards = 1.3f, Statuses = 1.2f
        };
        public static readonly AIStyle Engine = new AIStyle {
            Name = "Engine", Description = "Card advantage: draws whenever it can, paying with Health and creatures.",
            Cards = 2f, OwnHealth = 0.5f, Board = 1.2f
        };

        public static readonly IReadOnlyList<AIStyle> All = new[] { Balanced, Rusher, Controller, Swarm, Hexer, Bastion, Spymaster, Engine };
    }
}
