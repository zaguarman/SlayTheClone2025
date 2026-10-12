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

        public DestroyCreatureAction(ICreature creature, ICreature killer = null) {
            this.creature = creature;
            this.killer = killer;
        }

        public override string ToString() => $"DestroyCreatureAction: Cr={creature?.Name}({creature?.TargetId.ShortId()}), Killer={killer?.Name ?? "none"}";
    }
}
