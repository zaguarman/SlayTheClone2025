using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// Mods (the user, 2026-10-17: "support for external mods"): cards and decks written in JSON files outside the project,
    /// loaded when the main menu opens and offered like the game's own. A mod is a folder with one or more .json files (see
    /// 'Modding.txt' for the format) and, beside them, the PNG pictures its cards name. Looked for in <c>Mods/</c> next to the
    /// project (the editor and a desktop build) and in <c>Mods/</c> under the game's data folder (Application.persistentDataPath).
    /// A mod's cards use the game's own rules: the action, status, ability and trigger names are the ones in the glossary.
    /// What a file gets wrong is logged under <see cref="Problems"/> and shown in the console; the rest of it still loads.
    /// </summary>
    public static class Mods {
        #region The file format
        [Serializable]
        public class ModFile {
            public string mod;    // The mod's name: it prefixes its cards' ids and names its decks' opponent
            public string author;
            public List<ModCard> cards = new List<ModCard>();
            public List<ModDeck> decks = new List<ModDeck>();
        }

        [Serializable]
        public class ModCard {
            public string name;
            public string type = "creature";   // creature or spell
            public string faction = "None";    // Corporate, Statecraft, DigitalSociety, Myth, ShadowOps, Tech, Mind, Utopia
            public string flavor;              // The line under the picture
            public string notes;               // Design notes (not printed)
            public string art;                 // A PNG beside the file, 352x200 reads best
            public int attack, health;         // A creature's
            public int speed = 1;
            public int deploy;                 // Deployment Time
            public string ability = "None";    // A creature's ability, by its code name (Litigious, HunkerDown, ...)
            public string movement = "None";   // Leapfrog or Swap
            public bool gift;
            public string target = "EnemyCreatures"; // A spell's aim: Player, Enemy, AllCreatures, FriendlyCreatures, EnemyCreatures
            public bool singleUse;
            public int discard, sacrifice, prophecy;
            public List<ModEffect> effects = new List<ModEffect>();
        }

        [Serializable]
        public class ModEffect {
            public string trigger = "OnPlay";  // OnPlay (a creature's Arrival, a spell's cast), OnDeath, OnDamage, StartOfTurn, EndOfTurn, OnRevealed, OnReplaced, OnDiscarded
            public List<ModAction> actions = new List<ModAction>();
        }

        [Serializable]
        public class ModAction {
            public string action = "Damage";   // Damage, Heal, Draw, Summon, Armor, Stun, ModifyStat, ApplyStatus, Cleanse, PlaceSlotEffect, SetTrend, Destroy, ...
            public int value;
            public string target = "EnemyCreatures";
            public string modifier = "None";   // None, WithAdjacent, AllSameTypeTargets, Random, Strongest, Opposite, ... (several with |)
            public string status = "None";     // ApplyStatus: Burned, Exhausted, Blessed, Rooted, ...
            public int days, potency;
            public string slotEffect = "None"; // PlaceSlotEffect: Landmine, BarbedSnare, Shield, ScorchedEarth, Checkpoint, Prayer
            public string trend = "None";      // SetTrend: Zeitgeist, RushHour, SurvivalOfTheFittest, VolcanoLair, Casino, LiminalSpace
            public string ability = "None";    // GrantAbility
            public string requires = "None";   // A requisite on the target: Revealed, Deploying, Online, ...
            public bool attackStat = true, healthStat = true, speedStat; // ModifyStat: which stats
        }

        [Serializable]
        public class ModDeck {
            public string name;
            public string opponent;  // Who brings it (the mod's name if empty)
            public string tagline;
            public string strategy;
            public List<string> cards = new List<string>(); // Card names: the mod's own, or any of the game's
        }
        #endregion

        /// <summary>The mods loaded, by name.</summary>
        public static List<string> Loaded { get; } = new List<string>();
        /// <summary>What the files got wrong, in plain words, one line each.</summary>
        public static List<string> Problems { get; } = new List<string>();

        private static readonly HashSet<int> loadedInto = new HashSet<int>();

        // Each play session loads afresh: leaving play mode destroys the decks and cards made last time, while the catalog
        // asset and these statics outlive it when the domain isn't reloaded (the test runner, Enter Play Mode options). A
        // guard left standing then offered opponents whose decks were dead, and the match fell back to random decks
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnPlayStart() {
            loadedInto.Clear();
            Loaded.Clear();
            Problems.Clear();
        }

        /// <summary>Where mods are looked for: next to the project (or the build), and under the game's data folder.</summary>
        public static IEnumerable<string> Folders() {
            yield return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods"));
            yield return Path.Combine(Application.persistentDataPath, "Mods");
        }

        /// <summary>Loads every mod file into the catalog, once per catalog: its decks are offered beside the game's own.</summary>
        public static void LoadInto(DeckCatalog catalog) {
            if (catalog == null || !loadedInto.Add(catalog.GetInstanceID())) return;
            catalog.ForgetMods(); // Whatever an earlier session left on the asset
            foreach (var folder in Folders().Distinct()) {
                string[] files;
                try {
                    if (!Directory.Exists(folder)) continue;
                    files = Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories);
                }
                catch (Exception e) { // A platform with no file system (the browser), or a folder that can't be read
                    Log($"Mods: {folder} can't be read ({e.Message})", LogTag.Initialization);
                    continue;
                }
                foreach (var file in files.OrderBy(f => f)) {
                    try {
                        var mod = Parse(File.ReadAllText(file));
                        var (cards, decks, opponents) = Build(mod, Path.GetDirectoryName(file), catalog);
                        catalog.AddMod(decks, opponents);
                        Loaded.Add(mod.mod);
                        Log($"Mod '{mod.mod}' loaded from {file}: {cards.Count} cards, {decks.Count} decks", LogTag.Initialization);
                    }
                    catch (Exception e) {
                        Problem($"{Path.GetFileName(file)}: not loaded ({e.Message})");
                    }
                }
            }
            foreach (var problem in Problems) LogWarning($"Mods: {problem}", LogTag.Initialization);
        }

        public static ModFile Parse(string json) {
            var mod = JsonUtility.FromJson<ModFile>(json);
            if (mod == null) throw new InvalidDataException("not a mod file");
            if (string.IsNullOrWhiteSpace(mod.mod)) mod.mod = "Unnamed mod";
            return mod;
        }

        /// <summary>
        /// The cards and decks a mod file describes. A deck can name the game's own cards too (from <paramref name="catalog"/>),
        /// so a mod can be a deck list alone. Cards the file gets wrong are skipped and listed under <see cref="Problems"/>.
        /// </summary>
        public static (List<CardDataScriptableObject> cards, List<DeckScriptableObject> decks, List<DeckCatalog.Opponent> opponents)
            Build(ModFile mod, string folder, DeckCatalog catalog) {
            var cards = new List<CardDataScriptableObject>();
            foreach (var card in mod.cards ?? new List<ModCard>()) {
                try {
                    if (string.IsNullOrWhiteSpace(card.name)) throw new InvalidDataException("a card has no name");
                    cards.Add(MakeCard(card, mod.mod, folder));
                }
                catch (Exception e) {
                    Problem($"{mod.mod}: card '{card?.name}' skipped ({e.Message})");
                }
            }
            var known = new Dictionary<string, CardDataScriptableObject>(StringComparer.OrdinalIgnoreCase);
            if (catalog != null) foreach (var card in catalog.AllCards) known[card.cardName] = card;
            foreach (var card in cards) known[card.cardName] = card; // A mod's card with a game card's name takes its place in the mod's decks

            var decks = new List<DeckScriptableObject>();
            var opponents = new List<DeckCatalog.Opponent>();
            foreach (var deck in mod.decks ?? new List<ModDeck>()) {
                var list = new List<CardDataScriptableObject>();
                foreach (var name in deck.cards ?? new List<string>()) {
                    if (known.TryGetValue(name ?? "", out var card)) list.Add(card);
                    else Problem($"{mod.mod}: deck '{deck.name}' names no card called '{name}'");
                }
                if (list.Count < PlayerDecks.MinDeckSize) {
                    Problem($"{mod.mod}: deck '{deck.name}' has {list.Count} cards, fewer than {PlayerDecks.MinDeckSize}: not offered");
                    continue;
                }
                string deckName = string.IsNullOrWhiteSpace(deck.name) ? $"{mod.mod} - Deck {decks.Count + 1}" : deck.name;
                var made = DeckScriptableObject.Make(deckName, list.Take(PlayerDecks.MaxDeckSize));
                made.SetStrategy(deck.strategy);
                decks.Add(made);
                opponents.Add(new DeckCatalog.Opponent { name = string.IsNullOrWhiteSpace(deck.opponent) ? mod.mod : deck.opponent, tagline = deck.tagline ?? "", deck = made });
            }
            return (cards, decks, opponents);
        }

        private static CardDataScriptableObject MakeCard(ModCard card, string modName, string folder) {
            bool creature = !string.Equals(card.type, "spell", StringComparison.OrdinalIgnoreCase);
            CardDataScriptableObject asset = creature
                ? ScriptableObject.CreateInstance<CreatureCardScriptableObject>()
                : ScriptableObject.CreateInstance<SpellCardScriptableObject>();
            asset.hideFlags = HideFlags.DontUnloadUnusedAsset; // Loading the game scene doesn't unload it
            asset.name = card.name;
            asset.cardId = $"mod:{modName}:{card.name}";
            asset.cardName = card.name;
            asset.description = card.flavor ?? "";
            asset.designNotes = card.notes ?? "";
            asset.faction = Enum(card.faction, Faction.None, $"{card.name}: faction");
            asset.singleUse = card.singleUse;
            asset.discardCost = Math.Max(0, card.discard);
            asset.sacrifice = Math.Max(0, card.sacrifice);
            asset.prophecy = Math.Max(0, card.prophecy);
            asset.ReplaceEffects((card.effects ?? new List<ModEffect>()).Select(e => MakeEffect(e, creature, card.name))); // The runtime setter: SetEffects is the editor's
            switch (asset) {
                case CreatureCardScriptableObject c:
                    c.attack = Math.Max(0, card.attack);
                    c.health = Math.Max(1, card.health);
                    c.speed = Math.Max(1, card.speed);
                    c.turnsUntilOperative = Math.Max(0, card.deploy);
                    c.ability = Enum(card.ability, CreatureAbility.None, $"{card.name}: ability");
                    c.movement = Enum(card.movement, MovementKeyword.None, $"{card.name}: movement");
                    c.gift = card.gift;
                    break;
                case SpellCardScriptableObject s:
                    s.defaultTargetType = Enum(card.target, TargetType.EnemyCreatures, $"{card.name}: target");
                    break;
            }
            if (!string.IsNullOrWhiteSpace(card.art) && folder != null) asset.artwork = LoadArt(Path.Combine(folder, card.art), card.name);
            return asset;
        }

        private static CardEffect MakeEffect(ModEffect effect, bool creature, string cardName) {
            var trigger = Enum(effect.trigger, EffectTrigger.OnPlay, $"{cardName}: trigger");
            return new CardEffect {
                effectType = creature ? EffectType.Immediate : EffectType.Triggered, // As the generator makes them
                trigger = trigger,
                actions = (effect.actions ?? new List<ModAction>()).Select(a => MakeAction(a, cardName)).ToList()
            };
        }

        private static EffectAction MakeAction(ModAction a, string cardName) {
            var action = new EffectAction {
                actionType = Enum(a.action, ActionType.Damage, $"{cardName}: action"),
                value = a.value,
                targetType = Enum(a.target, TargetType.EnemyCreatures, $"{cardName}: target"),
                targetModifier = Flags(a.modifier, cardName),
                statusEffectToApply = Enum(a.status, StatusEffectType.None, $"{cardName}: status"),
                statusDuration = a.days,
                statusPotency = a.potency,
                slotEffect = Enum(a.slotEffect, SlotEffectType.None, $"{cardName}: slot effect"),
                trend = Enum(a.trend, Trend.None, $"{cardName}: trend"),
                ability = Enum(a.ability, CreatureAbility.None, $"{cardName}: ability"),
                requires = Enum(a.requires, TargetRequisite.None, $"{cardName}: requires"),
                modifyAttack = a.attackStat,
                modifyHealth = a.healthStat,
                modifySpeed = a.speedStat
            };
            if (action.actionType == ActionType.Stun && action.value <= 0) action.value = 1;
            return action;
        }

        // A name from the file as one of the game's own, any case; a wrong one is reported and the fallback used
        private static T Enum<T>(string name, T fallback, string where) where T : struct {
            if (string.IsNullOrWhiteSpace(name)) return fallback;
            if (System.Enum.TryParse(name.Replace(" ", "").Replace("-", "").Replace("'", ""), true, out T value)) return value;
            Problem($"{where}: '{name}' isn't one of the game's {typeof(T).Name}s; {fallback} used");
            return fallback;
        }

        private static TargetModifier Flags(string names, string cardName) {
            var result = TargetModifier.None;
            foreach (var part in (names ?? "").Split('|')) result |= Enum(part.Trim(), TargetModifier.None, $"{cardName}: modifier");
            return result;
        }

        private static Sprite LoadArt(string path, string cardName) {
            try {
                if (!File.Exists(path)) {
                    Problem($"{cardName}: no picture at {path}");
                    return null;
                }
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException("not a PNG or JPG");
                texture.name = cardName;
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (Exception e) {
                Problem($"{cardName}: picture not loaded ({e.Message})");
                return null;
            }
        }

        private static void Problem(string text) {
            if (!Problems.Contains(text)) Problems.Add(text);
        }
    }
}
