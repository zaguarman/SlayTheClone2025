using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// Removes a creature's statuses, its Temporary Stat Modifications, or both (GDD Cleanse). A Cleanse only removes the bad
    /// ones (harmful statuses, stat decreases); a Purge removes good and bad alike. Persistent Stat Modifications stay.
    /// Doomed can't be cleansed: the attempt passes it on instead (Heresy).
    /// </summary>
    public class CleanseAction : IGameAction {
        private readonly ICreature targetCreature;
        private readonly CleanseTarget what;

        public ICreature GetTargetCreature() => targetCreature;
        public CleanseTarget GetWhat() => what;
        /// <summary>A Purge removes the good with the bad; a Cleanse only the bad.</summary>
        public bool IsPurge { get; }

        public CleanseAction(ICreature target, CleanseTarget what, bool purge = false) {
            targetCreature = target;
            this.what = what;
            IsPurge = purge;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"CleanseAction: Target={targetCreature?.Name}, Removes={what}{(IsPurge ? " (Purge)" : "")}";
    }
}
