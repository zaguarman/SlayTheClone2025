using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static DebugLogger;

// Manages creation of ActiveModifier instances using ModifierDefinition
public class ModifierFactory {
    // Registry now holds ModifierDefinition
    private Dictionary<string, ModifierDefinition> modifierRegistry = new Dictionary<string, ModifierDefinition>();

    // Initialize with runtime ModifierDefinition data
    public void Initialize(IEnumerable<ModifierDefinition> allModifierDefinitions) {
        modifierRegistry.Clear();
        if (allModifierDefinitions == null) {
            LogWarning("ModifierFactory Initialize received null definition collection.", LogTag.Initialization);
            return;
        }
        foreach (var definition in allModifierDefinitions) {
            if (definition != null && !string.IsNullOrEmpty(definition.modifierId)) {
                if (!modifierRegistry.ContainsKey(definition.modifierId)) {
                    modifierRegistry.Add(definition.modifierId, definition);
                } else {
                    LogWarning($"Duplicate Modifier ID found during factory init: {definition.modifierId} ({definition.displayName}). Using first encountered.", LogTag.Initialization | LogTag.Effects);
                }
            } else {
                LogWarning($"ModifierDefinition is missing an ID or is null. Skipping registration.", LogTag.Initialization | LogTag.Effects);
            }
        }
        Log($"ModifierFactory initialized with {modifierRegistry.Count} modifier definitions.", LogTag.Initialization | LogTag.Effects);
    }

    // Renamed from GetModifierData to GetModifierDefinition
    public ModifierDefinition GetModifierDefinition(string modifierId) {
        if (string.IsNullOrEmpty(modifierId)) return null;
        modifierRegistry.TryGetValue(modifierId, out ModifierDefinition definition);
        if (definition == null) {
             // LogWarning($"ModifierDefinition with ID '{modifierId}' not found in registry.", LogTag.Effects);
        }
        return definition;
    }

    // Create using modifier ID (primary method)
    public ActiveModifier Create(string modifierId, IModifiable target, IModifierSource source = null) {
        ModifierDefinition definition = GetModifierDefinition(modifierId);
        if (definition != null) {
            // Pass the ModifierDefinition to ActiveModifier constructor
            return new ActiveModifier(definition, target, source);
        } else {
            LogError($"ModifierFactory: ModifierDefinition with ID '{modifierId}' not found. Cannot create ActiveModifier.", LogTag.Effects);
            return null;
        }
    }

    // REMOVED: Create(ModifierData data, ...) - No longer compatible or needed.
}
