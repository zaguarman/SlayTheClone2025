namespace SlayTheClone {
    /// <summary>
    /// The lethal check of the Creature Death Sequence (GDD 3.9): a creature at 0 Health is marked for destruction,
    /// its other pending actions are cancelled, and its OnAboutToDie effects get a chance to save it.
    /// </summary>
    public class CheckLethalAction : IGameAction {
        private readonly ICreature creature;
        private readonly ICreature killer; // Whoever brought it to 0 Health, if a creature did

        public ICreature GetCreature() => creature;
        public ICreature GetKiller() => killer;

        public CheckLethalAction(ICreature creature, ICreature killer = null) {
            this.creature = creature;
            this.killer = killer;
        }

        public override string ToString() => $"CheckLethalAction: Cr={creature?.Name}({creature?.TargetId.ShortId()}), Killer={killer?.Name ?? "none"}";
    }
}
