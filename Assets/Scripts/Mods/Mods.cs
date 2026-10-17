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
        // The file format is the game's own cards' (see CardFiles): a mod file is a card file with decks

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

        public static CardFiles.CardFile Parse(string json) {
            var mod = CardFiles.Read(json);
            if (string.IsNullOrWhiteSpace(mod.mod)) mod.mod = "Unnamed mod";
            return mod;
        }

        /// <summary>
        /// The cards and decks a mod file describes. A deck can name the game's own cards too (from <paramref name="catalog"/>),
        /// so a mod can be a deck list alone. Cards the file gets wrong are skipped and listed under <see cref="Problems"/>.
        /// </summary>
        public static (List<CardDataScriptableObject> cards, List<DeckScriptableObject> decks, List<DeckCatalog.Opponent> opponents)
            Build(CardFiles.CardFile mod, string folder, DeckCatalog catalog) {
            var cards = new List<CardDataScriptableObject>();
            // A card can name one written earlier in the file, or one of the game's (a token's disguise, a decoy)
            var known = new Dictionary<string, CardDataScriptableObject>(StringComparer.OrdinalIgnoreCase);
            if (catalog != null) foreach (var card in catalog.AllCards) known[card.cardName] = card;
            foreach (var card in mod.cards ?? new List<CardFiles.CardSpec>()) {
                try {
                    if (string.IsNullOrWhiteSpace(card.name)) throw new InvalidDataException("a card has no name");
                    var made = MakeCard(card, mod.mod, folder, known);
                    known[card.name] = made;
                    cards.Add(made);
                }
                catch (Exception e) {
                    Problem($"{mod.mod}: card '{card?.name}' skipped ({e.Message})");
                }
            }
            foreach (var card in cards) known[card.cardName] = card; // A mod's card with a game card's name takes its place in the mod's decks

            var decks = new List<DeckScriptableObject>();
            var opponents = new List<DeckCatalog.Opponent>();
            foreach (var deck in mod.decks ?? new List<CardFiles.DeckSpec>()) {
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

        private static CardDataScriptableObject MakeCard(CardFiles.CardSpec card, string modName, string folder, Dictionary<string, CardDataScriptableObject> known) {
            bool creature = CardFiles.IsCreature(card);
            CardDataScriptableObject asset = creature
                ? ScriptableObject.CreateInstance<CreatureCardScriptableObject>()
                : ScriptableObject.CreateInstance<SpellCardScriptableObject>();
            asset.hideFlags = HideFlags.DontUnloadUnusedAsset; // Loading the game scene doesn't unload it
            asset.name = card.name;
            asset.cardId = $"mod:{modName}:{card.name}";
            CardFiles.Apply(card, asset, Faction.None, name => known.TryGetValue(name ?? "", out var found) ? found : null, Problem);
            if (!string.IsNullOrWhiteSpace(card.art) && folder != null) asset.artwork = LoadArt(Path.Combine(folder, card.art), card.name);
            return asset;
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
