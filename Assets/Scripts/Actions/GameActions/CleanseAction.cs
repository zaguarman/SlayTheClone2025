using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Removes a creature's statuses, its Temporary Stat Modifications, or both (GDD Cleanse). Persistent Stat Modifications
    /// stay. Doomed can't be cleansed: the attempt passes it on instead (Heresy).
    /// </summary>
    public class CleanseAction : IGameAction {
        private readonly ICreature targetCreature;
        private readonly CleanseTarget what;

        public ICreature GetTargetCreature() => targetCreature;
        public CleanseTarget GetWhat() => what;

        public CleanseAction(ICreature target, CleanseTarget what) {
            targetCreature = target;
            this.what = what;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"CleanseAction: Target={targetCreature?.Name}, Removes={what}";
    }
}
