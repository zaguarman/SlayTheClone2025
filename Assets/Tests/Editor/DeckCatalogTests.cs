using System.Linq;
using NUnit.Framework;
using UnityEditor;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// What the main menu offers: one deck per faction, each made of that faction's cards only, and one opponent bringing each deck.
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
        public void EveryDeck_IsOneFactionsOwn_12To20DifferentCards_BroughtByOneOpponent() {
            Assert.IsNotEmpty(catalog.Decks);
            var factions = catalog.Decks.Select(deck => {
                var deckFactions = deck.Cards.Select(card => card.faction).Distinct().ToList();
                Assert.AreEqual(1, deckFactions.Count, $"{deck.GetDeckName()} mixes factions: {string.Join(", ", deckFactions)}");
                Assert.AreNotEqual(Faction.None, deckFactions[0], $"{deck.GetDeckName()}'s cards have no faction");
                return deckFactions[0];
            }).ToList();
            CollectionAssert.AllItemsAreUnique(factions, "Each faction has a single deck for now");

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
