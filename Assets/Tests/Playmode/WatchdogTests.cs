using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Watchdogs (GDD 3.11): created through the queue, they nullify, redirect, reorder or reveal matching actions, On Watch
    /// (just before each action) or at the Briefing (once a Day, from the Day after they're created). Each trigger uses a
    /// charge; when several match, the oldest triggers. Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class WatchdogTests {
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

        private static WatchdogSpec Spec(WatchdogEffect effect, WatchedAction watches = WatchedAction.Attack, WatchdogSide side = WatchdogSide.Enemy,
            int charges = 1, WatchdogTiming timing = WatchdogTiming.OnWatch, BriefingPick pick = BriefingPick.First, int atMost = 0) =>
            new WatchdogSpec { effect = effect, watches = watches, side = side, charges = charges, timing = timing, pick = pick, atMost = atMost };

        private void Watch(WatchdogSpec spec, IPlayer controller, ICreature creature = null, int days = 0) =>
            game.Run(new CreateWatchdogAction(spec, controller, creature, days));

        [Test]
        public void Nullify_StopsTheMatchingAction_AndUsesItsCharge() {
            game.Summon(p1, game.Creature("Guard", 0, 10), 0);
            var brute = game.Summon(p2, game.Creature("Brute", 3, 10), 0);
            Watch(Spec(WatchdogEffect.Nullify), p1);

            game.Attack(brute, p1.Battlefield[1]); // An empty slot: it would hit player 1
            Assert.AreEqual(Player.MaxHealth, p1.Health, "Nullified");
            Assert.IsEmpty(game.ActionsQueue.Watchdogs, "Its only charge is used: it's gone");

            game.Attack(brute, p1.Battlefield[1]);
            Assert.AreEqual(Player.MaxHealth - 3, p1.Health);
        }

        [Test]
        public void AWatchdogCreatedBySpell_CatchesAttacksQueuedBeforeIt_ThatHaventResolvedYet() {
            var brute = game.Summon(p2, game.Creature("Brute", 3, 10), 0);
            var firewall = game.GiveCard(p1, game.Spell("Firewall", TargetType.Player, new CardEffect {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction> {
                    new EffectAction { actionType = ActionType.CreateWatchdog, targetType = TargetType.Enemy, watchdog = Spec(WatchdogEffect.Nullify) }
                }
            }));

            game.Declare(brute, p1.Battlefield[0]); // Planned first
            game.Play(firewall, p1, null);
            game.Resolve(); // The spell layer comes before attacks

            Assert.AreEqual(Player.MaxHealth, p1.Health);
        }

        [Test]
        public void Redirect_SendsTheAttackToAnotherCreature_AtRandom() {
            var aimedAt = game.Summon(p1, game.Creature("Aimed At", 0, 10), 0);
            var decoy = game.Summon(p1, game.Creature("Decoy", 0, 10), 3);
            var brute = game.Summon(p2, game.Creature("Brute", 3, 10), 0);
            Watch(Spec(WatchdogEffect.Redirect), p1, creature: brute); // "attacks by Creature C"

            game.Attack(brute, p1.Battlefield[0]);

            Assert.AreEqual(10, aimedAt.Health);
            Assert.AreEqual(7, decoy.Health, "The only creature that is neither the attacker nor its target");
        }

        [Test]
        public void Redirect_GoesToTheCreatureWithTheMostHeat_OnEitherSide() {
            var aimedAt = game.Summon(p1, game.Creature("Aimed At", 0, 10), 0);
            game.Summon(p1, game.Creature("Bystander", 0, 10), 3);
            var brute = game.Summon(p2, game.Creature("Brute", 3, 10), 0);
            var hot = game.Summon(p2, game.Creature("Hot Ally", 0, 10), 4);
            game.Run(new ApplyStatusEffectAction(hot, StatusEffectType.HeatSignature, 1, 2));
            Watch(Spec(WatchdogEffect.Redirect), p1);

            game.Attack(brute, p1.Battlefield[0]);

            Assert.AreEqual(10, aimedAt.Health);
            Assert.AreEqual(7, hot.Health, "Heat draws it, even onto the attacker's own side");
        }

        [Test]
        public void Reorder_OnWatch_MovesTheActionToTheEndOfItsLayer() {
            var slow = game.Summon(p1, game.Creature("Slow", 5, 3), 0);
            var fast = game.Summon(p2, game.Creature("Fast", 5, 3, speed: 2), 0);
            Watch(Spec(WatchdogEffect.Reorder), p1);
            game.Declare(slow, p2.Battlefield[0]);
            game.Declare(fast, p1.Battlefield[0]);

            game.Resolve();

            Assert.IsTrue(fast.IsDead, "The faster creature's attack was moved after the slower one's");
            Assert.AreEqual(3, slow.Health);
        }

        [Test]
        public void Briefing_StartsTheDayAfter_AndCanPickEveryMatch_OneChargePerAction() {
            var a = game.Summon(p2, game.Creature("A", 2, 10), 0);
            var b = game.Summon(p2, game.Creature("B", 3, 10), 1);
            var c = game.Summon(p2, game.Creature("C", 4, 10), 2);
            Watch(Spec(WatchdogEffect.Nullify, timing: WatchdogTiming.Briefing, pick: BriefingPick.Every, charges: 3), p1);

            game.Declare(a, p1.Battlefield[0]);
            game.ActionsQueue.BriefWatchdogs();
            game.Resolve();
            Assert.AreEqual(Player.MaxHealth - 2, p1.Health, "Created today: it briefs from tomorrow");

            game.StartTurn();
            game.Declare(a, p1.Battlefield[0]);
            game.Declare(b, p1.Battlefield[1]);
            game.ActionsQueue.BriefWatchdogs();
            game.Resolve();
            Assert.AreEqual(Player.MaxHealth - 2, p1.Health, "Every enemy attack nullified");
            Assert.AreEqual(1, game.ActionsQueue.Watchdogs.Single().ChargesLeft, "One charge per action it changed");

            game.StartTurn();
            game.Declare(a, p1.Battlefield[0]);
            game.Declare(c, p1.Battlefield[2]);
            game.ActionsQueue.BriefWatchdogs();
            game.Resolve();
            Assert.AreEqual(Player.MaxHealth - 2 - 4, p1.Health, "Its last charge only stops the first: C's attack lands");
            Assert.IsEmpty(game.ActionsQueue.Watchdogs);
        }

        [Test]
        public void Briefing_CanPickTheStrongest() {
            var weak = game.Summon(p2, game.Creature("Weak", 2, 10), 0);
            var strong = game.Summon(p2, game.Creature("Strong", 5, 10), 1);
            Watch(Spec(WatchdogEffect.Nullify, timing: WatchdogTiming.Briefing, pick: BriefingPick.Strongest), p1);
            game.StartTurn();

            game.Declare(weak, p1.Battlefield[0]);
            game.Declare(strong, p1.Battlefield[1]);
            game.ActionsQueue.BriefWatchdogs();
            game.Resolve();

            Assert.AreEqual(Player.MaxHealth - 2, p1.Health, "Only the weak attack lands");
        }

        [Test]
        public void Conditions_SideAndStrength_AndOnlyTheOldestMatchTriggers() {
            var mine = game.Summon(p1, game.Creature("Mine", 4, 10), 0);
            var theirs = game.Summon(p2, game.Creature("Theirs", 4, 10), 0);
            Watch(Spec(WatchdogEffect.Nullify, atMost: 3), p1);
            game.Attack(theirs, p1.Battlefield[1]);
            Assert.AreEqual(Player.MaxHealth - 4, p1.Health, "Attack 4 isn't 3 or less");

            Watch(Spec(WatchdogEffect.Nullify), p1);
            game.Attack(mine, p2.Battlefield[1]);
            Assert.AreEqual(Player.MaxHealth - 4, p2.Health, "It watches enemy attacks, not friendly ones");

            Watch(Spec(WatchdogEffect.Reveal), p1);
            game.Attack(theirs, p1.Battlefield[1]);
            Assert.AreEqual(Player.MaxHealth - 4, p1.Health, "The older Nullify triggered");
            Assert.AreEqual(2, game.ActionsQueue.Watchdogs.Count, "The newer one kept its charge (with the strength one)");
        }

        [Test]
        public void AWatchdogWithDays_EndsWhenTheyRunOut() {
            var brute = game.Summon(p2, game.Creature("Brute", 3, 10), 0);
            Watch(Spec(WatchdogEffect.Nullify, charges: 5), p1, days: 1);

            game.StartTurn();
            game.Attack(brute, p1.Battlefield[0]);
            Assert.AreEqual(Player.MaxHealth - 3, p1.Health, "1 Day: it watched only the Day it was created");
        }

        [Test]
        public void WatchdogsArePrintedInTheirCardFormat() {
            var spell = game.Spell("Firewall", TargetType.Player, new CardEffect {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction> {
                    new EffectAction { actionType = ActionType.CreateWatchdog, targetType = TargetType.Enemy, statusDuration = 2,
                        watchdog = Spec(WatchdogEffect.Nullify, WatchedAction.Spell, charges: 1, atMost: 0) }
                }
            });
            StringAssert.Contains("<b>Watchdog</b>: enemy spells -> Nullify, 1 charge (On Watch, 2 Days).", CardTextFormatter.GetRulesText(spell));
        }
    }
}
