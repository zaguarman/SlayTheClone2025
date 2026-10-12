using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The order a Day resolves in (GDD 3.7): layer by layer (moves, summons, spells, then abilities and attacks),
    /// and inside a layer by Speed, then the tie-breakers. Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class ResolutionOrderTests {
        private TestGame game;
        private Player p1;
        private Player p2;

        [SetUp]
        public void SetUp() {
            game = new TestGame();
            p1 = game.Player1;
            p2 = game.Player2;
        }

        [TearDown]
        public void TearDown() => game.Dispose();

        [Test]
        public void ActionsResolveLayerByLayer_MovesSummonsSpellsThenAbilitiesAndAttacks() {
            var walker = game.Summon(p1, game.Creature("Walker", 1, 3), 0);
            var mover = game.Summon(p1, game.Creature("Mover", 1, 3), 2); // A creature has one intent a Day, so another one moves
            var played = game.GiveCard(p1, game.Creature("Played", 1, 1, speed: 2));
            var summoned = (ICreature)CardFactory.CreateCard(game.Creature("Summoned", 1, 1));
            var zap = game.GiveCard(p1, game.Spell("Zap", TargetType.Enemy, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.Enemy)));
            game.History.Clear();

            // Queued in the opposite order
            game.Declare(walker, p2.Battlefield[4]);
            game.ActionsQueue.AddAction(new PlayCardAction(zap, p1, null));
            game.ActionsQueue.AddAction(new SummonCreatureAction(summoned, p2, p2.Battlefield[0]));
            game.ActionsQueue.AddAction(new PlayCardAction(played, p1, p1.Battlefield[3]));
            game.ActionsQueue.AddAction(new MoveCreatureAction(mover, p1.Battlefield[2], p1.Battlefield[1], p1));
            game.Resolve();

            CollectionAssert.AreEqual(new[] {
                "MoveCreature",
                "PlayCard", "SummonCreature", // The faster creature enters first; playing it summons it right away
                "SummonCreature",
                "PlayCard", "PlaySpell", "DamagePlayer", // A spell's effects land as part of it
                "BattlefieldCombat", "DamagePlayer" // So does an attack's hit
            }, game.History.ExecutedKinds());
        }

        /// <summary>Two creatures attack each other: the first to strike can kill the other before it gets to.</summary>
        [Test]
        public void InALayer_TheFasterActsFirst_ThenTheWeaker_ThenTheLowerAttack() {
            using (var g = new TestGame()) {
                var fast = g.Summon(g.Player1, g.Creature("Fast", 5, 3, speed: 2), 0);
                var slow = g.Summon(g.Player2, g.Creature("Slow", 5, 3), 0);
                g.Declare(slow, g.Player1.Battlefield[0]);
                g.Declare(fast, g.Player2.Battlefield[0]);
                g.Resolve();
                Assert.IsTrue(slow.IsDead);
                Assert.AreEqual(3, fast.Health, "The faster creature strikes first, so the slower one never hits back");
            }
            using (var g = new TestGame()) {
                var tough = g.Summon(g.Player1, g.Creature("Tough", 5, 5), 0);
                var frail = g.Summon(g.Player2, g.Creature("Frail", 5, 3), 0);
                g.Declare(tough, g.Player2.Battlefield[0]);
                g.Declare(frail, g.Player1.Battlefield[0]);
                g.Resolve();
                Assert.IsTrue(tough.IsDead);
                Assert.AreEqual(3, frail.Health, "At equal Speed the one with less Health strikes first");
            }
            using (var g = new TestGame()) {
                var hard = g.Summon(g.Player1, g.Creature("Hard", 4, 4), 0);
                var soft = g.Summon(g.Player2, g.Creature("Soft", 2, 4), 0);
                g.Declare(hard, g.Player2.Battlefield[0]);
                g.Declare(soft, g.Player1.Battlefield[0]);
                g.Resolve();
                Assert.IsTrue(soft.IsDead);
                Assert.AreEqual(2, hard.Health, "At equal Speed and Health the one with lower base Attack strikes first");
            }
        }

        /// <summary>
        /// A 2-health guard is attacked for 2 the same turn its side gets +2 attack or armor, from a creature entering or a spell.
        /// Entry effects land as part of the summon, and spells have their own layer, both before attacks.
        /// </summary>
        [Test]
        public void HelpPlayedThisTurn_LandsBeforeThisTurnsAttacks() {
            var buff = Effect(EffectTrigger.OnPlay, ActionType.ModifyStat, 2, TargetType.FriendlyCreatures);
            var armor = Effect(EffectTrigger.OnPlay, ActionType.Armor, 2, TargetType.FriendlyCreatures);
            foreach (var (help, isSpell, what) in new[] { (buff, false, "attack"), (armor, false, "armor"), (armor, true, "armor") }) {
                using (var g = new TestGame()) {
                    var guard = g.Summon(g.Player1, g.Creature("Guard", 0, 2), 0);
                    var brute = g.Summon(g.Player2, g.Creature("Brute", 2, 3), 0);
                    var helper = g.GiveCard(g.Player1, isSpell
                        ? g.Spell("Shield", TargetType.FriendlyCreatures, help)
                        : g.Creature("Helper", 0, 3, 1, help));

                    g.Declare(brute, g.Player1.Battlefield[0]);
                    g.ActionsQueue.AddAction(new PlayCardAction(helper, g.Player1, g.Player1.Battlefield[isSpell ? 0 : 1]));
                    g.Resolve();

                    Assert.IsFalse(guard.IsDead, $"The {(isSpell ? "spell" : "entering creature")}'s +2 {what} should land before the brute's attack");
                }
            }
        }
    }
}
