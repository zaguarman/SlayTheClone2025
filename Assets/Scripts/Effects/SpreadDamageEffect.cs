using System.Collections.Generic;
using static Enums;
using static DebugLogger;

public static class SpreadDamageEffect {
    /// <summary>
    /// Applies spread damage to targets based on the target modifier
    /// </summary>
    /// <param name="primaryTarget">The primary target of the attack</param>
    /// <param name="damage">The amount of damage to deal</param>
    /// <param name="attacker">The creature that is attacking</param>
    /// <param name="targetModifier">The target modifier to apply</param>
    /// <param name="actionsQueue">The actions queue to add damage actions to</param>
    public static void ApplySpreadDamage(ITarget primaryTarget, int damage, ICreature attacker, TargetModifier targetModifier, ActionsQueue actionsQueue) {
        if (targetModifier == TargetModifier.None) return;

        // Get additional targets based on the target modifier
        var additionalTargets = GetAdditionalTargets(primaryTarget, attacker, targetModifier);

        // Apply damage to additional targets
        foreach (var target in additionalTargets) {
            if (target is ICreature creature) {
                Log($"Applying spread damage of {damage} to {creature.Name} (TargetID: {creature.TargetId.ToUpper()}) from {attacker.Name} (TargetID: {attacker.TargetId.ToUpper()})",
                    LogTag.Creatures | LogTag.Combat | LogTag.Effects);
                actionsQueue.AddAction(new DamageCreatureAction(creature, damage, attacker));
            } else if (target is IPlayer player) {
                Log($"Applying spread damage of {damage} to player {(player.IsPlayer1() ? "1" : "2")} (TargetID: {player.TargetId.ToUpper()}) from {attacker.Name} (TargetID: {attacker.TargetId.ToUpper()})",
                    LogTag.Players | LogTag.Combat | LogTag.Effects);
                actionsQueue.AddAction(new DamagePlayerAction(player, damage));
            }
        }
    }

    /// <summary>
    /// Gets additional targets based on the target modifier
    /// </summary>
    /// <param name="primaryTarget">The primary target of the attack</param>
    /// <param name="targetModifier">The target modifier to apply</param>
    /// <returns>A list of additional targets</returns>
    private static List<ITarget> GetAdditionalTargets(ITarget primaryTarget, ICreature _, TargetModifier targetModifier) {
        // Let the TargetingSystem handle the target selection based on the primary target and modifiers
        if (primaryTarget is ICreature creature && creature.Owner != null) {
            // For creature targets, get additional targets based on the creature's owner and the target modifier
            return TargetingSystem.GetAdditionalTargets(creature.Owner, primaryTarget, targetModifier);
        }
        else if (primaryTarget is IPlayer player) {
            // For player targets, get additional targets based on the player and the target modifier
            return TargetingSystem.GetAdditionalTargets(player, primaryTarget, targetModifier);
        }

        return new List<ITarget>();
    }
}
