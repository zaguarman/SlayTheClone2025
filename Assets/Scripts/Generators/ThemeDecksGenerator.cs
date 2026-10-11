#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class ThemeDecksGenerator : EditorWindow {
        [MenuItem("Cards/Generate Theme Decks")]
        public static void GenerateThemeDecks() {
            // Create directories if they don't exist
            CreateDirectoryIfNeeded("Assets/Scriptables");
            CreateDirectoryIfNeeded("Assets/Scriptables/Cards");
            CreateDirectoryIfNeeded("Assets/Scriptables/Cards/Water");
            CreateDirectoryIfNeeded("Assets/Scriptables/Decks");

            // Generate Water Cards
            List<CardDataScriptableObject> waterCards = GenerateWaterCards();

            // Create Water Deck
            CreateDeck("Water", waterCards, "Assets/Scriptables/Decks/WaterDeck.asset");

            AssetDatabase.SaveAssets();
            Debug.Log("Generated the Water deck successfully!");
        }

        private static void CreateDirectoryIfNeeded(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private static List<CardDataScriptableObject> GenerateWaterCards() {
            List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();
            Dictionary<string, string> existingCardIds = GatherExistingCardIds("Assets/Scriptables/Cards/Water");

            // 1. Dancing Dolphin
            cards.Add(CreateCreatureCard(
                "Dancing Dolphin",
                "Playful movements boost ally morale.",
                2, 3, speed: 3, turnsUntilOperative: 1,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnPlay,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Draw, 1, TargetType.Player)
                    }),
                "Assets/Scriptables/Cards/Water/DancingDolphin.asset",
                ExistingOrNewId(existingCardIds, "DancingDolphin")
            ));

            // 2. Guardian Whale (with permanent armor)
            cards.Add(CreateCreatureCard(
                "Guardian Whale",
                "Massive presence provides permanent protection to allies.",
                1, 5, speed: 1, turnsUntilOperative: 1,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnDeath,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Armor, 2, TargetType.AllCreatures, 0) // Duration is ignored
                    }),
                "Assets/Scriptables/Cards/Water/GuardianWhale.asset",
                ExistingOrNewId(existingCardIds, "GuardianWhale")
            ));

            // 3. Tidepool Star
            cards.Add(CreateCreatureCard(
                "Tidepool Star",
                "Regenerates during low tide.",
                1, 2, speed: 1, turnsUntilOperative: 0,
                new CardEffectData(EffectType.Immediate, EffectTrigger.StartOfTurn,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Heal, 2, TargetType.AllCreatures)
                    }),
                "Assets/Scriptables/Cards/Water/TidepoolStar.asset",
                ExistingOrNewId(existingCardIds, "TidepoolStar")
            ));

            // 4. Jellyfish Swarm
            cards.Add(CreateCreatureCard(
                "Jellyfish Swarm",
                "Paralyzes enemies with gentle pulses when destroyed.", // Updated description
                1, 3, speed: 1, turnsUntilOperative: 0,
                new CardEffectData(EffectType.Triggered, EffectTrigger.OnDeath,
                    new List<EffectActionData> {
                        // Use the new ApplyStatus constructor
                        new EffectActionData(ActionType.ApplyStatus, TargetType.EnemyCreatures, StatusEffectType.Paralyzed, 1, 0) // 1 turn duration, 0 potency
                    }),
                "Assets/Scriptables/Cards/Water/JellyfishSwarm.asset",
                ExistingOrNewId(existingCardIds, "JellyfishSwarm")
            ));

            // 5. Coral Builder (with permanent armor)
            cards.Add(CreateCreatureCard(
                "Coral Builder",
                "Constructs permanent protective barriers for allies.",
                1, 3, speed: 1, turnsUntilOperative: 0,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnPlay,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Armor, 2, TargetType.AllCreatures, 0) // Duration is ignored
                    }),
                "Assets/Scriptables/Cards/Water/CoralBuilder.asset",
                ExistingOrNewId(existingCardIds, "CoralBuilder")
            ));

            // 6. Abyssal Cucumber
            cards.Add(CreateCreatureCard(
                "Abyssal Cucumber",
                "Gives all friendly creatures +2 attack for 2 turns.",
                1, 4, speed: 1, turnsUntilOperative: 1,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnDamage,
                    new List<EffectActionData> {
                        // Example: Use the constructor for ModifyStat actions
                        new EffectActionData(ActionType.ModifyStat, 2, TargetType.FriendlyCreatures, true, false) // Modify Attack, not Health
                    }),
                "Assets/Scriptables/Cards/Water/AbyssalCucumber.asset",
                ExistingOrNewId(existingCardIds, "AbyssalCucumber")
            ));

            // 7. Crashing Wave
            cards.Add(CreateCreatureCard(
                "Crashing Wave",
                "A powerful wave that crashes onto enemies.",
                3, 3, speed: 2, turnsUntilOperative: 1,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnPlay,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Damage, 1, TargetType.EnemyCreatures)
                    }),
                "Assets/Scriptables/Cards/Water/CrashingWave.asset",
                ExistingOrNewId(existingCardIds, "CrashingWave")
            ));

            // 8. Deep Sea Angler
            cards.Add(CreateCreatureCard(
                "Deep Sea Angler",
                "Lures enemies with bioluminescent bait, paralyzing them.",
                2, 5, speed: 2, turnsUntilOperative: 1,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnDeath,
                    new List<EffectActionData> {
                        // Use the new ApplyStatus constructor
                        new EffectActionData(ActionType.ApplyStatus, TargetType.Enemy, StatusEffectType.Paralyzed, 1, 0) // 1 turn duration, 0 potency
                    }),
                "Assets/Scriptables/Cards/Water/DeepSeaAngler.asset",
                ExistingOrNewId(existingCardIds, "DeepSeaAngler")
            ));

            // 9. Electric Eel
            cards.Add(CreateCreatureCard(
                "Electric Eel",
                "When attacking, zaps the primary target and chains lightning to 2 additional targets in a random direction.",
                3, 3, speed: 3, turnsUntilOperative: 1,
                new CardEffectData(EffectType.Triggered, EffectTrigger.OnDamage,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Damage, 3, TargetType.EnemyCreatures, TargetModifier.Chained)
                    }),
                "Assets/Scriptables/Cards/Water/ElectricEel.asset",
                ExistingOrNewId(existingCardIds, "ElectricEel")
            ));

            // 10. Kraken
            cards.Add(CreateCreatureCard(
                "Kraken",
                "Massive sea monster that terrifies enemies.",
                5, 6, speed: 1, turnsUntilOperative: 2,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnDamage,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Damage, 2, TargetType.EnemyCreatures)
                    }),
                "Assets/Scriptables/Cards/Water/Kraken.asset",
                ExistingOrNewId(existingCardIds, "Kraken")
            ));

            // 11. Mermaid Healer
            cards.Add(CreateCreatureCard(
                "Mermaid Healer",
                "Restores health to damaged allies at the start of your turn.",
                1, 4, speed: 2, turnsUntilOperative: 1,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnDamage,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Heal, 2, TargetType.FriendlyCreatures)
                    }),
                "Assets/Scriptables/Cards/Water/MermaidHealer.asset",
                ExistingOrNewId(existingCardIds, "MermaidHealer")
            ));

            // 12. Octopus Defender (with permanent armor)
            cards.Add(CreateCreatureCard(
                "Octopus Defender",
                "Uses tentacles to create permanent armor for itself when damaged.",
                1, 6, speed: 1, turnsUntilOperative: 1,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnDeath,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Armor, 2, TargetType.AllCreatures, 0) // Duration is ignored
                    }),
                "Assets/Scriptables/Cards/Water/OctopusDefender.asset",
                ExistingOrNewId(existingCardIds, "OctopusDefender")
            ));

            // 13. Seahorse Scout
            cards.Add(CreateCreatureCard(
                "Seahorse Scout",
                "Quickly discovers new resources.",
                1, 2, speed: 3, turnsUntilOperative: 0,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnPlay,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Draw, 1, TargetType.Player)
                    }),
                "Assets/Scriptables/Cards/Water/SeahorseScout.asset",
                ExistingOrNewId(existingCardIds, "SeahorseScout")
            ));

            // 14. Tsunami Turtle
            cards.Add(CreateCreatureCard(
                "Tsunami Turtle",
                "Creates massive waves when attacking.",
                3, 5, speed: 1, turnsUntilOperative: 2,
                new CardEffectData(EffectType.Immediate, EffectTrigger.OnDeath,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Damage, 1, TargetType.EnemyCreatures)
                    }),
                "Assets/Scriptables/Cards/Water/TsunamiTurtle.asset",
                ExistingOrNewId(existingCardIds, "TsunamiTurtle")
            ));

            // Water Spells
            cards.AddRange(new List<CardDataScriptableObject> {
                CreateWaterSpell(
                    "Tidal Surge",
                    "Wash away enemy defenses.",
                    TargetType.EnemyCreatures,
                    new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                        new List<EffectActionData> {
                            new EffectActionData(ActionType.Damage, 3, TargetType.EnemyCreatures)
                        }),
                    "Assets/Scriptables/Cards/Water/TidalSurge.asset",
                    ExistingOrNewId(existingCardIds, "TidalSurge")
                ),
                CreateWaterSpell(
                    "Whirlpool",
                    "Trap enemies in a spinning vortex, paralyzing them for 2 turns.",
                    TargetType.EnemyCreatures,
                    new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                        new List<EffectActionData> {
                            // Use the new ApplyStatus constructor
                            new EffectActionData(ActionType.ApplyStatus, TargetType.EnemyCreatures, StatusEffectType.Paralyzed, 2, 0) // 2 turn duration, 0 potency
                        }),
                    "Assets/Scriptables/Cards/Water/Whirlpool.asset",
                    ExistingOrNewId(existingCardIds, "Whirlpool")
                ),
                CreateWaterSpell(
                    "Healing Rain",
                    "Restore health to all friendly creatures.",
                    TargetType.FriendlyCreatures,
                    new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                        new List<EffectActionData> {
                            new EffectActionData(ActionType.Heal, 3, TargetType.FriendlyCreatures)
                        }),
                    "Assets/Scriptables/Cards/Water/HealingRain.asset",
                    ExistingOrNewId(existingCardIds, "HealingRain")
                ),
                CreateWaterSpell(
                    "Coral Shield",
                    "Create temporary armor for allies.",
                    TargetType.FriendlyCreatures,
                    new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                        new List<EffectActionData> {
                            // Use the new Armor constructor with duration
                            new EffectActionData(ActionType.Armor, 3, TargetType.FriendlyCreatures, 2)
                        }),
                    "Assets/Scriptables/Cards/Water/CoralShield.asset",
                    ExistingOrNewId(existingCardIds, "CoralShield")
                ),
                CreateWaterSpell(
                    "Abyssal Call",
                    "Summon a powerful deep sea creature.",
                    TargetType.Player,
                    new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                        new List<EffectActionData> {
                            new EffectActionData(ActionType.Summon, 1, TargetType.Player)
                        }),
                    "Assets/Scriptables/Cards/Water/AbyssalCall.asset",
                    ExistingOrNewId(existingCardIds, "AbyssalCall")
                )
            });

            return cards;
        }

        private static SpellCardScriptableObject CreateWaterSpell(
            string name,
            string description,
            TargetType defaultTargetType,
            CardEffectData effectData,
            string path,
            string cardId) {

            SpellCardScriptableObject spell = AssetDatabase.LoadAssetAtPath<SpellCardScriptableObject>(path);
            if (spell != null) {
                spell.cardName = name;
                spell.description = description;
                spell.defaultTargetType = defaultTargetType;
                WriteEffects(spell, effectData);
                EditorUtility.SetDirty(spell);
                return spell;
            }

            spell = ScriptableObject.CreateInstance<SpellCardScriptableObject>();
            spell.cardName = name;
            spell.description = description;
            spell.defaultTargetType = defaultTargetType;
            spell.cardId = cardId;
            WriteEffects(spell, effectData);
            AssetDatabase.CreateAsset(spell, path);
            return spell;
        }

        // Keeps a regenerated card's id, keyed by its asset file name, so references to it stay valid
        private static string ExistingOrNewId(Dictionary<string, string> existingCardIds, string assetName) {
            return existingCardIds.TryGetValue(assetName, out var id) ? id : System.Guid.NewGuid().ToString();
        }

        private static Dictionary<string, string> GatherExistingCardIds(string folderPath) {
            Dictionary<string, string> cardIds = new Dictionary<string, string>();

            if (!Directory.Exists(folderPath)) return cardIds;

            // Find all card scriptable objects in the folder
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { folderPath });

            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var cardData = AssetDatabase.LoadAssetAtPath<CardDataScriptableObject>(path);

                if (cardData != null) {
                    string cardNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                    // If cardId is empty, we'll generate a new one later
                    if (!string.IsNullOrEmpty(cardData.cardId)) {
                        cardIds[cardNameWithoutExtension] = cardData.cardId;
                        Debug.Log($"Found existing card {cardNameWithoutExtension} with ID: {cardData.cardId}");
                    }
                    else {
                        Debug.Log($"Found existing card {cardNameWithoutExtension} with no ID");
                    }
                }
            }

            return cardIds;
        }

        private static CreatureCardScriptableObject CreateCreatureCard(
            string name,
            string description,
            int attack,
            int health,
            int speed,
            int turnsUntilOperative,
            CardEffectData effectData,
            string path,
            string cardId) {
            // Check if the card already exists
            CreatureCardScriptableObject card = null;
            if (File.Exists(path)) {
                card = AssetDatabase.LoadAssetAtPath<CreatureCardScriptableObject>(path);
                if (card != null) {
                    Debug.Log($"Updating existing card: {name} with ID: {card.cardId}");

                    // Update existing card properties
                    card.cardName = name;
                    card.description = description;
                    card.attack = attack;
                    card.health = health;
                    card.speed = speed;
                    card.turnsUntilOperative = turnsUntilOperative;

                    // Keep the existing cardId if it exists, otherwise set the new one
                    if (string.IsNullOrEmpty(card.cardId)) {
                        card.cardId = cardId;
                        Debug.Log($"Setting new ID for {name}: {cardId}");
                    }
                    else {
                        Debug.Log($"Keeping existing ID for {name}: {card.cardId}");
                    }

                    // Clear existing effects and add new ones
                    WriteEffects(card, effectData);

                    EditorUtility.SetDirty(card);
                    AssetDatabase.SaveAssetIfDirty(card);
                    return card;
                }
            }

            // If card doesn't exist or couldn't be loaded, create a new one
            Debug.Log($"Creating new card: {name} with ID: {cardId}");
            card = ScriptableObject.CreateInstance<CreatureCardScriptableObject>();
            card.cardName = name;
            card.description = description;
            card.attack = attack;
            card.health = health;
            card.speed = speed;
            card.turnsUntilOperative = turnsUntilOperative;
            card.cardId = cardId; // Set the card ID

            WriteEffects(card, effectData);

            AssetDatabase.CreateAsset(card, path);
            return card;
        }

        private static void CreateDeck(string name, List<CardDataScriptableObject> cards, string path) {
            DeckScriptableObject deck = null;

            // Check if the deck already exists
            if (File.Exists(path)) {
                deck = AssetDatabase.LoadAssetAtPath<DeckScriptableObject>(path);
                Debug.Log($"Updating existing deck: {name}");
            }

            // If it doesn't exist, create a new one
            if (deck == null) {
                deck = ScriptableObject.CreateInstance<DeckScriptableObject>();
                Debug.Log($"Creating new deck: {name}");
                AssetDatabase.CreateAsset(deck, path);
            }

            // Set the cards in the deck
            SerializedObject serializedDeck = new SerializedObject(deck);

            // Set deck name
            SerializedProperty nameProp = serializedDeck.FindProperty("deckName");
            nameProp.stringValue = name;

            // Set cards array
            SerializedProperty cardsProp = serializedDeck.FindProperty("cards");
            cardsProp.ClearArray();
            cardsProp.arraySize = cards.Count;

            for (int i = 0; i < cards.Count; i++) {
                cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            serializedDeck.ApplyModifiedProperties();
            EditorUtility.SetDirty(deck);
            Debug.Log($"Deck {name} now contains {cards.Count} cards");
        }

        // Overwrites the card's serialized effects with the given one
        private static void WriteEffects(CardDataScriptableObject card, CardEffectData effectData) {
            SerializedObject serializedCard = new SerializedObject(card);
            SerializedProperty effectsProp = serializedCard.FindProperty("effects");
            effectsProp.ClearArray();
            effectsProp.arraySize = 1;

            SerializedProperty effectProp = effectsProp.GetArrayElementAtIndex(0);

            SerializedProperty triggerProp = effectProp.FindPropertyRelative("trigger");
            triggerProp.enumValueIndex = (int)effectData.trigger;

            SerializedProperty typeProp = effectProp.FindPropertyRelative("effectType");
            typeProp.enumValueIndex = (int)effectData.effectType;

            SerializedProperty actionsProp = effectProp.FindPropertyRelative("actions");
            actionsProp.ClearArray();
            actionsProp.arraySize = effectData.actions.Count;

            for (int i = 0; i < effectData.actions.Count; i++) {
                SerializedProperty actionProp = actionsProp.GetArrayElementAtIndex(i);

                SerializedProperty actionTypeProp = actionProp.FindPropertyRelative("actionType");
                actionTypeProp.enumValueIndex = (int)effectData.actions[i].actionType;

                SerializedProperty valueProp = actionProp.FindPropertyRelative("value");
                valueProp.intValue = effectData.actions[i].value;

                SerializedProperty targetTypeProp = actionProp.FindPropertyRelative("targetType");
                targetTypeProp.enumValueIndex = (int)effectData.actions[i].targetType;

                SerializedProperty targetModifierProp = actionProp.FindPropertyRelative("targetModifier");
                targetModifierProp.intValue = (int)effectData.actions[i].targetModifier;

                // Store buff-specific properties if this is a buff action
                if (effectData.actions[i].actionType == ActionType.ModifyStat) {
                    // Check if the properties exist in the serialized object
                    SerializedProperty modifyAttackProp = actionProp.FindPropertyRelative("modifyAttack");
                    SerializedProperty modifyHealthProp = actionProp.FindPropertyRelative("modifyHealth");
                    SerializedProperty modifySpeedProp = actionProp.FindPropertyRelative("modifySpeed");

                    if (modifyAttackProp != null && modifyHealthProp != null) {
                        modifyAttackProp.boolValue = effectData.actions[i].modifyAttack;
                        modifyHealthProp.boolValue = effectData.actions[i].modifyHealth;
                        if (modifySpeedProp != null) modifySpeedProp.boolValue = effectData.actions[i].modifySpeed; // Ensure speed is copied
                    }
                }

                // --- Add serialization for ApplyStatus fields ---
                if (effectData.actions[i].actionType == ActionType.ApplyStatus) {
                    SerializedProperty statusTypeProp = actionProp.FindPropertyRelative("statusEffectToApply");
                    SerializedProperty statusDurationProp = actionProp.FindPropertyRelative("statusDuration");
                    SerializedProperty statusPotencyProp = actionProp.FindPropertyRelative("statusPotency");

                    if (statusTypeProp != null) statusTypeProp.enumValueIndex = (int)effectData.actions[i].statusEffectToApply;
                    if (statusDurationProp != null) statusDurationProp.intValue = effectData.actions[i].statusDuration;
                    if (statusPotencyProp != null) statusPotencyProp.intValue = effectData.actions[i].statusPotency;
                }
                // --- End Add ---
            }

            serializedCard.ApplyModifiedProperties();
        }

        [System.Serializable]
        public class CardEffectData {
            public EffectType effectType;
            public EffectTrigger trigger;
            public List<EffectActionData> actions;

            public CardEffectData(EffectType effectType, EffectTrigger trigger, List<EffectActionData> actions) {
                this.effectType = effectType;
                this.trigger = trigger;
                this.actions = actions;
            }
        }

        // Helper class to define effect actions
        public class EffectActionData {
            public ActionType actionType;
            public int value;
            public TargetType targetType;
            public TargetModifier targetModifier = TargetModifier.None;
            // ModifyStat Specific
            public bool modifyAttack = true;
            public bool modifyHealth = true;
            public bool modifySpeed = false;
            // --- ApplyStatus Specific ---
            public StatusEffectType statusEffectToApply = StatusEffectType.None;
            public int statusDuration = 0;
            public int statusPotency = 0;

            // Constructor for basic actions
            public EffectActionData(ActionType actionType, int value, TargetType targetType) {
                this.actionType = actionType; this.value = value; this.targetType = targetType;
            }
            // Constructor for ModifyStat actions
            public EffectActionData(ActionType actionType, int value, TargetType targetType, bool modifyAttack, bool modifyHealth) {
                if (actionType != ActionType.ModifyStat) Debug.LogError("Incorrect constructor used for non-ModifyStat action");
                this.actionType = actionType; this.value = value; this.targetType = targetType;
                this.modifyAttack = modifyAttack; this.modifyHealth = modifyHealth;
                this.modifySpeed = false; // Default to false for speed
            }

            // Constructor for ModifyStat actions with speed
            public EffectActionData(ActionType actionType, int value, TargetType targetType, bool modifyAttack, bool modifyHealth, bool modifySpeed) {
                if (actionType != ActionType.ModifyStat) Debug.LogError("Incorrect constructor used for non-ModifyStat action");
                this.actionType = actionType; this.value = value; this.targetType = targetType;
                this.modifyAttack = modifyAttack; this.modifyHealth = modifyHealth;
                this.modifySpeed = modifySpeed; // Use provided speed value
            }
            // Constructor for actions with modifiers
            public EffectActionData(ActionType actionType, int value, TargetType targetType, TargetModifier targetModifier) {
                this.actionType = actionType; this.value = value; this.targetType = targetType;
                this.targetModifier = targetModifier;
            }
            // Constructor for ModifyStat with modifiers
            public EffectActionData(ActionType actionType, int value, TargetType targetType, TargetModifier targetModifier, bool modifyAttack, bool modifyHealth) {
                 if (actionType != ActionType.ModifyStat) Debug.LogError("Incorrect constructor used for non-ModifyStat action");
                this.actionType = actionType; this.value = value; this.targetType = targetType;
                this.targetModifier = targetModifier; this.modifyAttack = modifyAttack; this.modifyHealth = modifyHealth;
                this.modifySpeed = false; // Default to false for speed
            }

            // Constructor for ModifyStat with modifiers and speed
            public EffectActionData(ActionType actionType, int value, TargetType targetType, TargetModifier targetModifier, bool modifyAttack, bool modifyHealth, bool modifySpeed) {
                 if (actionType != ActionType.ModifyStat) Debug.LogError("Incorrect constructor used for non-ModifyStat action");
                this.actionType = actionType; this.value = value; this.targetType = targetType;
                this.targetModifier = targetModifier; this.modifyAttack = modifyAttack; this.modifyHealth = modifyHealth;
                this.modifySpeed = modifySpeed; // Use provided speed value
            }
            // Constructor for ApplyStatus actions
             public EffectActionData(ActionType actionType, TargetType targetType, StatusEffectType status, int duration, int potency, TargetModifier modifier = TargetModifier.None) {
                if (actionType != ActionType.ApplyStatus) Debug.LogError("Incorrect constructor used for non-ApplyStatus action");
                this.actionType = actionType; this.targetType = targetType;
                this.statusEffectToApply = status;
                this.statusDuration = duration;
                this.statusPotency = potency;
                this.targetModifier = modifier;
                this.value = 0; // Value field is not used for ApplyStatus
            }
            // Constructor for Armor actions (uses value for amount, statusDuration for duration)
             public EffectActionData(ActionType actionType, int value, TargetType targetType, int duration, TargetModifier modifier = TargetModifier.None) {
                if (actionType != ActionType.Armor) Debug.LogError("Incorrect constructor used for non-Armor action");
                this.actionType = actionType; this.value = value; this.targetType = targetType; this.targetModifier = modifier;
                this.statusDuration = duration; // Use statusDuration field for armor duration
                 // Set defaults for others
                 this.modifyAttack = false; this.modifyHealth = false; this.modifySpeed = false;
                 this.statusEffectToApply = StatusEffectType.None; this.statusPotency = 0;
             }
        }
    }
}
#endif
