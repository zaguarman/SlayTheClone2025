using static DebugLogger;
using Enums;

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

        string attackerName = attacker != null ? attacker.Name : "Unknown";
        string attackerId = attacker != null ? attacker.TargetId.ToUpper() : "UNKNOWN";
        string slotId = targetSlot != null ? targetSlot.TargetId.ToUpper() : "UNKNOWN";

        Log($"Created MarkCombatTargetAction: {attackerName} (AttackerID: {attackerId}) targeting slot (SlotID: {slotId}) (ActionID: {GetHashCode().ToString().ToUpper()})",
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

        var actionsQueue = GameManager.Instance.ActionsQueue;
        ITarget primaryTarget = null;

        if (targetSlot.IsOccupied()) {
            var targetCreature = targetSlot.OccupyingCreature;
            if (targetCreature != null) {
                primaryTarget = targetCreature;
                Log($"Creature {attacker.Name} (AttackerID: {attacker.TargetId.ToUpper()}) attacking creature {targetCreature.Name} (TargetID: {targetCreature.TargetId.ToUpper()}) (ActionID: {GetHashCode().ToString().ToUpper()})",
                    LogTag.Combat);
                var damageAction = new DamageCreatureAction(targetCreature, attacker.Attack, attacker);
                actionsQueue.AddAction(damageAction);
            }
        } else {
            var targetPlayer = attacker.Owner?.Opponent;
            if (targetPlayer != null) {
                primaryTarget = targetPlayer;
                Log($"Creature {attacker.Name} (AttackerID: {attacker.TargetId.ToUpper()}) attacking player {(targetPlayer.IsPlayer1() ? "1" : "2")} (PlayerID: {targetPlayer.TargetId.ToUpper()}) (ActionID: {GetHashCode().ToString().ToUpper()})",
                    LogTag.Combat);
                actionsQueue.AddAction(
                    new DamagePlayerAction(targetPlayer, attacker.Attack)
                );
            }
        }

        // Check if the attacker has any spread damage effects
        if (primaryTarget != null && attacker is Creature creature) {
            // Look for spread damage effects in the creature's effects
            foreach (var effect in creature.Effects) {
                foreach (var action in effect.actions) {
                    if (action.targetModifier != TargetModifier.None) {
                        Log($"Applying spread damage effect with modifier {action.targetModifier} for {attacker.Name} (AttackerID: {attacker.TargetId.ToUpper()})",
                            LogTag.Combat | LogTag.Effects);
                        SpreadDamageEffect.ApplySpreadDamage(primaryTarget, attacker.Attack, attacker, action.targetModifier, actionsQueue);
                    }
                }
            }
        }
    }

    public override string ToString() {
        string attackerName = attacker != null ? attacker.Name : "Unknown";
        string attackerId = attacker != null ? attacker.TargetId.ToUpper() : "UNKNOWN";
        string slotId = targetSlot != null ? targetSlot.TargetId.ToUpper() : "UNKNOWN";
        return $"MarkCombatTargetAction: Attacker={attackerName} (AttackerID: {attackerId}), TargetSlot=(SlotID: {slotId}) (ActionID: {GetHashCode().ToString().ToUpper()})";
    }
    #endregion
}