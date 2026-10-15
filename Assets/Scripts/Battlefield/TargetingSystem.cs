using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public interface ITarget {
        string TargetId { get; }
        bool IsValidTarget();
    }

    public static class TargetingSystem {
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
                    validTargets.AddRange(CreaturesOf(controller));
                    validTargets.AddRange(CreaturesOf(controller.Opponent));
                    break;
                case TargetType.FriendlyCreatures:
                    validTargets.AddRange(CreaturesOf(controller));
                    break;
                case TargetType.EnemyCreatures:
                    validTargets.AddRange(CreaturesOf(controller.Opponent));
                    break;
            }

            // Apply target modifiers
            ApplyTargetModifiers(validTargets, targetModifier);

            return validTargets;
        }

        // The creatures on a player's battlefield, left to right
        private static IEnumerable<ITarget> CreaturesOf(IPlayer player) {
            return player.Battlefield
                .Where(s => s.IsValidTarget() && s.IsOccupied() && s.OccupyingCreature != null)
                .Select(s => s.OccupyingCreature);
        }

        private static ICreature CreatureAt(IReadOnlyList<BoardSlot> battlefield, int index) {
            var slot = battlefield[index];
            return slot.IsOccupied() ? slot.OccupyingCreature : null;
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
                int randomIndex = GameRandom.Next(targets.Count);
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
                var slotIndex = creature.Slot.Index;

                foreach (int index in Formation.Neighbours(slotIndex)) {
                    if (index < 0 || index >= owner.Battlefield.Count) continue;
                    var neighbor = CreatureAt(owner.Battlefield, index);
                    if (neighbor != null) adjacentTargets.Add(neighbor);
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

            // A creature's allies, or a player's creatures
            var owner = primaryTarget is ICreature creature ? creature.Owner : primaryTarget as IPlayer;
            if (owner != null) {
                sameTypeTargets.AddRange(CreaturesOf(owner));
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
                var slotIndex = creature.Slot.Index;

                // Randomly pick a side to go first; the other side fills the chain if needed
                int firstStep = GameRandom.Next(2) == 0 ? 1 : -1;
                AddCreaturesInDirection(owner.Battlefield, slotIndex, firstStep, chainedTargets, chainLength);
                AddCreaturesInDirection(owner.Battlefield, slotIndex, -firstStep, chainedTargets, chainLength);
            }

            return chainedTargets;
        }

        // Walks away from a slot, skipping empty slots, until the list holds maxCount targets
        private static void AddCreaturesInDirection(IReadOnlyList<BoardSlot> battlefield, int fromIndex, int step, List<ITarget> targets, int maxCount) {
            for (int i = fromIndex + step; i >= 0 && i < battlefield.Count && targets.Count < maxCount; i += step) {
                var creature = CreatureAt(battlefield, i);
                if (creature != null) targets.Add(creature);
            }
        }

        /// <summary>
        /// Gets targets in a chain starting *adjacent* to the primary target, following the longest path.
        /// Used by damage with the Chained modifier.
        /// </summary>
        /// <param name="primaryTarget">The target that was initially hit (used to find the battlefield).</param>
        /// <param name="maxChainLength">The maximum number of additional targets in the chain.</param>
        /// <returns>An *ordered* list of targets in the chain (first jump, second jump, etc.).</returns>
        public static List<ICreature> GetDirectionalChainTargets(ITarget primaryTarget, int maxChainLength) {
            var chainTargets = new List<ICreature>();
            if (maxChainLength <= 0) return chainTargets;

            // We need a creature target to determine the battlefield and position
            if (!(primaryTarget is ICreature creature && creature.Slot != null && creature.Owner != null)) {
                return chainTargets; // Cannot determine chain without a creature in a slot
            }

            var ownerBattlefield = creature.Owner.Battlefield;
            var visited = new HashSet<int> { creature.Slot.Index };
            int current = creature.Slot.Index;

            // The chain jumps from creature to creature a step apart in the formation, never back; with a choice, the one
            // that leads on to more creatures, then at random. An empty step breaks it
            bool Occupied(int i) => i >= 0 && i < ownerBattlefield.Count && ownerBattlefield[i].IsOccupied() && ownerBattlefield[i].OccupyingCreature != null;
            while (chainTargets.Count < maxChainLength) {
                var next = Formation.Neighbours(current).Where(i => !visited.Contains(i) && Occupied(i))
                    .OrderByDescending(i => Formation.Neighbours(i).Count(j => !visited.Contains(j) && j != i && Occupied(j)))
                    .ThenBy(_ => GameRandom.Next(int.MaxValue)).DefaultIfEmpty(-1).First();
                if (next < 0) break;
                chainTargets.Add(ownerBattlefield[next].OccupyingCreature);
                visited.Add(next);
                current = next;
            }
            return chainTargets;
        }
    }
}
