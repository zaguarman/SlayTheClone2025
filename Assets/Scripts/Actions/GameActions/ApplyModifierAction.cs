using static DebugLogger;
using System.Collections.Generic;
using Enums; // Assuming StatType uses Enums namespace

public class ApplyModifierAction : IGameAction {
    private readonly ITarget targetSpecifier;
    private readonly string modifierId; // Store the ID
    private readonly IModifierSource source;

    // Constructor using modifier ID (now the primary way)
    public ApplyModifierAction(ITarget targetSpecifier, string modifierId, IModifierSource source = null) {
        this.targetSpecifier = targetSpecifier;
        this.modifierId = modifierId; // Store the ID
        this.source = source;
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Log($"Created ApplyModifierAction: ModifierID={modifierId ?? "NULL"}, Target={targetSpecifier?.TargetId ?? "NULL"}", LogTag.Actions | LogTag.Effects);
        #endif
    }

    public void Execute() {
        // Use the stored modifierId
        if (string.IsNullOrEmpty(modifierId)) {
            LogError("Cannot execute ApplyModifierAction - Modifier ID is null or empty.", LogTag.Actions | LogTag.Effects);
            return;
        }
        if (targetSpecifier == null) {
            LogError("Cannot execute ApplyModifierAction - Target is null.", LogTag.Actions | LogTag.Effects);
            return;
        }

        List<IModifiable> resolvedTargets = ResolveTargets();

        if (resolvedTargets.Count == 0) {
            LogWarning($"ApplyModifierAction: No valid IModifiable targets found for specifier {targetSpecifier.TargetId}.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Access the ModifierFactory (assuming via GameReferences or GameManager)
        var factory = GameReferences.Instance?.ModifierFactory; // Or GameManager.Instance.ModifierFactory
        if (factory == null) {
            LogError("ModifierFactory not accessible.", LogTag.Actions | LogTag.Effects);
            return;
        }

        int successCount = 0;
        foreach (var modifiableTarget in resolvedTargets) {
            if (modifiableTarget == null) continue;

            // Create ActiveModifier using the factory with the ID
            ActiveModifier newModifier = factory.Create(modifierId, modifiableTarget, source);
            if (newModifier != null) {
                if (modifiableTarget.ModifierController.AddModifier(newModifier)) {
                    successCount++;
                }
            } else {
                // Factory Create method already logs error if ID not found
                // LogError($"Failed to create ActiveModifier instance for {modifierId}.", LogTag.Actions | LogTag.Effects);
            }
        }

        if (successCount > 0) {
            Log($"Executed ApplyModifierAction: Applied {modifierId} to {successCount} target(s) originating from {targetSpecifier.TargetId}.", LogTag.Actions | LogTag.Effects);
        } else {
            // Don't log warning if simply not applied due to stacking rules etc. AddModifier logs details.
            // Log($"Executed ApplyModifierAction: Modifier {modifierId} was not applied to any targets originating from {targetSpecifier.TargetId} (e.g., stacking rules).", LogTag.Actions | LogTag.Effects);
        }
    }

     // Helper to resolve ITarget to IModifiable(s)
     // This might need adaptation based on your specific ITarget implementations
     private List<IModifiable> ResolveTargets() {
         List<IModifiable> targets = new List<IModifiable>();

         if (targetSpecifier is IModifiable directlyModifiable) {
             targets.Add(directlyModifiable);
         }
         // Add cases for other ITarget types if they aren't directly IModifiable
         // Example: If ITarget could be a Player, find their Creatures or the Player object itself if it's IModifiable
         else if (targetSpecifier is IPlayer player) {
             // Decide if the modifier targets the player directly or their creatures?
             // If player implements IModifiable:
             // if (player is IModifiable modifiablePlayer) targets.Add(modifiablePlayer);

             // If it targets their creatures on the field:
             // foreach (var slot in player.Battlefield) {
             //     if (slot.OccupyingCreature is IModifiable modifiableCreature) {
             //         targets.Add(modifiableCreature);
             //     }
             // }
             LogWarning($"ApplyModifierAction: Target is IPlayer ({player.TargetId}), but resolution logic for Player targets is not fully defined. Assuming target is not modifiable.", LogTag.Actions);
         }
         // Add more `else if` cases as needed for your ITarget types

         return targets;
     }


    public override string ToString() {
        string sourceName = source?.GetSourceName() ?? "Unknown";
        // Use the stored modifierId
        return $"ApplyModifierAction: ModifierID={modifierId}, TargetSpecifier={targetSpecifier?.TargetId}, Source={sourceName}({source?.GetSourceId()})";
    }
}
