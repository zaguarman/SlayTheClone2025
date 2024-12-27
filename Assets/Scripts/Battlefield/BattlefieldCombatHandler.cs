using static DebugLogger;
using System.Collections.Generic;
using System.Linq;

public class BattlefieldCombatHandler {
    private readonly GameManager gameManager;
    private HashSet<ITarget> attackingCreatures = new HashSet<ITarget>();
    private readonly Dictionary<ITarget, BattlefieldSlot> targetedSlots = new Dictionary<ITarget, BattlefieldSlot>();
    private readonly Dictionary<CardController, ICreature> creatureCache = new Dictionary<CardController, ICreature>();

    public BattlefieldCombatHandler(GameManager gameManager) {
        this.gameManager = gameManager;
    }

    public void HandleCreatureCombat(CardController attackingCard, ITarget targetSlot) {
        var attackerCreature = GetCachedCreature(attackingCard);

        if (attackerCreature == null) {
            LogWarning("Attacker creature is null", LogTag.Creatures | LogTag.Actions);
            return;
        }

        if (HasCreatureAttacked(attackerCreature)) {
            LogWarning($"Creature {attackerCreature.Name} has already attacked this turn", LogTag.Creatures | LogTag.Combat);
            return;
        }

        if (targetSlot == null) {
            LogWarning("Target slot is null", LogTag.Creatures | LogTag.Combat);
            return;
        }

        RegisterAttack(attackerCreature, targetSlot);
        QueueCombatAction(attackerCreature, targetSlot);
    }

    private ICreature GetCachedCreature(CardController cardController) {
        if (cardController == null) return null;

        if (creatureCache.TryGetValue(cardController, out var cachedCreature)) {
            return cachedCreature;
        }

        string targetId = cardController.GetLinkedCreature().TargetId;
        if (string.IsNullOrEmpty(targetId)) return null;

        var slot = gameManager.Player1.Battlefield.FirstOrDefault(c => c.TargetId == targetId) ??
                      gameManager.Player2.Battlefield.FirstOrDefault(c => c.TargetId == targetId);

        var creature = slot?.OccupyingCreature;

        return creature;
    }

    private void RegisterAttack(ITarget attacker, ITarget targetSlot) {
        attackingCreatures.Add(attacker);
        targetedSlots[attacker] = (BattlefieldSlot)targetSlot;
    }

    private void QueueCombatAction(ICreature attackerCreature, ITarget targetSlot) {
        gameManager.ActionsQueue.AddAction(new MarkCombatTargetAction(attackerCreature, targetSlot));
        Log($"{attackerCreature.Name} with ID {attackerCreature.TargetId} targets slot {targetSlot.TargetId}", LogTag.Creatures | LogTag.Combat);
    }

    public void ResetAttackingCreatures() {
        attackingCreatures.Clear();
        targetedSlots.Clear();
        creatureCache.Clear();
        Log("Reset attacking creatures tracking", LogTag.Creatures | LogTag.Combat);
    }

    public bool HasCreatureAttacked(ITarget creature) {
        return attackingCreatures.Contains(creature);
    }

    public BattlefieldSlot GetTargetedSlot(ITarget attacker) {
        return targetedSlots.TryGetValue(attacker, out var slot) ? slot : null;
    }
}