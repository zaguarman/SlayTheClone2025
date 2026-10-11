using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class SwapCreaturesAction : IGameAction {
        private readonly ICreature fromCreature;
        private readonly ICreature toCreature;
        private readonly BattlefieldSlot fromSlot;
        private readonly BattlefieldSlot toSlot;

        public ICreature GetCreature1() => fromCreature;
        public ICreature GetCreature2() => toCreature;
        public BattlefieldSlot GetFromSlot() => fromSlot;
        public BattlefieldSlot GetToSlot() => toSlot;

        public SwapCreaturesAction(ICreature fromCreature, ICreature toCreature, BattlefieldSlot fromSlot, BattlefieldSlot toSlot) {
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
