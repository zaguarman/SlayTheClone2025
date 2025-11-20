#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using static Enums;

public class ThemeDecksGenerator : EditorWindow {
    // Constants for paths
    private const string BASE_PATH = "Assets/Scriptables";
    private const string CARDS_PATH = BASE_PATH + "/Cards";
    private const string DECKS_PATH = BASE_PATH + "/Decks";
    
    private const string SPIDER_PATH = CARDS_PATH + "/Spiders";
    private const string BIRD_PATH = CARDS_PATH + "/Birds";
    private const string WATER_PATH = CARDS_PATH + "/Water";

    [MenuItem("Cards/Generate Theme Decks")]
    public static void GenerateThemeDecks() {
        // Create directories if they don't exist
        CreateDirectoryIfNeeded(BASE_PATH);
        CreateDirectoryIfNeeded(CARDS_PATH);
        CreateDirectoryIfNeeded(SPIDER_PATH);
        CreateDirectoryIfNeeded(BIRD_PATH);
        CreateDirectoryIfNeeded(WATER_PATH);
        CreateDirectoryIfNeeded(DECKS_PATH);

        // Generate Spider Cards
        List<CardDataScriptableObject> spiderCards = GenerateSpiderCards();

        // Generate Bird Cards
        List<CardDataScriptableObject> birdCards = GenerateBirdCards();

        // Create Spider Deck
        CreateDeck("SpiderDeck", spiderCards, Path.Combine(DECKS_PATH, "SpiderDeck.asset"));

        // Create Bird Deck
        CreateDeck("BirdDeck", birdCards, Path.Combine(DECKS_PATH, "BirdDeck.asset"));

        // Generate Water Cards
        List<CardDataScriptableObject> waterCards = GenerateWaterCards();

        // Create Water Deck
        CreateDeck("WaterDeck", waterCards, Path.Combine(DECKS_PATH, "WaterDeck.asset"));

        AssetDatabase.SaveAssets();
        Debug.Log("Generated Spider, Bird, and Water decks successfully!");
    }

    private static void CreateDirectoryIfNeeded(string path) {
        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    private static List<CardDataScriptableObject> GenerateSpiderCards() {
        List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();
        Dictionary<string, string> existingCardIds = GatherExistingCardIds(SPIDER_PATH);

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Venomous Spider",
            "A small but deadly spider whose poison weakens enemies over time.",
            SPIDER_PATH, existingCardIds,
            c => {
                c.attack = 2;
                c.health = 2;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDamage,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 1, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Web Weaver",
            "Spins strong webs that immobilize attackers.",
            SPIDER_PATH, existingCardIds,
            c => {
                c.attack = 1;
                c.health = 3;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 1, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Shadow Lurker",
            "A stealthy spider that ambushes from the shadows.",
            SPIDER_PATH, existingCardIds,
            c => {
                c.attack = 3;
                c.health = 1;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 2, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Broodmother",
            "Spawns smaller spiders when threatened.",
            SPIDER_PATH, existingCardIds,
            c => {
                c.attack = 2;
                c.health = 4;
            },
            new CardEffectData {
                trigger = EffectTrigger.EndOfTurn,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Draw, value = 1, targetType = TargetType.Player }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Tunnel Spider",
            "Creates elaborate tunnel networks to ambush prey.",
            SPIDER_PATH, existingCardIds,
            c => {
                c.attack = 4;
                c.health = 2;
            },
            new CardEffectData {
                trigger = EffectTrigger.StartOfTurn,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 1, targetType = TargetType.Enemy }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Silk Spinner",
            "Its valuable silk can heal allies.",
            SPIDER_PATH, existingCardIds,
            c => {
                c.attack = 1;
                c.health = 2;
            },
            new CardEffectData {
                trigger = EffectTrigger.StartOfTurn,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Heal, value = 1, targetType = TargetType.FriendlyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Giant Tarantula",
            "This massive spider crushes opponents with brute force.",
            SPIDER_PATH, existingCardIds,
            c => {
                c.attack = 5;
                c.health = 5;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDamage,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 2, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Widow Assassin",
            "Its deadly venom can take down creatures many times its size.",
            SPIDER_PATH, existingCardIds,
            c => {
                c.attack = 3;
                c.health = 2;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 3, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        return cards;
    }

    private static List<CardDataScriptableObject> GenerateBirdCards() {
        List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();
        Dictionary<string, string> existingCardIds = GatherExistingCardIds(BIRD_PATH);

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Royal Falcon",
            "A noble bird trained for hunting, with exceptional speed.",
            BIRD_PATH, existingCardIds,
            c => {
                c.attack = 3;
                c.health = 2;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 1, targetType = TargetType.AllCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Wise Owl",
            "Its wisdom allows allies to find new strategies.",
            BIRD_PATH, existingCardIds,
            c => {
                c.attack = 2;
                c.health = 3;
            },
            new CardEffectData {
                trigger = EffectTrigger.EndOfTurn,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Draw, value = 1, targetType = TargetType.Player }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Thunderhawk",
            "Calls lightning down upon enemies.",
            BIRD_PATH, existingCardIds,
            c => {
                c.attack = 4;
                c.health = 3;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 2, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Healing Dove",
            "Its gentle presence mends wounds.",
            BIRD_PATH, existingCardIds,
            c => {
                c.attack = 1;
                c.health = 2;
            },
            new CardEffectData {
                trigger = EffectTrigger.StartOfTurn,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Heal, value = 2, targetType = TargetType.Player }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "War Eagle",
            "A battle-hardened bird that leads the charge in combat.",
            BIRD_PATH, existingCardIds,
            c => {
                c.attack = 5;
                c.health = 4;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDamage,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 2, targetType = TargetType.Enemy }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Mischievous Raven",
            "Steals valuable items to aid its allies.",
            BIRD_PATH, existingCardIds,
            c => {
                c.attack = 2;
                c.health = 1;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Draw, value = 2, targetType = TargetType.Player }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Phoenix Hatchling",
            "Though young, it carries the flame of rebirth.",
            BIRD_PATH, existingCardIds,
            c => {
                c.attack = 1;
                c.health = 3;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDeath,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 2, targetType = TargetType.AllCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Majestic Griffin",
            "Half eagle, half lion, all power.",
            BIRD_PATH, existingCardIds,
            c => {
                c.attack = 4;
                c.health = 5;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 1, targetType = TargetType.EnemyCreatures },
                    new EffectActionData { actionType = ActionType.Heal, value = 1, targetType = TargetType.FriendlyCreatures }
                }
            }
        ));

        return cards;
    }

    private static List<CardDataScriptableObject> GenerateWaterCards() {
        List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();
        Dictionary<string, string> existingCardIds = GatherExistingCardIds(WATER_PATH);

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Dancing Dolphin",
            "Playful movements boost ally morale.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 2;
                c.health = 3;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Draw, value = 1, targetType = TargetType.Player }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Guardian Whale",
            "Massive presence provides permanent protection to allies.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 1;
                c.health = 6;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDamage,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Armor, value = 2, targetType = TargetType.Self, statusDuration = 0 }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Tidepool Star",
            "Regenerates during low tide.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 1;
                c.health = 2;
            },
            new CardEffectData {
                trigger = EffectTrigger.EndOfTurn,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Heal, value = 2, targetType = TargetType.Self }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Jellyfish Swarm",
            "Paralyzes enemies with gentle pulses when destroyed.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 0;
                c.health = 4;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDeath,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.ApplyStatus, targetType = TargetType.EnemyCreatures, statusEffectToApply = StatusEffectType.Paralyzed, statusDuration = 1, statusPotency = 0 }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Coral Builder",
            "Constructs permanent protective barriers for allies.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 0;
                c.health = 3;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Armor, value = 3, targetType = TargetType.Self, statusDuration = 0 }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Abyssal Cucumber",
            "Gives all friendly creatures +2 attack for 2 turns.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 1;
                c.health = 4;
            },
            new CardEffectData {
                trigger = EffectTrigger.StartOfTurn,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.ModifyStat, value = 2, targetType = TargetType.FriendlyCreatures, modifyAttack = true, modifyHealth = false }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Crashing Wave",
            "A powerful wave that crashes onto enemies.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 3;
                c.health = 4;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 2, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Deep Sea Angler",
            "Lures enemies with bioluminescent bait, paralyzing them.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 2;
                c.health = 5;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDamage,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.ApplyStatus, targetType = TargetType.Enemy, statusEffectToApply = StatusEffectType.Paralyzed, statusDuration = 1, statusPotency = 0 }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Electric Eel",
            "When attacking, zaps the primary target and chains lightning to 2 additional targets in a random direction.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 4;
                c.health = 3;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDamage,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 4, targetType = TargetType.EnemyCreatures, targetModifier = TargetModifier.Chained }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Kraken",
            "Massive sea monster that terrifies enemies.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 6;
                c.health = 8;
            },
            new CardEffectData {
                trigger = EffectTrigger.StartOfTurn,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 3, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Mermaid Healer",
            "Restores health to damaged allies at the start of your turn.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 1;
                c.health = 4;
            },
            new CardEffectData {
                trigger = EffectTrigger.StartOfTurn,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Heal, value = 2, targetType = TargetType.FriendlyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Octopus Defender",
            "Uses tentacles to create permanent armor for itself when damaged.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 0;
                c.health = 7;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDamage,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Armor, value = 2, targetType = TargetType.Self, statusDuration = 0 }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Seahorse Scout",
            "Quickly discovers new resources.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 1;
                c.health = 1;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Draw, value = 1, targetType = TargetType.Player }
                }
            }
        ));

        cards.Add(CreateCard<CreatureCardScriptableObject>(
            "Tsunami Turtle",
            "Creates massive waves when attacking.",
            WATER_PATH, existingCardIds,
            c => {
                c.attack = 4;
                c.health = 6;
            },
            new CardEffectData {
                trigger = EffectTrigger.OnDamage,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 1, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        // Water Spells
        cards.Add(CreateCard<SpellCardScriptableObject>(
            "Tidal Surge",
            "Wash away enemy defenses.",
            WATER_PATH, existingCardIds,
            s => s.defaultTargetType = (Enums.TargetType)TargetType.EnemyCreatures,
            new CardEffectData {
                effectType = EffectType.Instantaneous,
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Damage, value = 4, targetType = TargetType.EnemyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<SpellCardScriptableObject>(
            "Whirlpool",
            "Trap enemies in a spinning vortex, paralyzing them for 2 turns.",
            WATER_PATH, existingCardIds,
            s => s.defaultTargetType = (Enums.TargetType)TargetType.EnemyCreatures,
            new CardEffectData {
                effectType = EffectType.Instantaneous,
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.ApplyStatus, targetType = TargetType.EnemyCreatures, statusEffectToApply = StatusEffectType.Paralyzed, statusDuration = 2, statusPotency = 0 }
                }
            }
        ));

        cards.Add(CreateCard<SpellCardScriptableObject>(
            "Healing Rain",
            "Restore health to all friendly creatures.",
            WATER_PATH, existingCardIds,
            s => s.defaultTargetType = (Enums.TargetType)TargetType.FriendlyCreatures,
            new CardEffectData {
                effectType = EffectType.Instantaneous,
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Heal, value = 3, targetType = TargetType.FriendlyCreatures }
                }
            }
        ));

        cards.Add(CreateCard<SpellCardScriptableObject>(
            "Coral Shield",
            "Create temporary armor for allies.",
            WATER_PATH, existingCardIds,
            s => s.defaultTargetType = (Enums.TargetType)TargetType.FriendlyCreatures,
            new CardEffectData {
                effectType = EffectType.Instantaneous,
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Armor, value = 4, targetType = TargetType.FriendlyCreatures, statusDuration = 2 }
                }
            }
        ));

        cards.Add(CreateCard<SpellCardScriptableObject>(
            "Abyssal Call",
            "Summon a powerful deep sea creature.",
            WATER_PATH, existingCardIds,
            s => s.defaultTargetType = (Enums.TargetType)TargetType.Player,
            new CardEffectData {
                effectType = EffectType.Instantaneous,
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectActionData> {
                    new EffectActionData { actionType = ActionType.Summon, value = 1, targetType = TargetType.Player }
                }
            }
        ));

        return cards;
    }

    private static Dictionary<string, string> GatherExistingCardIds(string folderPath) {
        Dictionary<string, string> cardIds = new Dictionary<string, string>();

        if (!Directory.Exists(folderPath)) return cardIds;

        // Find all card scriptable objects in the folder
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { folderPath });

        foreach (string guid in guids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var cardData = AssetDatabase.LoadAssetAtPath<CardData>(path);

            if (cardData != null) {
                string cardNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(cardData.cardId)) {
                    cardIds[cardNameWithoutExtension] = cardData.cardId;
                }
            }
        }

        return cardIds;
    }

    private static T CreateCard<T>(
        string name,
        string description,
        string folderPath,
        Dictionary<string, string> existingIds,
        System.Action<T> initializer,
        CardEffectData effectData) where T : CardDataScriptableObject {

        string safeName = name.Replace(" ", "");
        string path = Path.Combine(folderPath, safeName + ".asset");
        string cardId = existingIds.ContainsKey(safeName) ? existingIds[safeName] : System.Guid.NewGuid().ToString();

        T card = AssetDatabase.LoadAssetAtPath<T>(path);
        bool isNew = card == null;

        if (isNew) {
            card = ScriptableObject.CreateInstance<T>();
            Debug.Log($"Creating new card: {name} with ID: {cardId}");
        } else {
            Debug.Log($"Updating existing card: {name} with ID: {cardId}");
        }

        card.cardName = name;
        card.description = description;
        card.cardId = cardId;

        initializer(card);
        UpdateCardEffects(card, effectData);

        if (isNew) {
            AssetDatabase.CreateAsset(card, path);
        } else {
            EditorUtility.SetDirty(card);
            AssetDatabase.SaveAssetIfDirty(card);
        }

        return card;
    }

    private static void CreateDeck(string name, List<CardDataScriptableObject> cards, string path) {
        DeckScriptableObject deck = AssetDatabase.LoadAssetAtPath<DeckScriptableObject>(path);
        bool isNew = deck == null;

        if (isNew) {
            deck = ScriptableObject.CreateInstance<DeckScriptableObject>();
            Debug.Log($"Creating new deck: {name}");
        } else {
            Debug.Log($"Updating existing deck: {name}");
        }

        SerializedObject serializedDeck = new SerializedObject(deck);

        serializedDeck.FindProperty("deckName").stringValue = name;
        
        SerializedProperty cardsProp = serializedDeck.FindProperty("cards");
        cardsProp.ClearArray();
        cardsProp.arraySize = cards.Count;

        for (int i = 0; i < cards.Count; i++) {
            cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        }

        serializedDeck.ApplyModifiedProperties();
        
        if (isNew) {
            AssetDatabase.CreateAsset(deck, path);
        } else {
            EditorUtility.SetDirty(deck);
        }
        
        Debug.Log($"Deck {name} now contains {cards.Count} cards");
    }

    private static void UpdateCardEffects(CardDataScriptableObject card, CardEffectData effectData) {
        SerializedObject serializedCard = new SerializedObject(card);
        SerializedProperty effectsProp = serializedCard.FindProperty("effects");
        effectsProp.ClearArray();
        effectsProp.arraySize = 1;

        SerializedProperty effectProp = effectsProp.GetArrayElementAtIndex(0);

        effectProp.FindPropertyRelative("trigger").enumValueIndex = (int)effectData.trigger;
        effectProp.FindPropertyRelative("effectType").enumValueIndex = (int)effectData.effectType;

        SerializedProperty actionsProp = effectProp.FindPropertyRelative("actions");
        actionsProp.ClearArray();
        actionsProp.arraySize = effectData.actions.Count;

        for (int i = 0; i < effectData.actions.Count; i++) {
            SerializedProperty actionProp = actionsProp.GetArrayElementAtIndex(i);
            EffectActionData actionData = effectData.actions[i];

            actionProp.FindPropertyRelative("actionType").enumValueIndex = (int)actionData.actionType;
            actionProp.FindPropertyRelative("value").intValue = actionData.value;
            actionProp.FindPropertyRelative("targetType").enumValueIndex = (int)actionData.targetType;
            actionProp.FindPropertyRelative("targetModifier").intValue = (int)actionData.targetModifier;

            // Optional fields
            SerializedProperty modifyAttackProp = actionProp.FindPropertyRelative("modifyAttack");
            if (modifyAttackProp != null) modifyAttackProp.boolValue = actionData.modifyAttack;

            SerializedProperty modifyHealthProp = actionProp.FindPropertyRelative("modifyHealth");
            if (modifyHealthProp != null) modifyHealthProp.boolValue = actionData.modifyHealth;
            
            SerializedProperty modifySpeedProp = actionProp.FindPropertyRelative("modifySpeed");
            if (modifySpeedProp != null) modifySpeedProp.boolValue = actionData.modifySpeed;

            SerializedProperty statusTypeProp = actionProp.FindPropertyRelative("statusEffectToApply");
            if (statusTypeProp != null) statusTypeProp.enumValueIndex = (int)actionData.statusEffectToApply;

            SerializedProperty statusDurationProp = actionProp.FindPropertyRelative("statusDuration");
            if (statusDurationProp != null) statusDurationProp.intValue = actionData.statusDuration;

            SerializedProperty statusPotencyProp = actionProp.FindPropertyRelative("statusPotency");
            if (statusPotencyProp != null) statusPotencyProp.intValue = actionData.statusPotency;
        }

        serializedCard.ApplyModifiedProperties();
    }

    public enum EffectType {
        Triggered,
        Instantaneous
    }

    public enum EffectTrigger {
        OnPlay,
        OnDamage,
        StartOfTurn,
        EndOfTurn,
        OnDeath
    }

    public enum TargetType {
        Player,
        Enemy,
        Self,
        FriendlyCreatures,
        EnemyCreatures,
        AllCreatures
    }

    [System.Serializable]
    public class CardEffectData {
        public EffectType effectType = EffectType.Triggered;
        public EffectTrigger trigger;
        public List<EffectActionData> actions = new List<EffectActionData>();
    }

    [System.Serializable]
    public class EffectActionData {
        public ActionType actionType;
        public int value;
        public TargetType targetType;
        public TargetModifier targetModifier = TargetModifier.None;
        
        public bool modifyAttack = true;
        public bool modifyHealth = true;
        public bool modifySpeed = false;

        public StatusEffectType statusEffectToApply = StatusEffectType.None;
        public int statusDuration = 0;
        public int statusPotency = 0;
    }
}
#endif