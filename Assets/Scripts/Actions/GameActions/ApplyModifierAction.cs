using static DebugLogger;
using System.Collections.Generic; // Needed for List<T>

public class ApplyModifierAction : IGameAction {
    private readonly ITarget targetSpecifier; // Can be Creature, Player, Slot etc.
    private readonly ModifierData modifierData;
    private readonly IModifierSource source; // Optional: Who applied it

    // Constructor using ModifierData directly (preferred)
    public ApplyModifierAction(ITarget targetSpecifier, ModifierData modifierData, IModifierSource source = null) {
        this.targetSpecifier = targetSpecifier;
        this.modifierData = modifierData;
        this.source = source; // Can be null
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Log($"Created ApplyModifierAction: Modifier={modifierData?.modifierId ?? "NULL"}, Target={targetSpecifier?.TargetId ?? "NULL"}", LogTag.Actions | LogTag.Effects);
        #endif
    }

    // Constructor using modifier ID (requires factory lookup)
    public ApplyModifierAction(ITarget targetSpecifier, string modifierId, IModifierSource source = null)
        : this(targetSpecifier, GetModifierDataFromFactory(modifierId), source) { }

    private static ModifierData GetModifierDataFromFactory(string modifierId) {
        // Assuming ModifierFactory is accessible via GameReferences
        var factory = GameReferences.Instance?.ModifierFactory;
         if (factory == null) {
             LogError("ApplyModifierAction: ModifierFactory not accessible via GameReferences.", LogTag.Actions | LogTag.Effects);
             return null;
         }
        return factory.GetModifierData(modifierId);
    }

    public void Execute() {
        if (modifierData == null) {
            LogError("Cannot execute ApplyModifierAction - ModifierData is null.", LogTag.Actions | LogTag.Effects);
            return;
        }
        if (targetSpecifier == null) {
             LogError("Cannot execute ApplyModifierAction - Target is null.", LogTag.Actions | LogTag.Effects);
             return;
        }

        // --- Target Resolution Logic ---
        List<IModifiable> resolvedTargets = ResolveTargets();
        // --- End Target Resolution ---


        if (resolvedTargets.Count == 0) {
            LogWarning($"ApplyModifierAction: No valid IModifiable targets found for specifier {targetSpecifier.TargetId}.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Assuming ModifierFactory is accessible via GameReferences
        var factory = GameReferences.Instance?.ModifierFactory;
        if (factory == null) {
            LogError("ModifierFactory not accessible.", LogTag.Actions | LogTag.Effects);
            return;
        }

        int successCount = 0;
        foreach(var modifiableTarget in resolvedTargets) {
            if(modifiableTarget == null) continue; // Skip if null

            ActiveModifier newModifier = factory.Create(modifierData, modifiableTarget, source);
            if (newModifier != null) {
                if (modifiableTarget.ModifierController.AddModifier(newModifier)) {
                    successCount++;
                }
            } else {
                LogError($"Failed to create ActiveModifier instance for {modifierData.modifierId}.", LogTag.Actions | LogTag.Effects);
            }
        }

        if (successCount > 0) {
             Log($"Executed ApplyModifierAction: Applied {modifierData.modifierId} to {successCount} target(s) originating from {targetSpecifier.TargetId}.", LogTag.Actions | LogTag.Effects);
        } else {
             Log($"Executed ApplyModifierAction: Modifier {modifierData.modifierId} was not applied to any targets originating from {targetSpecifier.TargetId} (e.g., stacking rules).", LogTag.Actions | LogTag.Effects);
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
        return $"ApplyModifierAction: Modifier={modifierData?.modifierId}, TargetSpecifier={targetSpecifier?.TargetId}, Source={sourceName}({source?.GetSourceId()})";
    }
}
