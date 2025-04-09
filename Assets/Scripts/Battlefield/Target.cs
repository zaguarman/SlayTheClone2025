using System.Collections.Generic;
using System.Linq;
using static Enums;

public interface ITarget {
    string TargetId { get; }
    bool IsValidTarget();
}

public static class TargetingSystem {
    public static List<ITarget> GetValidTargets(IPlayer controller, TargetType targetType) {
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

        return validTargets;
    }
}