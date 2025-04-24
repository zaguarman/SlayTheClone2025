using static DebugLogger;
using static Enums;

public class BattlefieldCombatAction : IGameAction {
    private readonly ICreature attacker;
    private readonly BattlefieldSlot targetSlot;
    public ICreature GetAttacker() => attacker;
    public BattlefieldSlot GetTargetSlot() => targetSlot;

    public BattlefieldCombatAction(ICreature attacker, ITarget targetSlot) {
        this.attacker = attacker;
        this.targetSlot = (BattlefieldSlot)targetSlot;

        string attackerName = attacker != null ? attacker.Name : "Unknown";
        string attackerId = attacker != null ? attacker.TargetId.ToUpper() : "UNKNOWN";
        string slotId = targetSlot != null ? targetSlot.TargetId.ToUpper() : "UNKNOWN";

        Log($"Created MarkCombatTargetAction: {attackerName} (AttackerID: {attackerId}) targeting slot (SlotID: {slotId}) (ActionID: {GetHashCode().ToString().ToUpper()})",
            LogTag.Actions | LogTag.Combat);
    }



    public override string ToString() {
        string attackerName = attacker != null ? attacker.Name : "Unknown";
        string attackerId = attacker != null ? attacker.TargetId.ToUpper() : "UNKNOWN";
        string slotId = targetSlot != null ? targetSlot.TargetId.ToUpper() : "UNKNOWN";
        return $"MarkCombatTargetAction: Attacker={attackerName} (AttackerID: {attackerId}), TargetSlot=(SlotID: {slotId}) (ActionID: {GetHashCode().ToString().ToUpper()})";
    }
}