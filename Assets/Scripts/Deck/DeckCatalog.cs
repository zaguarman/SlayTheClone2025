using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// What the main menu offers: the decks you can play and the opponents you can face, each with the deck they bring.
    /// The opponent's deck can still be swapped for any deck in the list.
    /// </summary>
    [CreateAssetMenu(fileName = "DeckCatalog", menuName = "Cards/Deck Catalog")]
    public class DeckCatalog : ScriptableObject {
        [Serializable]
        public class Opponent {
            public string name;
            [TextArea] public string tagline;
            public DeckScriptableObject deck;
        }

        [SerializeField] private List<DeckScriptableObject> decks = new List<DeckScriptableObject>();
        [SerializeField] private List<Opponent> opponents = new List<Opponent>();
        // Decks for trying out mechanics, which can mix factions; offered next to the faction decks but brought by no opponent
        [SerializeField] private List<DeckScriptableObject> testDecks = new List<DeckScriptableObject>();

        public IReadOnlyList<DeckScriptableObject> Decks => decks.Where(d => d != null).ToList();
        public IReadOnlyList<Opponent> Opponents => opponents.Where(o => o != null && o.deck != null).ToList();
        public IReadOnlyList<DeckScriptableObject> TestDecks => testDecks.Where(d => d != null).ToList();
        /// <summary>Everything a player can pick: the faction decks, then the test decks.</summary>
        public IReadOnlyList<DeckScriptableObject> AllDecks => Decks.Concat(TestDecks).ToList();

        /// <summary>"17 cards · Corporate": the size of the deck, and its faction when every card shares one.</summary>
        public static string Describe(DeckScriptableObject deck) {
            var faction = FactionOf(deck);
            string summary = $"{deck.Cards.Count} cards";
            return faction != Faction.None ? $"{summary} · {CardTextFormatter.FactionName(faction)}" : summary;
        }

        /// <summary>The faction every card of the deck shares; None for a mixed or missing deck.</summary>
        public static Faction FactionOf(DeckScriptableObject deck) {
            if (deck == null) return Faction.None;
            var factions = deck.Cards.Where(c => c != null).Select(c => c.faction).Distinct().ToList();
            return factions.Count == 1 ? factions[0] : Faction.None;
        }
    }
}
