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
        // How a card looks, for showing the cards outside a game (the menu's card gallery)
        [SerializeField] private CardController cardPrefab;

        public CardController CardPrefab => cardPrefab;

        public IReadOnlyList<DeckScriptableObject> Decks => decks.Where(d => d != null).ToList();
        public IReadOnlyList<Opponent> Opponents => opponents.Where(o => o != null && o.deck != null).ToList();
        public IReadOnlyList<DeckScriptableObject> TestDecks => testDecks.Where(d => d != null).ToList();
        /// <summary>Everything a player can pick: the faction decks, then the test decks.</summary>
        public IReadOnlyList<DeckScriptableObject> AllDecks => Decks.Concat(TestDecks).ToList();

        /// <summary>Every different card in any deck on offer, by faction then name.</summary>
        public IReadOnlyList<CardDataScriptableObject> AllCards => AllDecks.SelectMany(d => d.Cards).Where(c => c != null).Distinct()
            .OrderBy(c => c.faction).ThenBy(c => c.cardName).ToList();

        /// <summary>"17 cards · Corporate", "13 cards · Shadow Ops. + Mind": the size of the deck, and its faction or two, each with its emblem.</summary>
        public static string Describe(DeckScriptableObject deck) {
            var factions = FactionsOf(deck);
            string summary = $"{deck.Cards.Count} cards";
            return factions.Count is 1 or 2 ? $"{summary} · {string.Join(" + ", factions.Select(CardTextFormatter.FactionLabel))}" : summary;
        }

        /// <summary>The factions of the deck's cards, the one with the most cards first.</summary>
        public static List<Faction> FactionsOf(DeckScriptableObject deck) {
            if (deck == null) return new List<Faction>();
            return deck.Cards.Where(c => c != null).GroupBy(c => c.faction).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
        }

        /// <summary>
        /// The deck's faction: the one every card shares, or the main one of a two-faction deck (it dresses the table).
        /// None for a deck mixing more, or a missing one.
        /// </summary>
        public static Faction FactionOf(DeckScriptableObject deck) {
            var factions = FactionsOf(deck);
            return factions.Count is 1 or 2 ? factions[0] : Faction.None;
        }
    }
}
