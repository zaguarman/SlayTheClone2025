using System.Collections.Generic;
using System.Linq;
using Enums;

public interface ITarget {
    string TargetId { get; }
    bool IsValidTarget();
}

public static class TargetingSystem {
    private static System.Random random = new System.Random();

    public static List<ITarget> GetValidTargets(IPlayer controller, TargetType targetType, TargetModifier targetModifier = TargetModifier.None) {
        var validTargets = new List<ITarget>();

        switch (targetType) {
            case TargetType.Enemy:
                validTargets.Add(controller.Opponent);
                break;
            case TargetType.Player:
                validTargets.Add(controller);
                break;
            case TargetType.AllCreatures:
                // Get creatures from friendly battlefield
                validTargets.AddRange(controller.Battlefield
                    .Where(s => s.IsValidTarget() && s.IsOccupied() && s.OccupyingCreature != null)
                    .Select(s => s.OccupyingCreature)
                    .Cast<ITarget>());
                // Get creatures from enemy battlefield
                validTargets.AddRange(controller.Opponent.Battlefield
                    .Where(s => s.IsValidTarget() && s.IsOccupied() && s.OccupyingCreature != null)
                    .Select(s => s.OccupyingCreature)
                    .Cast<ITarget>());
                break;
            case TargetType.FriendlyCreatures:
                // Get creatures from friendly battlefield
                validTargets.AddRange(controller.Battlefield
                    .Where(s => s.IsValidTarget() && s.IsOccupied() && s.OccupyingCreature != null)
                    .Select(s => s.OccupyingCreature)
                    .Cast<ITarget>());
                break;
            case TargetType.EnemyCreatures:
                // Get creatures from enemy battlefield
                validTargets.AddRange(controller.Opponent.Battlefield
                    .Where(s => s.IsValidTarget() && s.IsOccupied() && s.OccupyingCreature != null)
                    .Select(s => s.OccupyingCreature)
                    .Cast<ITarget>());
                break;
        }

        // Apply target modifiers
        ApplyTargetModifiers(validTargets, targetModifier);

        return validTargets;
    }

    /// <summary>
    /// Gets additional targets based on the primary target and target modifiers
    /// </summary>
    /// <param name="controller">The player who controls the action</param>
    /// <param name="primaryTarget">The primary target</param>
    /// <param name="targetModifier">The target modifier to apply</param>
    /// <returns>A list of additional targets</returns>
    public static List<ITarget> GetAdditionalTargets(IPlayer controller, ITarget primaryTarget, TargetModifier targetModifier) {
        var additionalTargets = new List<ITarget>();

        // Apply different target modifiers
        if (targetModifier.HasFlag(TargetModifier.WithAdjacent)) {
            additionalTargets.AddRange(GetAdjacentTargets(primaryTarget));
        }

        if (targetModifier.HasFlag(TargetModifier.AllSameTypeTargets)) {
            additionalTargets.AddRange(GetSameTypeTargets(primaryTarget, controller));
        }

        if (targetModifier.HasFlag(TargetModifier.Chained)) {
            // For chained targets, we want to get a specific number of targets in a chain
            // Clear any other targets first since chained is a specific pattern
            additionalTargets.Clear();
            additionalTargets.AddRange(GetChainedTargets(primaryTarget, 2)); // Get 2 additional targets in the chain
        }

        // Remove the primary target from the list to avoid duplicate damage
        additionalTargets.RemoveAll(t => t.TargetId == primaryTarget.TargetId);

        // Apply random modifier if needed
        ApplyTargetModifiers(additionalTargets, targetModifier);

        return additionalTargets;
    }

    /// <summary>
    /// Applies target modifiers to a list of targets
    /// </summary>
    /// <param name="targets">The list of targets to modify</param>
    /// <param name="targetModifier">The target modifier to apply</param>
    private static void ApplyTargetModifiers(List<ITarget> targets, TargetModifier targetModifier) {
        // Apply Random modifier
        if (targetModifier.HasFlag(TargetModifier.Random) && targets.Count > 0) {
            int randomIndex = random.Next(targets.Count);
            var randomTarget = targets[randomIndex];
            targets.Clear();
            targets.Add(randomTarget);
        }
    }

    /// <summary>
    /// Gets targets adjacent to the primary target
    /// </summary>
    /// <param name="primaryTarget">The primary target</param>
    /// <returns>A list of adjacent targets</returns>
    private static List<ITarget> GetAdjacentTargets(ITarget primaryTarget) {
        var adjacentTargets = new List<ITarget>();

        // If the primary target is a creature, get adjacent creatures
        if (primaryTarget is ICreature creature && creature.Slot != null) {
            var owner = creature.Owner;
            if (owner == null) return adjacentTargets;

            // Get the index of the primary target's slot
            var slotIndex = owner.Battlefield.IndexOf(creature.Slot);
            if (slotIndex == -1) return adjacentTargets;

            // Get the adjacent slots
            if (slotIndex > 0) {
                var leftSlot = owner.Battlefield[slotIndex - 1];
                if (leftSlot.IsOccupied() && leftSlot.OccupyingCreature != null) {
                    adjacentTargets.Add(leftSlot.OccupyingCreature);
                }
            }

            if (slotIndex < owner.Battlefield.Count - 1) {
                var rightSlot = owner.Battlefield[slotIndex + 1];
                if (rightSlot.IsOccupied() && rightSlot.OccupyingCreature != null) {
                    adjacentTargets.Add(rightSlot.OccupyingCreature);
                }
            }
        }

        return adjacentTargets;
    }

    /// <summary>
    /// Gets targets of the same type as the primary target
    /// </summary>
    /// <param name="primaryTarget">The primary target</param>
    /// <param name="controller">The player who controls the action</param>
    /// <returns>A list of targets of the same type</returns>
    private static List<ITarget> GetSameTypeTargets(ITarget primaryTarget, IPlayer controller) {
        var sameTypeTargets = new List<ITarget>();

        // If the primary target is a creature, get all creatures of the same owner
        if (primaryTarget is ICreature creature) {
            var owner = creature.Owner;
            if (owner == null) return sameTypeTargets;

            // Get all creatures from the same owner
            sameTypeTargets.AddRange(owner.Battlefield
                .Where(s => s.IsValidTarget() && s.IsOccupied() && s.OccupyingCreature != null)
                .Select(s => s.OccupyingCreature)
                .Cast<ITarget>());
        }
        // If the primary target is a player, get all creatures owned by that player
        else if (primaryTarget is IPlayer player) {
            sameTypeTargets.AddRange(player.Battlefield
                .Where(s => s.IsValidTarget() && s.IsOccupied() && s.OccupyingCreature != null)
                .Select(s => s.OccupyingCreature)
                .Cast<ITarget>());
        }

        return sameTypeTargets;
    }

    /// <summary>
    /// Gets targets in a chain starting from the primary target
    /// </summary>
    /// <param name="primaryTarget">The primary target</param>
    /// <param name="chainLength">The number of additional targets to include in the chain</param>
    /// <returns>A list of targets in the chain</returns>
    private static List<ITarget> GetChainedTargets(ITarget primaryTarget, int chainLength) {
        var chainedTargets = new List<ITarget>();

        // If the primary target is a creature, get chained creatures
        if (primaryTarget is ICreature creature && creature.Slot != null) {
            var owner = creature.Owner;
            if (owner == null) return chainedTargets;

            // Get the index of the primary target's slot
            var slotIndex = owner.Battlefield.IndexOf(creature.Slot);
            if (slotIndex == -1) return chainedTargets;

            // Randomly choose a direction (left or right)
            bool goRight = random.Next(2) == 0;

            // Get targets in the chosen direction first
            var targetsInDirection = new List<ITarget>();
            if (goRight) {
                // Try to get targets to the right
                for (int i = slotIndex + 1; i < owner.Battlefield.Count && targetsInDirection.Count < chainLength; i++) {
                    var slot = owner.Battlefield[i];
                    if (slot.IsOccupied() && slot.OccupyingCreature != null) {
                        targetsInDirection.Add(slot.OccupyingCreature);
                    }
                }

                // If we couldn't get enough targets to the right, try the left side
                if (targetsInDirection.Count < chainLength) {
                    for (int i = slotIndex - 1; i >= 0 && targetsInDirection.Count < chainLength; i--) {
                        var slot = owner.Battlefield[i];
                        if (slot.IsOccupied() && slot.OccupyingCreature != null) {
                            targetsInDirection.Add(slot.OccupyingCreature);
                        }
                    }
                }
            } else {
                // Try to get targets to the left
                for (int i = slotIndex - 1; i >= 0 && targetsInDirection.Count < chainLength; i--) {
                    var slot = owner.Battlefield[i];
                    if (slot.IsOccupied() && slot.OccupyingCreature != null) {
                        targetsInDirection.Add(slot.OccupyingCreature);
                    }
                }

                // If we couldn't get enough targets to the left, try the right side
                if (targetsInDirection.Count < chainLength) {
                    for (int i = slotIndex + 1; i < owner.Battlefield.Count && targetsInDirection.Count < chainLength; i++) {
                        var slot = owner.Battlefield[i];
                        if (slot.IsOccupied() && slot.OccupyingCreature != null) {
                            targetsInDirection.Add(slot.OccupyingCreature);
                        }
                    }
                }
            }

            chainedTargets.AddRange(targetsInDirection);
        }

        return chainedTargets;
    }
}