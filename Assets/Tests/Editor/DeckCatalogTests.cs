using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using static Zeitgeist.Enums;

namespace Zeitgeist.Tests {
    /// <summary>
    /// What the main menu offers: decks of one faction or a pair of them (one deck each), built by the deck builder's rules,
    /// and one opponent bringing each deck.
    /// </summary>
    public class DeckCatalogTests {

        private DeckCatalog catalog;

        [SetUp]
        public void SetUp() {
            var guids = AssetDatabase.FindAssets("t:DeckCatalog");
            Assert.AreEqual(1, guids.Length, "There should be exactly one deck catalog");
            catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [Test]
        public void EveryDeck_IsOneOrTwoFactions_20To40Cards_BroughtByOneOpponent() {
            Assert.IsNotEmpty(catalog.Decks);
            var factions = catalog.Decks.Select(deck => {
                var deckFactions = DeckCatalog.FactionsOf(deck);
                Assert.That(deckFactions.Count, Is.InRange(1, 2), $"{deck.GetDeckName()} mixes too many factions: {string.Join(", ", deckFactions)}");
                CollectionAssert.DoesNotContain(deckFactions, Faction.None, $"{deck.GetDeckName()} has cards without a faction");
                return string.Join(" + ", deckFactions.OrderBy(f => f));
            }).ToList();
            CollectionAssert.IsSubsetOf(Enum.GetValues(typeof(Faction)).Cast<Faction>().Where(f => f != Faction.None && f != Faction.DigitalSociety).Select(f => f.ToString()).ToList(), factions, // Digital Society merged into Pop Culture (2026-10-17)
                "Every faction has a deck of its own");
            CollectionAssert.AllItemsAreUnique(catalog.Decks.Select(d => d.GetDeckName()).ToList(), "Every deck has its own name (a faction can have more than one: Shadow Ops. - Black Budget)");

            foreach (var offered in catalog.Decks) {
                string name = offered.GetDeckName();
                Assert.That(offered.Cards.Count, Is.InRange(PlayerDecks.MinDeckSize, PlayerDecks.MaxDeckSize), $"{name} has {offered.Cards.Count} cards");
                Assert.LessOrEqual(offered.Cards.Count, 48, $"{name} has {offered.Cards.Count} cards: a starter deck past 48 plays thin (the user, 2026-10-17)");
                Assert.IsTrue(offered.Cards.Any(c => c is CreatureCardScriptableObject), $"{name} has no creatures");
                Assert.IsTrue(offered.Cards.Any(c => c is SpellCardScriptableObject), $"{name} has no spells");
            }

            CollectionAssert.AreEquivalent(catalog.Decks, catalog.Opponents.Select(o => o.deck),
                "Every opponent should bring one of the decks on offer, and every deck should have its opponent");
            foreach (var opponent in catalog.Opponents) {
                Assert.IsFalse(string.IsNullOrWhiteSpace(opponent.name), "An opponent has no name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(opponent.tagline), $"{opponent.name} has no tagline");
            }
        }
    }
}
