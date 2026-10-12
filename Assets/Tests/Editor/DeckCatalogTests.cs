using System.Linq;
using NUnit.Framework;
using UnityEditor;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// What the main menu offers: decks of one faction or a pair of them (one deck each), and one opponent bringing each deck.
    /// </summary>
    public class DeckCatalogTests {
        private const int MinDeckSize = 12;
        private const int MaxDeckSize = 20;

        private DeckCatalog catalog;

        [SetUp]
        public void SetUp() {
            var guids = AssetDatabase.FindAssets("t:DeckCatalog");
            Assert.AreEqual(1, guids.Length, "There should be exactly one deck catalog");
            catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [Test]
        public void EveryDeck_IsOneOrTwoFactions_12To20DifferentCards_BroughtByOneOpponent() {
            Assert.IsNotEmpty(catalog.Decks);
            var factions = catalog.Decks.Select(deck => {
                var deckFactions = DeckCatalog.FactionsOf(deck);
                Assert.That(deckFactions.Count, Is.InRange(1, 2), $"{deck.GetDeckName()} mixes too many factions: {string.Join(", ", deckFactions)}");
                CollectionAssert.DoesNotContain(deckFactions, Faction.None, $"{deck.GetDeckName()} has cards without a faction");
                return string.Join(" + ", deckFactions.OrderBy(f => f));
            }).ToList();
            CollectionAssert.AllItemsAreUnique(factions, "Each faction, or pair of factions, has a single deck for now");

            foreach (var offered in catalog.Decks) {
                string name = offered.GetDeckName();
                Assert.That(offered.Cards.Count, Is.InRange(MinDeckSize, MaxDeckSize), $"{name} has {offered.Cards.Count} cards");
                CollectionAssert.AllItemsAreUnique(offered.Cards, $"{name} has copies of a card");
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
