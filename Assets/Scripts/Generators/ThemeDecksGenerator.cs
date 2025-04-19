#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using static Enums;

public class ThemeDecksGenerator : EditorWindow {
    [MenuItem("Cards/Generate Theme Decks")]
    public static void GenerateThemeDecks() {
        // Create directories if they don't exist
        CreateDirectoryIfNeeded("Assets/Scriptables");
        CreateDirectoryIfNeeded("Assets/Scriptables/Cards");
        CreateDirectoryIfNeeded("Assets/Scriptables/Cards/Spiders");
        CreateDirectoryIfNeeded("Assets/Scriptables/Cards/Birds");
        CreateDirectoryIfNeeded("Assets/Scriptables/Cards/Water");
        CreateDirectoryIfNeeded("Assets/Scriptables/Decks");

        // Generate Spider Cards
        List<CardDataScriptableObject> spiderCards = GenerateSpiderCards();

        // Generate Bird Cards
        List<CardDataScriptableObject> birdCards = GenerateBirdCards();

        // Create Spider Deck
        CreateDeck("SpiderDeck", spiderCards, "Assets/Scriptables/Decks/SpiderDeck.asset");

        // Create Bird Deck
        CreateDeck("BirdDeck", birdCards, "Assets/Scriptables/Decks/BirdDeck.asset");

        // Generate Water Cards
        List<CardDataScriptableObject> waterCards = GenerateWaterCards();

        // Create Water Deck
        CreateDeck("WaterDeck", waterCards, "Assets/Scriptables/Decks/WaterDeck.asset");

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

        // Check if cards already exist and gather their IDs
        Dictionary<string, string> existingCardIds = GatherExistingCardIds("Assets/Scriptables/Cards/Spiders");

        // 1. Venomous Spider
        cards.Add(CreateSpiderCard(
            "Venomous Spider",
            "A small but deadly spider whose poison weakens enemies over time.",
            2, 2,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnDamage,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 1, TargetType.EnemyCreatures)
                }),
            "Assets/Scriptables/Cards/Spiders/VenomousSpider.asset",
            existingCardIds.ContainsKey("VenomousSpider") ? existingCardIds["VenomousSpider"] : System.Guid.NewGuid().ToString()
        ));

        // 2. Web Weaver
        cards.Add(CreateSpiderCard(
            "Web Weaver",
            "Spins strong webs that immobilize attackers.",
            1, 3,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 1, TargetType.EnemyCreatures)
                }),
            "Assets/Scriptables/Cards/Spiders/WebWeaver.asset",
            existingCardIds.ContainsKey("WebWeaver") ? existingCardIds["WebWeaver"] : System.Guid.NewGuid().ToString()
        ));

        // 3. Shadow Lurker
        cards.Add(CreateSpiderCard(
            "Shadow Lurker",
            "A stealthy spider that ambushes from the shadows.",
            3, 1,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 2, TargetType.EnemyCreatures)
                }),
            "Assets/Scriptables/Cards/Spiders/ShadowLurker.asset",
            existingCardIds.ContainsKey("ShadowLurker") ? existingCardIds["ShadowLurker"] : System.Guid.NewGuid().ToString()
        ));

        // 4. Broodmother
        cards.Add(CreateSpiderCard(
            "Broodmother",
            "Spawns smaller spiders when threatened.",
            2, 4,
            new CardEffectData(EffectType.Triggered, EffectTrigger.EndOfTurn,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Draw, 1, TargetType.Player)
                }),
            "Assets/Scriptables/Cards/Spiders/Broodmother.asset",
            existingCardIds.ContainsKey("Broodmother") ? existingCardIds["Broodmother"] : System.Guid.NewGuid().ToString()
        ));

        // 5. Tunnel Spider
        cards.Add(CreateSpiderCard(
            "Tunnel Spider",
            "Creates elaborate tunnel networks to ambush prey.",
            4, 2,
            new CardEffectData(EffectType.Triggered, EffectTrigger.StartOfTurn,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 1, TargetType.Enemy)
                }),
            "Assets/Scriptables/Cards/Spiders/TunnelSpider.asset",
            existingCardIds.ContainsKey("TunnelSpider") ? existingCardIds["TunnelSpider"] : System.Guid.NewGuid().ToString()
        ));

        // 6. Silk Spinner
        cards.Add(CreateSpiderCard(
            "Silk Spinner",
            "Its valuable silk can heal allies.",
            1, 2,
            new CardEffectData(EffectType.Triggered, EffectTrigger.StartOfTurn,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Heal, 1, TargetType.FriendlyCreatures)
                }),
            "Assets/Scriptables/Cards/Spiders/SilkSpinner.asset",
            existingCardIds.ContainsKey("SilkSpinner") ? existingCardIds["SilkSpinner"] : System.Guid.NewGuid().ToString()
        ));

        // 7. Giant Tarantula
        cards.Add(CreateSpiderCard(
            "Giant Tarantula",
            "This massive spider crushes opponents with brute force.",
            5, 5,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnDamage,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 2, TargetType.EnemyCreatures)
                }),
            "Assets/Scriptables/Cards/Spiders/GiantTarantula.asset",
            existingCardIds.ContainsKey("GiantTarantula") ? existingCardIds["GiantTarantula"] : System.Guid.NewGuid().ToString()
        ));

        // 8. Widow Assassin
        cards.Add(CreateSpiderCard(
            "Widow Assassin",
            "Its deadly venom can take down creatures many times its size.",
            3, 2,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 3, TargetType.EnemyCreatures)
                }),
            "Assets/Scriptables/Cards/Spiders/WidowAssassin.asset",
            existingCardIds.ContainsKey("WidowAssassin") ? existingCardIds["WidowAssassin"] : System.Guid.NewGuid().ToString()
        ));

        return cards;
    }

    private static List<CardDataScriptableObject> GenerateBirdCards() {
        List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();

        // Check if cards already exist and gather their IDs
        Dictionary<string, string> existingCardIds = GatherExistingCardIds("Assets/Scriptables/Cards/Birds");

        // 1. Royal Falcon
        cards.Add(CreateBirdCard(
            "Royal Falcon",
            "A noble bird trained for hunting, with exceptional speed.",
            3, 2,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 1, TargetType.AllCreatures)
                }),
            "Assets/Scriptables/Cards/Birds/RoyalFalcon.asset",
            existingCardIds.ContainsKey("RoyalFalcon") ? existingCardIds["RoyalFalcon"] : System.Guid.NewGuid().ToString()
        ));

        // 2. Wise Owl
        cards.Add(CreateBirdCard(
            "Wise Owl",
            "Its wisdom allows allies to find new strategies.",
            2, 3,
            new CardEffectData(EffectType.Triggered, EffectTrigger.EndOfTurn,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Draw, 1, TargetType.Player)
                }),
            "Assets/Scriptables/Cards/Birds/WiseOwl.asset",
            existingCardIds.ContainsKey("WiseOwl") ? existingCardIds["WiseOwl"] : System.Guid.NewGuid().ToString()
        ));

        // 3. Thunderhawk
        cards.Add(CreateBirdCard(
            "Thunderhawk",
            "Calls lightning down upon enemies.",
            4, 3,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 2, TargetType.EnemyCreatures)
                }),
            "Assets/Scriptables/Cards/Birds/Thunderhawk.asset",
            existingCardIds.ContainsKey("Thunderhawk") ? existingCardIds["Thunderhawk"] : System.Guid.NewGuid().ToString()
        ));

        // 4. Healing Dove
        cards.Add(CreateBirdCard(
            "Healing Dove",
            "Its gentle presence mends wounds.",
            1, 2,
            new CardEffectData(EffectType.Triggered, EffectTrigger.StartOfTurn,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Heal, 2, TargetType.Player)
                }),
            "Assets/Scriptables/Cards/Birds/HealingDove.asset",
            existingCardIds.ContainsKey("HealingDove") ? existingCardIds["HealingDove"] : System.Guid.NewGuid().ToString()
        ));

        // 5. War Eagle
        cards.Add(CreateBirdCard(
            "War Eagle",
            "A battle-hardened bird that leads the charge in combat.",
            5, 4,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnDamage,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 2, TargetType.Enemy)
                }),
            "Assets/Scriptables/Cards/Birds/WarEagle.asset",
            existingCardIds.ContainsKey("WarEagle") ? existingCardIds["WarEagle"] : System.Guid.NewGuid().ToString()
        ));

        // 6. Mischievous Raven
        cards.Add(CreateBirdCard(
            "Mischievous Raven",
            "Steals valuable items to aid its allies.",
            2, 1,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Draw, 2, TargetType.Player)
                }),
            "Assets/Scriptables/Cards/Birds/MischievousRaven.asset",
            existingCardIds.ContainsKey("MischievousRaven") ? existingCardIds["MischievousRaven"] : System.Guid.NewGuid().ToString()
        ));

        // 7. Phoenix Hatchling
        cards.Add(CreateBirdCard(
            "Phoenix Hatchling",
            "Though young, it carries the flame of rebirth.",
            1, 3,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnDeath,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 2, TargetType.AllCreatures)
                }),
            "Assets/Scriptables/Cards/Birds/PhoenixHatchling.asset",
            existingCardIds.ContainsKey("PhoenixHatchling") ? existingCardIds["PhoenixHatchling"] : System.Guid.NewGuid().ToString()
        ));

        // 8. Majestic Griffin
        cards.Add(CreateBirdCard(
            "Majestic Griffin",
            "Half eagle, half lion, all power.",
            4, 5,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 1, TargetType.EnemyCreatures),
                    new EffectActionData(ActionType.Heal, 1, TargetType.FriendlyCreatures)
                }),
            "Assets/Scriptables/Cards/Birds/MajesticGriffin.asset",
            existingCardIds.ContainsKey("MajesticGriffin") ? existingCardIds["MajesticGriffin"] : System.Guid.NewGuid().ToString()
        ));

        return cards;
    }

    private static List<CardDataScriptableObject> GenerateWaterCards() {
        List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();
        Dictionary<string, string> existingCardIds = GatherExistingCardIds("Assets/Scriptables/Cards/Water");

        // 1. Dancing Dolphin
        cards.Add(CreateWaterCard(
            "Dancing Dolphin",
            "Playful movements boost ally morale.",
            2, 3,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Draw, 1, TargetType.Player)
                }),
            "Assets/Scriptables/Cards/Water/DancingDolphin.asset",
            existingCardIds.ContainsKey("DancingDolphin") ? existingCardIds["DancingDolphin"] : System.Guid.NewGuid().ToString()
        ));

        // 2. Guardian Whale
        cards.Add(CreateWaterCard(
            "Guardian Whale",
            "Massive presence protects allies.",
            1, 6,
            new CardEffectData(EffectType.Triggered, EffectTrigger.StartOfTurn,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Armor, 2, TargetType.FriendlyCreatures)
                }),
            "Assets/Scriptables/Cards/Water/GuardianWhale.asset",
            existingCardIds.ContainsKey("GuardianWhale") ? existingCardIds["GuardianWhale"] : System.Guid.NewGuid().ToString()
        ));

        // 3. Tidepool Star
        cards.Add(CreateWaterCard(
            "Tidepool Star",
            "Regenerates during low tide.",
            1, 2,
            new CardEffectData(EffectType.Triggered, EffectTrigger.EndOfTurn,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Heal, 2, TargetType.Self)
                }),
            "Assets/Scriptables/Cards/Water/TidepoolStar.asset",
            existingCardIds.ContainsKey("TidepoolStar") ? existingCardIds["TidepoolStar"] : System.Guid.NewGuid().ToString()
        ));

        // 4. Jellyfish Swarm
        cards.Add(CreateWaterCard(
            "Jellyfish Swarm",
            "Paralyzes enemies with gentle pulses when destroyed.", // Updated description
            0, 4,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnDeath,
                new List<EffectActionData> {
                    // Use the new ApplyStatus constructor
                    new EffectActionData(ActionType.ApplyStatus, TargetType.EnemyCreatures, StatusEffectType.Paralyzed, 1, 0) // 1 turn duration, 0 potency
                }),
            "Assets/Scriptables/Cards/Water/JellyfishSwarm.asset",
            existingCardIds.ContainsKey("JellyfishSwarm") ? existingCardIds["JellyfishSwarm"] : System.Guid.NewGuid().ToString()
        ));

        // 5. Coral Builder
        cards.Add(CreateWaterCard(
            "Coral Builder",
            "Constructs protective barriers.",
            0, 3,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Armor, 3, TargetType.FriendlyCreatures)
                }),
            "Assets/Scriptables/Cards/Water/CoralBuilder.asset",
            existingCardIds.ContainsKey("CoralBuilder") ? existingCardIds["CoralBuilder"] : System.Guid.NewGuid().ToString()
        ));

        // 6. Abyssal Cucumber
        cards.Add(CreateWaterCard(
            "Abyssal Cucumber",
            "Gives all friendly creatures +2 attack for 2 turns.",
            1, 4,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Buff, 2, TargetType.FriendlyCreatures, true, false)
                }),
            "Assets/Scriptables/Cards/Water/AbyssalCucumber.asset",
            existingCardIds.ContainsKey("AbyssalCucumber") ? existingCardIds["AbyssalCucumber"] : System.Guid.NewGuid().ToString()
        ));

        // 7. Crashing Wave
        cards.Add(CreateWaterCard(
            "Crashing Wave",
            "A powerful wave that crashes onto enemies.",
            3, 4,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 2, TargetType.EnemyCreatures)
                }),
            "Assets/Scriptables/Cards/Water/CrashingWave.asset",
            existingCardIds.ContainsKey("CrashingWave") ? existingCardIds["CrashingWave"] : System.Guid.NewGuid().ToString()
        ));

        // 8. Deep Sea Angler
        cards.Add(CreateWaterCard(
            "Deep Sea Angler",
            "Lures enemies with bioluminescent bait, paralyzing them.",
            2, 5,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnDamage,
                new List<EffectActionData> {
                    // Use the new ApplyStatus constructor
                    new EffectActionData(ActionType.ApplyStatus, TargetType.Enemy, StatusEffectType.Paralyzed, 1, 0) // 1 turn duration, 0 potency
                }),
            "Assets/Scriptables/Cards/Water/DeepSeaAngler.asset",
            existingCardIds.ContainsKey("DeepSeaAngler") ? existingCardIds["DeepSeaAngler"] : System.Guid.NewGuid().ToString()
        ));

        // 9. Electric Eel
        cards.Add(CreateWaterCard(
            "Electric Eel",
            "When attacking, zaps the primary target and chains lightning to 2 additional targets in a random direction.",
            4, 3,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnDamage,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 4, TargetType.EnemyCreatures, TargetModifier.Chained)
                }),
            "Assets/Scriptables/Cards/Water/ElectricEel.asset",
            existingCardIds.ContainsKey("ElectricEel") ? existingCardIds["ElectricEel"] : System.Guid.NewGuid().ToString()
        ));

        // 10. Kraken
        cards.Add(CreateWaterCard(
            "Kraken",
            "Massive sea monster that terrifies enemies.",
            6, 8,
            new CardEffectData(EffectType.Triggered, EffectTrigger.StartOfTurn,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 3, TargetType.EnemyCreatures)
                }),
            "Assets/Scriptables/Cards/Water/Kraken.asset",
            existingCardIds.ContainsKey("Kraken") ? existingCardIds["Kraken"] : System.Guid.NewGuid().ToString()
        ));

        // 11. Mermaid Healer
        cards.Add(CreateWaterCard(
            "Mermaid Healer",
            "Restores health to damaged allies at the start of your turn.",
            1, 4,
            new CardEffectData(EffectType.Triggered, EffectTrigger.StartOfTurn,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Heal, 2, TargetType.FriendlyCreatures)
                }),
            "Assets/Scriptables/Cards/Water/MermaidHealer.asset",
            existingCardIds.ContainsKey("MermaidHealer") ? existingCardIds["MermaidHealer"] : System.Guid.NewGuid().ToString()
        ));

        // 12. Octopus Defender
        cards.Add(CreateWaterCard(
            "Octopus Defender",
            "Uses tentacles to block incoming attacks.",
            0, 7,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnDamage,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Armor, 2, TargetType.Self)
                }),
            "Assets/Scriptables/Cards/Water/OctopusDefender.asset",
            existingCardIds.ContainsKey("OctopusDefender") ? existingCardIds["OctopusDefender"] : System.Guid.NewGuid().ToString()
        ));

        // 13. Seahorse Scout
        cards.Add(CreateWaterCard(
            "Seahorse Scout",
            "Quickly discovers new resources.",
            1, 1,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnPlay,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Draw, 1, TargetType.Player)
                }),
            "Assets/Scriptables/Cards/Water/SeahorseScout.asset",
            existingCardIds.ContainsKey("SeahorseScout") ? existingCardIds["SeahorseScout"] : System.Guid.NewGuid().ToString()
        ));

        // 14. Tsunami Turtle
        cards.Add(CreateWaterCard(
            "Tsunami Turtle",
            "Creates massive waves when attacking.",
            4, 6,
            new CardEffectData(EffectType.Triggered, EffectTrigger.OnDamage,
                new List<EffectActionData> {
                    new EffectActionData(ActionType.Damage, 1, TargetType.EnemyCreatures)
                }),
            "Assets/Scriptables/Cards/Water/TsunamiTurtle.asset",
            existingCardIds.ContainsKey("TsunamiTurtle") ? existingCardIds["TsunamiTurtle"] : System.Guid.NewGuid().ToString()
        ));

        // Water Spells
        cards.AddRange(new List<CardDataScriptableObject> {
            CreateWaterSpell(
                "Tidal Surge",
                "Wash away enemy defenses.",
                TargetType.EnemyCreatures,
                new CardEffectData(EffectType.Instantaneous, EffectTrigger.OnPlay,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Damage, 4, TargetType.EnemyCreatures)
                    }),
                "Assets/Scriptables/Cards/Water/TidalSurge.asset",
                existingCardIds.ContainsKey("TidalSurge") ? existingCardIds["TidalSurge"] : System.Guid.NewGuid().ToString()
            ),
            CreateWaterSpell(
                "Whirlpool",
                "Trap enemies in a spinning vortex, paralyzing them for 2 turns.",
                TargetType.EnemyCreatures,
                new CardEffectData(EffectType.Instantaneous, EffectTrigger.OnPlay,
                    new List<EffectActionData> {
                        // Use the new ApplyStatus constructor
                        new EffectActionData(ActionType.ApplyStatus, TargetType.EnemyCreatures, StatusEffectType.Paralyzed, 2, 0) // 2 turn duration, 0 potency
                    }),
                "Assets/Scriptables/Cards/Water/Whirlpool.asset",
                existingCardIds.ContainsKey("Whirlpool") ? existingCardIds["Whirlpool"] : System.Guid.NewGuid().ToString()
            ),
            CreateWaterSpell(
                "Healing Rain",
                "Restore health to all friendly creatures.",
                TargetType.FriendlyCreatures,
                new CardEffectData(EffectType.Instantaneous, EffectTrigger.OnPlay,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Heal, 3, TargetType.FriendlyCreatures)
                    }),
                "Assets/Scriptables/Cards/Water/HealingRain.asset",
                existingCardIds.ContainsKey("HealingRain") ? existingCardIds["HealingRain"] : System.Guid.NewGuid().ToString()
            ),
            CreateWaterSpell(
                "Coral Shield",
                "Create temporary armor for allies.",
                TargetType.FriendlyCreatures,
                new CardEffectData(EffectType.Instantaneous, EffectTrigger.OnPlay,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Armor, 4, TargetType.FriendlyCreatures)
                    }),
                "Assets/Scriptables/Cards/Water/CoralShield.asset",
                existingCardIds.ContainsKey("CoralShield") ? existingCardIds["CoralShield"] : System.Guid.NewGuid().ToString()
            ),
            CreateWaterSpell(
                "Abyssal Call",
                "Summon a powerful deep sea creature.",
                TargetType.Player,
                new CardEffectData(EffectType.Instantaneous, EffectTrigger.OnPlay,
                    new List<EffectActionData> {
                        new EffectActionData(ActionType.Summon, 1, TargetType.Player)
                    }),
                "Assets/Scriptables/Cards/Water/AbyssalCall.asset",
                existingCardIds.ContainsKey("AbyssalCall") ? existingCardIds["AbyssalCall"] : System.Guid.NewGuid().ToString()
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
            spell.defaultTargetType = (Enums.TargetType)defaultTargetType;
            UpdateSpellCardEffects(spell, effectData);
            EditorUtility.SetDirty(spell);
            return spell;
        }

        spell = ScriptableObject.CreateInstance<SpellCardScriptableObject>();
        spell.cardName = name;
        spell.description = description;
        spell.defaultTargetType = (Enums.TargetType)defaultTargetType;
        spell.cardId = cardId;
        AddSpellEffectToCard(spell, effectData);
        AssetDatabase.CreateAsset(spell, path);
        return spell;
    }

    static CreatureCardScriptableObject CreateWaterCard(
        string name,
        string description,
        int attack,
        int health,
        CardEffectData effectData,
        string path,
        string cardId) {
        return CreateSpiderCard(name, description, attack, health, effectData, path, cardId);
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

    private static CreatureCardScriptableObject CreateSpiderCard(
        string name,
        string description,
        int attack,
        int health,
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

                // Keep the existing cardId if it exists, otherwise set the new one
                if (string.IsNullOrEmpty(card.cardId)) {
                    card.cardId = cardId;
                    Debug.Log($"Setting new ID for {name}: {cardId}");
                }
                else {
                    Debug.Log($"Keeping existing ID for {name}: {card.cardId}");
                }

                // Clear existing effects and add new ones
                UpdateCardEffects(card, effectData);

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
        card.cardId = cardId; // Set the card ID

        AddEffectToCard(card, effectData);

        AssetDatabase.CreateAsset(card, path);
        return card;
    }

    private static CreatureCardScriptableObject CreateBirdCard(
        string name,
        string description,
        int attack,
        int health,
        CardEffectData effectData,
        string path,
        string cardId) {
        // Using the same implementation as CreateSpiderCard since they're identical
        return CreateSpiderCard(name, description, attack, health, effectData, path, cardId);
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

    // Updates effects on an existing card
    private static void UpdateCardEffects(CreatureCardScriptableObject card, CardEffectData effectData) {
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
            if (effectData.actions[i].actionType == ActionType.Buff) {
                // Check if the properties exist in the serialized object
                SerializedProperty buffAttackProp = actionProp.FindPropertyRelative("buffAttack");
                SerializedProperty buffHealthProp = actionProp.FindPropertyRelative("buffHealth");

                if (buffAttackProp != null && buffHealthProp != null) {
                    buffAttackProp.boolValue = effectData.actions[i].buffAttack;
                    buffHealthProp.boolValue = effectData.actions[i].buffHealth;
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

    private static void AddEffectToCard(CreatureCardScriptableObject card, CardEffectData effectData) {
        // This method is the same as UpdateCardEffects, just with a different name for clarity
        UpdateCardEffects(card, effectData);
    }

    // Add a new method for spell card effects
    private static void UpdateSpellCardEffects(SpellCardScriptableObject card, CardEffectData effectData) {
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
            if (effectData.actions[i].actionType == ActionType.Buff) {
                // Check if the properties exist in the serialized object
                SerializedProperty buffAttackProp = actionProp.FindPropertyRelative("buffAttack");
                SerializedProperty buffHealthProp = actionProp.FindPropertyRelative("buffHealth");

                if (buffAttackProp != null && buffHealthProp != null) {
                    buffAttackProp.boolValue = effectData.actions[i].buffAttack;
                    buffHealthProp.boolValue = effectData.actions[i].buffHealth;
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

    private static void AddSpellEffectToCard(SpellCardScriptableObject card, CardEffectData effectData) {
        // This method is the same as UpdateSpellCardEffects, just with a different name for clarity
        UpdateSpellCardEffects(card, effectData);
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
        // Buff Specific
        public bool buffAttack = true;
        public bool buffHealth = true;
        // --- ApplyStatus Specific ---
        public StatusEffectType statusEffectToApply = StatusEffectType.None;
        public int statusDuration = 0;
        public int statusPotency = 0;

        // Constructor for basic actions
        public EffectActionData(ActionType actionType, int value, TargetType targetType) {
            this.actionType = actionType; this.value = value; this.targetType = targetType;
        }
        // Constructor for buff actions
        public EffectActionData(ActionType actionType, int value, TargetType targetType, bool buffAttack, bool buffHealth) {
            this.actionType = actionType; this.value = value; this.targetType = targetType;
            this.buffAttack = buffAttack; this.buffHealth = buffHealth;
        }
        // Constructor for actions with modifiers
        public EffectActionData(ActionType actionType, int value, TargetType targetType, TargetModifier targetModifier) {
            this.actionType = actionType; this.value = value; this.targetType = targetType;
            this.targetModifier = targetModifier;
        }
        // Constructor for buffs with modifiers
        public EffectActionData(ActionType actionType, int value, TargetType targetType, TargetModifier targetModifier, bool buffAttack, bool buffHealth) {
            this.actionType = actionType; this.value = value; this.targetType = targetType;
            this.targetModifier = targetModifier; this.buffAttack = buffAttack; this.buffHealth = buffHealth;
        }
        // --- Constructor for ApplyStatus actions ---
         public EffectActionData(ActionType actionType, TargetType targetType, StatusEffectType status, int duration, int potency, TargetModifier modifier = TargetModifier.None) {
            if (actionType != ActionType.ApplyStatus) Debug.LogError("Incorrect constructor used for non-ApplyStatus action");
            this.actionType = actionType;
            this.targetType = targetType;
            this.statusEffectToApply = status;
            this.statusDuration = duration;
            this.statusPotency = potency;
            this.targetModifier = modifier;
            this.value = 0; // Value field is not used for ApplyStatus
        }
    }
}
#endif