using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class BattlefieldCombatAction : IGameAction {
        private readonly ICreature attacker;
        private readonly BoardSlot targetSlot;

        public ICreature GetAttacker() => attacker;
        public BoardSlot GetTargetSlot() => targetSlot;

        public BattlefieldCombatAction(ICreature attacker, BoardSlot targetSlot) {
            this.attacker = attacker;
            this.targetSlot = targetSlot;
            Log($"Created {this}", LogTag.Actions | LogTag.Combat);
        }

        public override string ToString() {
            return $"BattlefieldCombatAction: Attacker={attacker?.Name ?? "Unknown"} (AttackerID: {attacker?.TargetId.ToUpper() ?? "UNKNOWN"}), " +
                   $"TargetSlot=(SlotID: {targetSlot?.TargetId.ToUpper() ?? "UNKNOWN"}) (ActionID: {GetHashCode()})";
        }
    }
}
