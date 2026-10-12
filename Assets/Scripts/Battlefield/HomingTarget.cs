namespace SlayTheClone {
    /// <summary>
    /// An effect locked onto one creature (GDD 4.6, Homing): it follows the creature as it moves. A spell aimed at one of
    /// your own creatures homes on it, since there's no fog of war on your own side (see <see cref="EffectResolver.AimAt"/>).
    /// If the creature leaves play first, the effect is aimed at the slot it was last in; if it never got into play
    /// (its summon was taken back), at the slot it was aimed at.
    /// </summary>
    public class HomingTarget : ITarget {
        public ICreature Creature { get; }
        public BoardSlot AimedSlot { get; }
        public string TargetId => Creature.TargetId;

        private readonly bool wasInPlay;
        private readonly int entriesAtAim;

        public HomingTarget(ICreature creature, BoardSlot aimedSlot) {
            Creature = creature ?? throw new System.ArgumentNullException(nameof(creature));
            AimedSlot = aimedSlot;
            wasInPlay = creature.Slot != null;
            entriesAtAim = creature.TimesEnteredPlay;
        }

        public bool IsValidTarget() => true;

        /// <summary>The creature, while it is in play.</summary>
        public ICreature CreatureInPlay => Creature.Slot != null && !Creature.IsDead ? Creature : null;

        /// <summary>The slot the effect is aimed at right now.</summary>
        public BoardSlot CurrentSlot {
            get {
                if (Creature.Slot != null) return Creature.Slot;
                bool beenInPlaySinceAimed = wasInPlay || Creature.TimesEnteredPlay > entriesAtAim;
                return beenInPlaySinceAimed ? Creature.LastSlot ?? AimedSlot : AimedSlot;
            }
        }

        public override string ToString() => $"Homing on {Creature.Name}";
    }
}
