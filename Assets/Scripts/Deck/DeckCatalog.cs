using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
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
        // An adventure's potions: found and bought on the way, never in a deck (see AdventureRun.Potions)
        [SerializeField] private List<CardDataScriptableObject> potions = new List<CardDataScriptableObject>();
        public IReadOnlyList<CardDataScriptableObject> Potions => potions.Where(p => p != null).ToList();

        public CardController CardPrefab => cardPrefab;

        // Mods (the user, 2026-10-17): decks and opponents loaded from files when the menu opens, never saved in the asset
        [NonSerialized] private readonly List<DeckScriptableObject> modDecks = new List<DeckScriptableObject>();
        [NonSerialized] private readonly List<Opponent> modOpponents = new List<Opponent>();

        public void AddMod(IEnumerable<DeckScriptableObject> moddedDecks, IEnumerable<Opponent> moddedOpponents) {
            modDecks.AddRange(moddedDecks.Where(d => d != null));
            modOpponents.AddRange(moddedOpponents.Where(o => o?.deck != null));
        }

        /// <summary>Drops the mods loaded into this asset (a new play session loads them again; the old decks are dead by then).</summary>
        public void ForgetMods() {
            modDecks.Clear();
            modOpponents.Clear();
        }

        // A mod deck is a runtime object: dead once play mode was left, so it's checked like the asset ones
        public IReadOnlyList<DeckScriptableObject> Decks => decks.Concat(modDecks).Where(d => d != null).ToList();
        public IReadOnlyList<Opponent> Opponents => opponents.Concat(modOpponents).Where(o => o != null && o.deck != null).ToList();
        public IReadOnlyList<DeckScriptableObject> TestDecks => testDecks.Where(d => d != null).ToList();
        /// <summary>Everything a player can pick: the faction decks, then the test decks.</summary>
        public IReadOnlyList<DeckScriptableObject> AllDecks => Decks.Concat(TestDecks).ToList();

        /// <summary>Every different card in any deck on offer, by faction then name.</summary>
        public IReadOnlyList<CardDataScriptableObject> AllCards => AllDecks.SelectMany(d => d.Cards).Where(c => c != null).Distinct()
            .OrderBy(c => c.faction).ThenBy(c => c.cardName).ToList();

        /// <summary>"17 cards · Corporate", "13 cards · Shadow Ops. + Mind": the size of the deck, and its faction or two.</summary>
        public static string Describe(DeckScriptableObject deck) {
            var factions = FactionsOf(deck);
            string summary = $"{deck.Cards.Count} cards";
            return factions.Count is 1 or 2 ? $"{summary} · {string.Join(" + ", factions.Select(CardTextFormatter.FactionName))}" : summary;
        }

        /// <summary>The factions of the deck's cards, the one with the most cards first; as many cards: by name.</summary>
        public static List<Faction> FactionsOf(DeckScriptableObject deck) {
            if (deck == null) return new List<Faction>();
            return deck.Cards.Where(c => c != null).GroupBy(c => c.faction).OrderByDescending(g => g.Count())
                .ThenBy(g => CardTextFormatter.FactionName(g.Key)).Select(g => g.Key).ToList();
        }

        /// <summary>The factions a menu shows a deck's coins for: one or two (a deck mixing more shows none), any number for your own decks.</summary>
        public static List<Faction> CoinsFor(DeckScriptableObject deck) {
            var factions = FactionsOf(deck).Where(f => f != Faction.None).ToList();
            return factions.Count <= 2 || PlayerDecks.IsPlayers(deck) ? factions : new List<Faction>();
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
