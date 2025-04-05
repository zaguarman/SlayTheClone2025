#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using static Enums;
using System.Collections.Generic;

public static class CardEditorMenu
{
    [MenuItem("Cards/Create New Creature")]
    public static void CreateNewCreature()
    {
        CreatureCardScriptableObject creature = ScriptableObject.CreateInstance<CreatureCardScriptableObject>();
        creature.cardName = "New Creature";
        creature.description = "A new creature";
        creature.attack = 1;
        creature.health = 1;

        string path = EditorUtility.SaveFilePanelInProject("Save Creature", "NewCreature", "asset", "Create a new creature card");
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(creature, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = creature;
        }
    }

    [MenuItem("Cards/Create New Spell")]
    public static void CreateNewSpell()
    {
        SpellCardScriptableObject spell = ScriptableObject.CreateInstance<SpellCardScriptableObject>();
        spell.cardName = "New Spell";
        spell.description = "A new spell";
        spell.defaultTargetType = TargetType.EnemyCreatures;

        string path = EditorUtility.SaveFilePanelInProject("Save Spell", "NewSpell", "asset", "Create a new spell card");
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(spell, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = spell;
        }
    }

    [MenuItem("Cards/Create New Deck")]
    public static void CreateNewDeck()
    {
        DeckScriptableObject deck = ScriptableObject.CreateInstance<DeckScriptableObject>();

        string path = EditorUtility.SaveFilePanelInProject("Save Deck", "NewDeck", "asset", "Create a new deck");
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(deck, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = deck;
        }
    }

    [MenuItem("Cards/Create Sample Deck with Cards")]
    public static void CreateSampleDeck()
    {
        // Create a folder for the sample deck and cards
        string folderPath = "Assets/Cards/SampleDeck";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parentFolder = "Assets/Cards";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Cards");
            }
            AssetDatabase.CreateFolder(parentFolder, "SampleDeck");
        }

        // Create sample creatures
        List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();
        for (int i = 1; i <= 5; i++)
        {
            CreatureCardScriptableObject creature = ScriptableObject.CreateInstance<CreatureCardScriptableObject>();
            creature.cardName = $"Sample Creature {i}";
            creature.description = $"A sample creature with attack {i} and health {i+1}";
            creature.attack = i;
            creature.health = i + 1;

            string path = $"{folderPath}/SampleCreature{i}.asset";
            AssetDatabase.CreateAsset(creature, path);
            cards.Add(creature);
        }

        // Create sample spells
        for (int i = 1; i <= 3; i++)
        {
            SpellCardScriptableObject spell = ScriptableObject.CreateInstance<SpellCardScriptableObject>();
            spell.cardName = $"Sample Spell {i}";
            spell.description = $"A sample spell that deals {i} damage";
            spell.defaultTargetType = TargetType.EnemyCreatures;

            // Add a damage effect
            CardEffect effect = new CardEffect
            {
                effectType = EffectType.Immediate,
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction>
                {
                    new EffectAction
                    {
                        actionType = ActionType.Damage,
                        value = i,
                        targetType = TargetType.EnemyCreatures
                    }
                }
            };

            // Set the effect
            SerializedObject serializedSpell = new SerializedObject(spell);
            SerializedProperty effectsProp = serializedSpell.FindProperty("effects");
            effectsProp.ClearArray();
            effectsProp.arraySize = 1;
            SerializedProperty elementProp = effectsProp.GetArrayElementAtIndex(0);
            
            SerializedProperty triggerProp = elementProp.FindPropertyRelative("trigger");
            triggerProp.enumValueIndex = (int)effect.trigger;
            
            SerializedProperty typeProp = elementProp.FindPropertyRelative("effectType");
            typeProp.enumValueIndex = (int)effect.effectType;
            
            SerializedProperty actionsProp = elementProp.FindPropertyRelative("actions");
            actionsProp.ClearArray();
            actionsProp.arraySize = 1;
            
            SerializedProperty actionProp = actionsProp.GetArrayElementAtIndex(0);
            SerializedProperty actionTypeProp = actionProp.FindPropertyRelative("actionType");
            actionTypeProp.enumValueIndex = (int)effect.actions[0].actionType;
            
            SerializedProperty valueProp = actionProp.FindPropertyRelative("value");
            valueProp.intValue = effect.actions[0].value;
            
            SerializedProperty targetTypeProp = actionProp.FindPropertyRelative("targetType");
            targetTypeProp.enumValueIndex = (int)effect.actions[0].targetType;
            
            serializedSpell.ApplyModifiedProperties();

            string path = $"{folderPath}/SampleSpell{i}.asset";
            AssetDatabase.CreateAsset(spell, path);
            cards.Add(spell);
        }

        // Create the deck and add all cards to it
        DeckScriptableObject deck = ScriptableObject.CreateInstance<DeckScriptableObject>();
        string deckPath = $"{folderPath}/SampleDeck.asset";
        AssetDatabase.CreateAsset(deck, deckPath);

        // Add cards to the deck
        SerializedObject serializedDeck = new SerializedObject(deck);
        SerializedProperty cardsProp = serializedDeck.FindProperty("cards");
        cardsProp.ClearArray();
        cardsProp.arraySize = cards.Count;

        for (int i = 0; i < cards.Count; i++)
        {
            cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        }

        serializedDeck.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = deck;
        Debug.Log($"Sample deck created at {deckPath} with {cards.Count} cards");
    }
}
#endif