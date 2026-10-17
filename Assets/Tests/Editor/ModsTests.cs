using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist.Tests {
    /// <summary>Mods ('Modding.txt'): the example mod file parses, its cards read as the game's own, and its deck is offered.</summary>
    public class ModsTests {
        [Test]
        public void TheExampleMod_Parses_ItsCardsRead_AndItsDeckIsOffered() {
            string path = Path.Combine(Application.dataPath, "..", "Mods", "Example", "example.json");
            Assert.IsTrue(File.Exists(path), $"The example mod should ship at {path}");
            var guids = AssetDatabase.FindAssets("t:DeckCatalog");
            var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));

            var mod = Mods.Parse(File.ReadAllText(path));
            Assert.AreEqual("Example Mod", mod.mod);
            var (cards, decks, opponents) = Mods.Build(mod, Path.GetDirectoryName(path), catalog);
            CollectionAssert.IsEmpty(Mods.Problems.Where(p => p.StartsWith("Example Mod") || p.StartsWith("Office") || p.StartsWith("Reply")), "The example has no mistakes");
            Assert.AreEqual(2, cards.Count);

            var printer = cards.Single(c => c.cardName == "Office Printer") as CreatureCardScriptableObject;
            Assert.IsNotNull(printer, "A creature card");
            Assert.AreEqual((1, 7, Faction.Corporate, CreatureAbility.Litigious), (printer.attack, printer.health, printer.faction, printer.ability));
            Assert.AreEqual("mod:Example Mod:Office Printer", printer.cardId, "Its id says which mod it's from");
            StringAssert.Contains("1 damage", Keywords.Plain(CardTextFormatter.GetRulesText(printer.ToCardData())), "Its text is built from its actions, like every card's");
            StringAssert.Contains("Litigious", Keywords.Plain(CardTextFormatter.GetRulesText(printer.ToCardData())));

            var replyAll = cards.Single(c => c.cardName == "Reply All") as SpellCardScriptableObject;
            Assert.IsNotNull(replyAll, "A spell card");
            Assert.AreEqual(TargetType.Enemy, replyAll.defaultTargetType);
            Assert.AreEqual(2, replyAll.ToCardData().effects[0].actions.Count, "Two actions: the damage and the draw");

            Assert.AreEqual(1, decks.Count);
            Assert.AreEqual("Example - Paper Jam", decks[0].GetDeckName());
            Assert.AreEqual(20, decks[0].Cards.Count, "Its own cards and the game's, by name");
            Assert.IsTrue(decks[0].Cards.Any(c => c.cardName == "Fat Tony"), "A game card named in a mod's deck is that card");
            Assert.AreEqual("The Office Manager", opponents.Single().name);
        }
    }
}
