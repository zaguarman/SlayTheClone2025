using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// A condition on a battlefield slot ('Slot Effects Compendium'). It isn't a status: it stays with the slot, not the
    /// creature. Whoever applied it controls it. Hidden ones are secret from the other side until they trigger.
    /// </summary>
    public class SlotEffect {
        public SlotEffectType Type { get; }
        public IPlayer Controller { get; }
        public int Value { get; }       // Damage for Landmine and Barbed Snare
        public int Days { get; }        // How long it lasts, counting the Day it's placed; 0 means until replaced or used up
        public int TurnApplied { get; }

        public SlotEffect(SlotEffectType type, IPlayer controller, int value, int days, int turnApplied) {
            Type = type;
            Controller = controller;
            Value = value;
            Days = days;
            TurnApplied = turnApplied;
        }

        /// <summary>Only its controller knows it's there, until it triggers or is revealed.</summary>
        public bool IsHidden => Type is SlotEffectType.Landmine or SlotEffectType.BarbedSnare;

        /// <summary>Players who saw a hidden one through Intelligence (a Reveal), besides its controller.</summary>
        public System.Collections.Generic.HashSet<IPlayer> RevealedTo { get; } = new System.Collections.Generic.HashSet<IPlayer>();

        /// <summary>"1 Trigger": removed as soon as its condition is met, so it can't trigger twice.</summary>
        public bool IsOneTrigger => Type is SlotEffectType.Landmine or SlotEffectType.BarbedSnare;

        /// <summary>Checked with the Day about to start: lasting 1 Day, it ends at the Dusk of the Day it was placed.</summary>
        public bool HasExpired(int nextTurn) => Days > 0 && nextTurn >= TurnApplied + Days;

        public string Name => Type switch {
            SlotEffectType.BarbedSnare => "Barbed Snare",
            SlotEffectType.TemporalShield => "Temporal Shield",
            SlotEffectType.ScorchedEarth => "Scorched Earth",
            _ => Type.ToString()
        };

        public string Description => Type switch {
            SlotEffectType.Landmine => $"Hidden. The first creature to enter takes {Value} damage.",
            SlotEffectType.BarbedSnare => $"Hidden. The first creature to leave takes {Value} damage.",
            SlotEffectType.TemporalShield => "The creature here is immune to combat damage.",
            SlotEffectType.ScorchedEarth => "The creature here can't trigger its effects. Its entry effects wait until it's off this slot.",
            _ => ""
        };

        public override string ToString() => $"{Name} ({Controller?.Name}{(Days > 0 ? $", {Days} Days from Day {TurnApplied}" : "")})";
    }
}
