using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static DebugLogger;

// Manages creation of ActiveModifier instances
public class ModifierFactory {
    private Dictionary<string, ModifierData> modifierRegistry = new Dictionary<string, ModifierData>();

    public void Initialize(IEnumerable<ModifierData> allModifierData) {
        modifierRegistry.Clear();
        if (allModifierData == null) {
             LogWarning("ModifierFactory Initialize received null data collection.", LogTag.Initialization);
             return;
        }
        foreach (var data in allModifierData) {
            if (data != null && !string.IsNullOrEmpty(data.modifierId)) {
                if (!modifierRegistry.ContainsKey(data.modifierId)) {
                    modifierRegistry.Add(data.modifierId, data);
                } else {
                    LogWarning($"Duplicate Modifier ID found: {data.modifierId} ({data.name}). Using first encountered.", LogTag.Initialization | LogTag.Effects);
                }
            } else {
                 LogWarning($"ModifierData asset is missing an ID or is null. Skipping.", LogTag.Initialization | LogTag.Effects);
            }
        }
        Log($"ModifierFactory initialized with {modifierRegistry.Count} modifier types.", LogTag.Initialization | LogTag.Effects);
    }

    public ModifierData GetModifierData(string modifierId) {
        if (string.IsNullOrEmpty(modifierId)) return null;
        modifierRegistry.TryGetValue(modifierId, out ModifierData data);
        if(data == null) {
            // LogWarning($"ModifierData with ID '{modifierId}' not found in registry.", LogTag.Effects);
        }
        return data;
    }

    public ActiveModifier Create(string modifierId, IModifiable target, IModifierSource source = null) {
        ModifierData data = GetModifierData(modifierId);
        if (data != null) {
            return new ActiveModifier(data, target, source);
        } else {
            LogError($"ModifierFactory: ModifierData with ID '{modifierId}' not found.", LogTag.Effects);
            return null;
        }
    }

    public ActiveModifier Create(ModifierData data, IModifiable target, IModifierSource source = null) {
        if (data == null) {
            LogError("ModifierFactory: Provided ModifierData is null.", LogTag.Effects);
            return null;
        }
        // Ensure data is registered (optional, but good practice)
        if (!modifierRegistry.ContainsKey(data.modifierId)) {
            LogWarning($"ModifierFactory: ModifierData '{data.modifierId}' ({data.name}) was not pre-registered. Creating anyway.", LogTag.Effects);
        }
        return new ActiveModifier(data, target, source);
    }
}
