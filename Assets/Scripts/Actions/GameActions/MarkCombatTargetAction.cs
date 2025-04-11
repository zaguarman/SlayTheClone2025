using static DebugLogger;

public class MarkCombatTargetAction : IGameAction {
    #region Fields & Properties
    private readonly ICreature attacker;
    private readonly BattlefieldSlot targetSlot;
    public ICreature GetAttacker() => attacker;
    public BattlefieldSlot GetTargetSlot() => targetSlot;
    #endregion

    #region Constructor
    public MarkCombatTargetAction(ICreature attacker, ITarget targetSlot) {
        this.attacker = attacker;
        this.targetSlot = (BattlefieldSlot)targetSlot;
        Log($"Created MarkCombatTargetAction: {attacker?.Name ?? "Unknown"} (AttackerID: {attacker?.TargetId.ToUpper() ?? "UNKNOWN"}) targeting slot (SlotID: {targetSlot?.TargetId.ToUpper() ?? "UNKNOWN"}) (ActionID: {GetHashCode().ToString().ToUpper()})",
            LogTag.Actions | LogTag.Combat);
    }
    #endregion

    #region Methods
    public void Execute() {
        if (attacker == null || targetSlot == null) {
            LogError($"Cannot execute combat action - attacker or target slot is null (ActionID: {GetHashCode().ToString().ToUpper()})",
                LogTag.Actions | LogTag.Combat);
            return;
        }

        if (targetSlot.IsOccupied()) {
            var targetCreature = targetSlot.OccupyingCreature;
            if (targetCreature != null) {
                Log($"Creature {attacker.Name} (AttackerID: {attacker.TargetId.ToUpper()}) attacking creature {targetCreature.Name} (TargetID: {targetCreature.TargetId.ToUpper()}) (ActionID: {GetHashCode().ToString().ToUpper()})",
                    LogTag.Combat);
                var damageAction = new DamageCreatureAction(targetCreature, attacker.Attack, attacker);
                GameManager.Instance.ActionsQueue.AddAction(damageAction);
            }
        } else {
            var targetPlayer = attacker.Owner?.Opponent;
            if (targetPlayer != null) {
                Log($"Creature {attacker.Name} (AttackerID: {attacker.TargetId.ToUpper()}) attacking player {(targetPlayer.IsPlayer1() ? "1" : "2")} (PlayerID: {targetPlayer.TargetId.ToUpper()}) (ActionID: {GetHashCode().ToString().ToUpper()})",
                    LogTag.Combat);
                GameManager.Instance.ActionsQueue.AddAction(
                    new DamagePlayerAction(targetPlayer, attacker.Attack)
                );
            }
        }
    }

    public override string ToString() {
        return $"MarkCombatTargetAction: Attacker={attacker?.Name ?? "Unknown"} (AttackerID: {attacker?.TargetId.ToUpper() ?? "UNKNOWN"}), TargetSlot=(SlotID: {targetSlot?.TargetId.ToUpper() ?? "UNKNOWN"}) (ActionID: {GetHashCode().ToString().ToUpper()})";
    }
    #endregion
}