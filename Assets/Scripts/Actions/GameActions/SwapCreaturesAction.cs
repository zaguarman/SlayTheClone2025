using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class SwapCreaturesAction : IGameAction {
        private readonly ICreature fromCreature;
        private readonly ICreature toCreature;
        private readonly BoardSlot fromSlot;
        private readonly BoardSlot toSlot;

        public ICreature GetCreature1() => fromCreature;
        public ICreature GetCreature2() => toCreature;
        public BoardSlot GetFromSlot() => fromSlot;
        public BoardSlot GetToSlot() => toSlot;

        public SwapCreaturesAction(ICreature fromCreature, ICreature toCreature, BoardSlot fromSlot, BoardSlot toSlot) {
            this.fromCreature = fromCreature;
            this.toCreature = toCreature;
            this.fromSlot = fromSlot;
            this.toSlot = toSlot;
            Log($"Created {this}", LogTag.Actions | LogTag.Creatures);
        }

        public override string ToString() {
            return $"SwapCreaturesAction: From={fromCreature?.Name ?? "Unknown"} (TargetID: {fromCreature?.TargetId.ToUpper() ?? "UNKNOWN"}), " +
                   $"To={toCreature?.Name ?? "Unknown"} (TargetID: {toCreature?.TargetId.ToUpper() ?? "UNKNOWN"})";
        }
    }
}
