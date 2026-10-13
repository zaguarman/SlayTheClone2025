using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// The decks the player makes in the deck builder, mixing any cards on offer. They're kept between sessions in a file
    /// in the player's data folder, each card by its id (and its name, in case the id changed). A deck has
    /// <see cref="DeckSize"/> cards, at most <see cref="MaxCopies"/> of each; once complete, the match setup offers it next to
    /// the starter decks, to you and to your opponent.
    /// </summary>
    public static class PlayerDecks {
        public const int DeckSize = 20; // As many as a starter deck
        public const int MaxCopies = 2;
        public const int MaxNameLength = 20;
        public const string NewDeckName = "New deck";

        [Serializable]
        private class SavedCard {
            public string id;
            public string name;
        }

        [Serializable]
        private class SavedDeck {
            public string name;
            public List<SavedCard> cards = new List<SavedCard>();
        }

        [Serializable]
        private class SavedDecks {
            public List<SavedDeck> decks = new List<SavedDeck>();
        }

        private static string filePath;
        private static List<DeckScriptableObject> decks;
        private static DeckCatalog catalog;

        /// <summary>Where the decks are kept. Tests point it at a file of their own.</summary>
        public static string FilePath {
            get => filePath ??= Path.Combine(Application.persistentDataPath, "decks.json");
            set {
                filePath = value;
                decks = null; // Read again from there
            }
        }

        // Also runs when play mode starts: the decks are read from the file again, and a test's file is never kept
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Forget() {
            filePath = null;
            decks = null;
            catalog = null;
        }

        /// <summary>The player's decks, in the order they were made. Read from the file the first time; their cards are found among the catalog's.</summary>
        public static IReadOnlyList<DeckScriptableObject> All(DeckCatalog cardsFrom) {
            Load(cardsFrom);
            return decks;
        }

        /// <summary>The player's decks that can be played: those with all their cards.</summary>
        public static IReadOnlyList<DeckScriptableObject> Playable(DeckCatalog cardsFrom) => All(cardsFrom).Where(IsComplete).ToList();

        public static bool IsComplete(DeckScriptableObject deck) => deck != null && deck.Cards.Count == DeckSize;
        public static int CopiesOf(DeckScriptableObject deck, CardDataScriptableObject card) => deck?.Cards.Count(c => c == card) ?? 0;
        public static bool CanAdd(DeckScriptableObject deck, CardDataScriptableObject card) =>
            IsPlayers(deck) && card != null && deck.Cards.Count < DeckSize && CopiesOf(deck, card) < MaxCopies;
        public static bool IsPlayers(DeckScriptableObject deck) => deck != null && decks != null && decks.Contains(deck);

        /// <summary>"14 / 20 cards", or "20 cards" once complete. Its factions show as coins (see <see cref="DeckCatalog.CoinsFor"/>).</summary>
        public static string SizeText(DeckScriptableObject deck) => IsComplete(deck) ? $"{DeckSize} cards" : $"{deck.Cards.Count} / {DeckSize} cards";

        /// <summary>A new deck, named after the given name (or "New deck") but never like another deck, with these cards.</summary>
        public static DeckScriptableObject Create(DeckCatalog cardsFrom, string name = null, IEnumerable<CardDataScriptableObject> cards = null) {
            Load(cardsFrom);
            var deck = DeckScriptableObject.Make(UniqueName(string.IsNullOrWhiteSpace(name) ? NewDeckName : name.Trim(), null),
                (cards ?? Enumerable.Empty<CardDataScriptableObject>()).Take(DeckSize));
            decks.Add(deck);
            Save();
            return deck;
        }

        /// <summary>Adds a copy of the card, unless the deck is full or has as many copies as allowed.</summary>
        public static bool Add(DeckScriptableObject deck, CardDataScriptableObject card) {
            if (!CanAdd(deck, card)) return false;
            deck.SetCards(deck.Cards.Append(card));
            Save();
            return true;
        }

        /// <summary>Takes one copy of the card out.</summary>
        public static bool Remove(DeckScriptableObject deck, CardDataScriptableObject card) {
            if (!IsPlayers(deck) || CopiesOf(deck, card) == 0) return false;
            var cards = deck.Cards.ToList();
            cards.RemoveAt(cards.LastIndexOf(card));
            deck.SetCards(cards);
            Save();
            return true;
        }

        /// <summary>Renames the deck; a blank name keeps the old one, a long one is cut to 20 characters, and a name another deck has gets a number.</summary>
        public static void Rename(DeckScriptableObject deck, string name) {
            if (!IsPlayers(deck) || string.IsNullOrWhiteSpace(name) || name.Trim() == deck.GetDeckName()) return;
            deck.SetName(UniqueName(name.Trim(), deck));
            Save();
        }

        public static void Delete(DeckScriptableObject deck) {
            if (!IsPlayers(deck)) return;
            decks.Remove(deck);
            Save();
        }

        // "New deck", then "New deck 2", "New deck 3"...: no two decks share a name, starter decks included, and none is
        // longer than 20 characters (the number included)
        private static string UniqueName(string name, DeckScriptableObject renamed) {
            var taken = new HashSet<string>(decks.Where(d => d != renamed).Select(d => d.GetDeckName())
                .Concat(catalog != null ? catalog.AllDecks.Select(d => d.GetDeckName()) : Enumerable.Empty<string>()), StringComparer.OrdinalIgnoreCase);
            string Cut(string text, int room) => text.Length > room ? text.Substring(0, room).TrimEnd() : text;
            string candidate = Cut(name, MaxNameLength);
            for (int number = 2; taken.Contains(candidate); number++) candidate = $"{Cut(name, MaxNameLength - 1 - number.ToString().Length)} {number}";
            return candidate;
        }

        private static void Load(DeckCatalog cardsFrom) {
            if (decks != null && (cardsFrom == null || cardsFrom == catalog)) return;
            catalog = cardsFrom;
            decks = new List<DeckScriptableObject>();
            if (!File.Exists(FilePath)) return;
            try {
                var saved = JsonUtility.FromJson<SavedDecks>(File.ReadAllText(FilePath));
                var pool = catalog != null ? catalog.AllCards : new List<CardDataScriptableObject>();
                foreach (var entry in saved?.decks ?? new List<SavedDeck>()) {
                    var cards = entry.cards.Select(c => pool.FirstOrDefault(p => p.cardId == c.id) ?? pool.FirstOrDefault(p => p.cardName == c.name)).ToList();
                    int missing = cards.Count(c => c == null);
                    if (missing > 0) LogWarning($"Your deck '{entry.name}' had {missing} cards that no longer exist; they were taken out", LogTag.Cards);
                    decks.Add(DeckScriptableObject.Make(entry.name, cards));
                }
            }
            catch (Exception e) {
                LogError($"Couldn't read your decks from {FilePath}: {e.Message}", LogTag.Cards);
            }
        }

        private static void Save() {
            var saved = new SavedDecks {
                decks = decks.Select(d => new SavedDeck {
                    name = d.GetDeckName(),
                    cards = d.Cards.Select(c => new SavedCard { id = c.cardId, name = c.cardName }).ToList()
                }).ToList()
            };
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(saved, true));
            }
            catch (Exception e) {
                LogError($"Couldn't save your decks to {FilePath}: {e.Message}", LogTag.Cards);
            }
        }
    }
}
