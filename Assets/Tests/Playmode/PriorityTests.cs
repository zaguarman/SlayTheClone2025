using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Priority (Action) (GDD 3.7): inside a layer, higher Priority goes first, whatever the Speed. It's printed on cards
    /// (a spell's Spell Action, a creature's attacks) and changed by Boosts and Penalties, on one creature or a whole side.
    /// Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class PriorityTests {
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

        private static CardEffect ChangePriority(int amount, int days, TargetType target) => new CardEffect {
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction { actionType = ActionType.ModifyPriority, value = amount, statusDuration = days, targetType = target }
            }
        };

        // Two 5/3 creatures attack each other: whoever strikes first kills the other before it hits back
        private (ICreature slow, ICreature fast) Duel(int slowPriority = 0) {
            var slowData = game.Creature("Slow", 5, 3);
            slowData.priority = slowPriority;
            var slow = game.Summon(p1, slowData, 0);
            var fast = game.Summon(p2, game.Creature("Fast", 5, 3, speed: 2), 0);
            game.Declare(slow, p2.Battlefield[0]);
            game.Declare(fast, p1.Battlefield[0]);
            return (slow, fast);
        }

        [Test]
        public void APriorityAttack_StrikesBeforeAFasterCreature() {
            var (slow, fast) = Duel(slowPriority: 1);
            game.Resolve();

            Assert.IsTrue(fast.IsDead);
            Assert.AreEqual(3, slow.Health, "Priority +1 beats Speed 2");
        }

        [Test]
        public void SpellsWithHigherPriority_ResolveFirst_WhicheverOrderTheyWerePlayedIn() {
            foreach (bool earlyFirst in new[] { true, false }) {
                using (var g = new TestGame()) {
                    var earlyData = g.Spell("Early", TargetType.Enemy, Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Enemy));
                    earlyData.priority = 1;
                    var early = g.GiveCard(g.Player1, earlyData);
                    var late = g.GiveCard(g.Player2, g.Spell("Late", TargetType.Enemy, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.Enemy)));

                    var plays = new[] { new PlayCardAction(early, g.Player1, null), new PlayCardAction(late, g.Player2, null) };
                    foreach (var play in earlyFirst ? plays : plays.Reverse()) g.ActionsQueue.AddAction(play);
                    g.Resolve();

                    CollectionAssert.AreEqual(new[] { 2, 1 }, g.History.PlayerDamage.Select(d => d.Amount).ToList(),
                        $"The Priority +1 spell lands first (queued {(earlyFirst ? "first" : "second")})");
                }
            }
        }

        [Test]
        public void APriorityBoost_LandsBeforeAttacks_AndMakesTheCreatureStrikeFirstThatDay() {
            var (slow, fast) = Duel();
            var rally = game.GiveCard(p1, game.Spell("Rally", TargetType.FriendlyCreatures, ChangePriority(1, 1, TargetType.FriendlyCreatures)));
            game.Play(rally, p1, p1.Battlefield[0]);
            game.Resolve();

            Assert.IsTrue(fast.IsDead, "Spells resolve before attacks, so the boost is there when they're ordered");
            Assert.AreEqual(3, slow.Health);
        }

        [Test]
        public void APriorityPenalty_MakesTheCreatureActAfterASlowerOne() {
            var slow = game.Summon(p1, game.Creature("Slow", 5, 3), 0);
            var fast = game.Summon(p2, game.Creature("Fast", 5, 3, speed: 2), 0);
            game.Run(new ModifyPriorityAction(fast, -1, 1));
            game.Declare(slow, p2.Battlefield[0]);
            game.Declare(fast, p1.Battlefield[0]);
            game.Resolve();

            Assert.IsTrue(fast.IsDead, "Priority -1: the faster creature now strikes last");
            Assert.AreEqual(3, slow.Health);
        }

        [Test]
        public void ABoostForOneDay_EndsAtThatDaysDusk() {
            var creature = game.Summon(p1, game.Creature("Boosted", 1, 3), 0);
            game.Run(new ModifyPriorityAction(creature, 2, 1));
            Assert.AreEqual(2, game.ModifierManager.PriorityOf(creature));

            game.StartTurn();
            Assert.AreEqual(0, game.ModifierManager.PriorityOf(creature), "It isn't a status: it works the Day it's applied and ends at that Dusk");
        }

        [Test]
        public void ASideWideBoost_CoversCreaturesThatArriveLater_ForItsDays() {
            var here = game.Summon(p1, game.Creature("Here", 1, 3), 0);
            var enemy = game.Summon(p2, game.Creature("Enemy", 1, 3), 0);
            var advance = game.GiveCard(p1, game.Spell("Coordinated Advance", TargetType.Player, ChangePriority(1, 2, TargetType.Player)));
            game.Play(advance, p1, null);
            game.Resolve();

            var later = game.Summon(p1, game.Creature("Later", 1, 3), 1);
            Assert.AreEqual(1, game.ModifierManager.PriorityOf(here));
            Assert.AreEqual(1, game.ModifierManager.PriorityOf(later), "Every creature you control, including new ones");
            Assert.AreEqual(0, game.ModifierManager.PriorityOf(enemy), "Not the enemy's");

            game.StartTurn();
            Assert.AreEqual(1, game.ModifierManager.PriorityOf(here), "2 Days: this one and the next");
            game.StartTurn();
            Assert.AreEqual(0, game.ModifierManager.PriorityOf(here));
        }

        [Test]
        public void PriorityIsPrintedOnTheCard() {
            var spell = game.Spell("Quick", TargetType.Enemy, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.Enemy));
            spell.priority = 1;
            var creature = game.Creature("Striker", 2, 2);
            creature.priority = 1;
            var advance = game.Spell("Advance", TargetType.Player, ChangePriority(1, 4, TargetType.Player));

            StringAssert.StartsWith("<b>Priority +1</b>.", CardTextFormatter.GetRulesText(spell));
            StringAssert.StartsWith("<b>Priority Attack +1</b>.", CardTextFormatter.GetRulesText(creature));
            StringAssert.Contains("Actions by creatures you control get +1 Priority this Day and the next 3 Days.", CardTextFormatter.GetRulesText(advance));
        }
    }
}
