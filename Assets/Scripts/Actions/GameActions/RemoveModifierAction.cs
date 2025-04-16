using static DebugLogger;
using System.Collections.Generic; // Needed for List<T>

public class RemoveModifierAction : IGameAction {
    private readonly ITarget targetSpecifier;
    private readonly string modifierIdToRemove;
    // Could also add a constructor taking ModifierData

    public RemoveModifierAction(ITarget targetSpecifier, string modifierIdToRemove) {
        this.targetSpecifier = targetSpecifier;
        this.modifierIdToRemove = modifierIdToRemove;
         #if UNITY_EDITOR || DEVELOPMENT_BUILD
         Log($"Created RemoveModifierAction: ModifierID={modifierIdToRemove}, Target={targetSpecifier?.TargetId ?? "NULL"}", LogTag.Actions | LogTag.Effects);
         #endif
    }

    public void Execute() {
        if (string.IsNullOrEmpty(modifierIdToRemove)) {
             LogError("Cannot execute RemoveModifierAction - Modifier ID is null or empty.", LogTag.Actions | LogTag.Effects);
             return;
        }
        if (targetSpecifier == null) {
             LogError("Cannot execute RemoveModifierAction - Target is null.", LogTag.Actions | LogTag.Effects);
             return;
        }

        // --- Target Resolution Logic (Similar to ApplyModifierAction) ---
        List<IModifiable> resolvedTargets = ResolveTargets();
        // --- End Target Resolution ---

        if (resolvedTargets.Count == 0) {
            LogWarning($"RemoveModifierAction: No valid IModifiable targets found for specifier {targetSpecifier.TargetId}.", LogTag.Actions | LogTag.Effects);
            return;
        }

        int successCount = 0;
        foreach(var modifiableTarget in resolvedTargets) {
             if (modifiableTarget == null) continue; // Skip if null

             if (modifiableTarget.ModifierController.RemoveModifier(modifierIdToRemove)) {
                 successCount++;
             }
        }

        if (successCount > 0) {
            Log($"Executed RemoveModifierAction: Removed {modifierIdToRemove} from {successCount} target(s) originating from {targetSpecifier.TargetId}.", LogTag.Actions | LogTag.Effects);
        } else {
             // This isn't necessarily an error, the modifier might just not be present
             // Log($"Executed RemoveModifierAction: Modifier {modifierIdToRemove} was not found on target(s) originating from {targetSpecifier.TargetId}.", LogTag.Actions | LogTag.Effects);
        }
    }

     // Helper to resolve ITarget to IModifiable(s) - Reuse or adapt from ApplyModifierAction
     private List<IModifiable> ResolveTargets() {
         List<IModifiable> targets = new List<IModifiable>();
         if (targetSpecifier is IModifiable directlyModifiable) {
             targets.Add(directlyModifiable);
         }
         // Add cases for other ITarget types if they aren't directly IModifiable
         else if (targetSpecifier is IPlayer player) {
             // Example: If targeting player's creatures
             // foreach (var slot in player.Battlefield) {
             //     if (slot.OccupyingCreature is IModifiable modifiableCreature) {
             //         targets.Add(modifiableCreature);
             //     }
             // }
             LogWarning($"RemoveModifierAction: Target is IPlayer ({player.TargetId}), but resolution logic for Player targets is not fully defined.", LogTag.Actions);
         }
         return targets;
     }

    public override string ToString() {
        return $"RemoveModifierAction: ModifierID={modifierIdToRemove}, TargetSpecifier={targetSpecifier?.TargetId}";
    }
}
