namespace Zeitgeist {
    /// <summary>
    /// Doomed's last stack is gone: Last Rites, then the creature is destroyed. Both happen right away, as part of this
    /// action, and nothing can save it (Status Effects Compendium, Doomed).
    /// </summary>
    public class DoomedSentenceAction : IGameAction {
        private readonly ICreature creature;

        public ICreature GetCreature() => creature;

        public DoomedSentenceAction(ICreature creature) {
            this.creature = creature;
        }

        public override string ToString() => $"DoomedSentenceAction: Cr={creature?.Name}({creature?.TargetId.ShortId()})";
    }
}
