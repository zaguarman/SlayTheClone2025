namespace SlayTheClone {
    /// <summary>
    /// The last step of the Creature Death Sequence (GDD 3.9): unless something saved it, the creature is destroyed,
    /// goes to its owner's discard pile, and its OnDied effects are queued.
    /// </summary>
    public class DestroyCreatureAction : IGameAction {
        private readonly ICreature creature;
        private readonly ICreature killer;

        public ICreature GetCreature() => creature;
        public ICreature GetKiller() => killer;
        /// <summary>Destroyed whatever its Health (like Doomed's sentence): nothing can save it.</summary>
        public bool IsUnconditional { get; }
        /// <summary>Whose effect destroys it, if known: an enemy's can land on a Fall Guy instead.</summary>
        public IPlayer Controller { get; }

        public DestroyCreatureAction(ICreature creature, ICreature killer = null, bool unconditional = false, IPlayer controller = null) {
            this.creature = creature;
            this.killer = killer;
            IsUnconditional = unconditional;
            Controller = controller ?? killer?.Owner;
        }

        public override string ToString() => $"DestroyCreatureAction: Cr={creature?.Name}({creature?.TargetId.ShortId()}), Killer={killer?.Name ?? "none"}";
    }
}
