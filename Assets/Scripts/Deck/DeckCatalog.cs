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

        public IReadOnlyList<DeckScriptableObject> Decks => decks.Where(d => d != null).ToList();
        public IReadOnlyList<Opponent> Opponents => opponents.Where(o => o != null && o.deck != null).ToList();

        /// <summary>"17 cards · Corporate": the size of the deck, and its faction when every card shares one.</summary>
        public static string Describe(DeckScriptableObject deck) {
            var factions = deck.Cards.Where(c => c != null).Select(c => c.faction).Distinct().ToList();
            string summary = $"{deck.Cards.Count} cards";
            return factions.Count == 1 && factions[0] != Faction.None
                ? $"{summary} · {CardTextFormatter.FactionName(factions[0])}"
                : summary;
        }
    }
}
