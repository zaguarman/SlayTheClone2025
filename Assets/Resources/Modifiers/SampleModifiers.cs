using UnityEngine;
using Enums;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;

// This script provides a way to create sample ModifierData assets in the editor
// It's wrapped in UNITY_EDITOR so it won't be included in builds
public class SampleModifiers
{
    [MenuItem("Assets/Create/Game Data/Sample Modifiers")]
    public static void CreateSampleModifiers()
    {
        CreateStrengthModifier();
        CreateWeaknessModifier();
        CreateRegenerationModifier();
        CreatePoisonModifier();
        CreateArmorModifier();
        
        AssetDatabase.SaveAssets();
        Debug.Log("Created sample modifiers in Resources/Modifiers folder");
    }

    private static void CreateStrengthModifier()
    {
        var modifier = ScriptableObject.CreateInstance<ModifierData>();
        modifier.modifierId = "strength";
        modifier.displayName = "Strength";
        modifier.description = "Increases attack power.";
        modifier.durationType = ModifierDurationType.Permanent;
        modifier.stackingType = ModifierStackingType.Stackable;
        modifier.maxStacks = 3;
        
        var statAdjustment = new StatAdjustment
        {
            statType = StatType.Attack,
            adjustmentType = StatAdjustmentType.Flat,
            value = 2
        };
        
        modifier.statAdjustments = new List<StatAdjustment> { statAdjustment };
        modifier.tags = new List<string> { "Buff" };
        
        AssetDatabase.CreateAsset(modifier, "Assets/Resources/Modifiers/Strength.asset");
    }

    private static void CreateWeaknessModifier()
    {
        var modifier = ScriptableObject.CreateInstance<ModifierData>();
        modifier.modifierId = "weakness";
        modifier.displayName = "Weakness";
        modifier.description = "Reduces attack power temporarily.";
        modifier.durationType = ModifierDurationType.TurnBased;
        modifier.baseDuration = 2;
        modifier.stackingType = ModifierStackingType.RefreshDuration;
        
        var statAdjustment = new StatAdjustment
        {
            statType = StatType.Attack,
            adjustmentType = StatAdjustmentType.Flat,
            value = -1
        };
        
        modifier.statAdjustments = new List<StatAdjustment> { statAdjustment };
        modifier.tags = new List<string> { "Debuff" };
        
        AssetDatabase.CreateAsset(modifier, "Assets/Resources/Modifiers/Weakness.asset");
    }

    private static void CreateRegenerationModifier()
    {
        var modifier = ScriptableObject.CreateInstance<ModifierData>();
        modifier.modifierId = "regeneration";
        modifier.displayName = "Regeneration";
        modifier.description = "Heals at the start of each turn.";
        modifier.durationType = ModifierDurationType.TurnBased;
        modifier.baseDuration = 3;
        modifier.stackingType = ModifierStackingType.Stackable;
        modifier.maxStacks = 2;
        
        // Note: This doesn't actually implement the healing effect
        // You would need to add a StartOfTurn effect handler that checks for this modifier
        
        modifier.tags = new List<string> { "Buff", "Healing" };
        
        AssetDatabase.CreateAsset(modifier, "Assets/Resources/Modifiers/Regeneration.asset");
    }

    private static void CreatePoisonModifier()
    {
        var modifier = ScriptableObject.CreateInstance<ModifierData>();
        modifier.modifierId = "poison";
        modifier.displayName = "Poison";
        modifier.description = "Takes damage at the end of each turn.";
        modifier.durationType = ModifierDurationType.TurnBased;
        modifier.baseDuration = 2;
        modifier.stackingType = ModifierStackingType.Stackable;
        modifier.maxStacks = 5;
        
        // Note: This doesn't actually implement the damage effect
        // You would need to add an EndOfTurn effect handler that checks for this modifier
        
        modifier.tags = new List<string> { "Debuff", "Damage Over Time" };
        
        AssetDatabase.CreateAsset(modifier, "Assets/Resources/Modifiers/Poison.asset");
    }

    private static void CreateArmorModifier()
    {
        var modifier = ScriptableObject.CreateInstance<ModifierData>();
        modifier.modifierId = "armor";
        modifier.displayName = "Armor";
        modifier.description = "Increases maximum health.";
        modifier.durationType = ModifierDurationType.Permanent;
        modifier.stackingType = ModifierStackingType.Stackable;
        modifier.maxStacks = 3;
        
        var statAdjustment = new StatAdjustment
        {
            statType = StatType.MaxHealth,
            adjustmentType = StatAdjustmentType.Flat,
            value = 2
        };
        
        modifier.statAdjustments = new List<StatAdjustment> { statAdjustment };
        modifier.tags = new List<string> { "Buff", "Defense" };
        
        AssetDatabase.CreateAsset(modifier, "Assets/Resources/Modifiers/Armor.asset");
    }
}
#endif
