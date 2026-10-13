using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Watchdogs (GDD 3.11): created through the queue, they nullify, redirect, reorder or reveal matching actions, On Watch
    /// (just before each action) or at the Briefing (once a Day, from the Day after they're created). Each changed action uses
    /// a charge; when several match, the oldest triggers. Runs the real rules without the scene (see <see cref="TestGame"/>).
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

        // An empty board for the next part of a test
        private void Fresh() {
            game.Dispose();
            SetUp();
        }

        private static WatchdogSpec Spec(WatchdogEffect effect, WatchedAction watches = WatchedAction.Attack, WatchdogSide side = WatchdogSide.Enemy,
            int charges = 1, WatchdogTiming timing = WatchdogTiming.OnWatch, BriefingPick pick = BriefingPick.First, int atMost = 0) =>
            new WatchdogSpec { effect = effect, watches = watches, side = side, charges = charges, timing = timing, pick = pick, atMost = atMost };

        private void Watch(WatchdogSpec spec, IPlayer controller, ICreature creature = null, int days = 0) =>
            game.Run(new CreateWatchdogAction(spec, controller, creature, days));

        [Test]
        public void OnWatch_Nullify_Conditions_AndTheOldestMatchTriggers() {
            var mine = game.Summon(p1, game.Creature("Mine", 4, 10), 0);
            var brute = game.Summon(p2, game.Creature("Brute", 3, 10), 0);
            Watch(Spec(WatchdogEffect.Nullify), p1);
            game.Attack(brute, p1.Battlefield[1]); // An empty slot: it would hit player 1
            Assert.AreEqual(Player.MaxHealth, p1.Health, "Nullified");
            Assert.IsEmpty(game.ActionsQueue.Watchdogs, "Its only charge is used: it's gone");
            game.Attack(brute, p1.Battlefield[1]);
            Assert.AreEqual(Player.MaxHealth - 3, p1.Health);

            Watch(Spec(WatchdogEffect.Nullify, atMost: 2), p1);
            game.Attack(brute, p1.Battlefield[1]);
            Assert.AreEqual(Player.MaxHealth - 6, p1.Health, "Attack 3 isn't 2 or less");
            Watch(Spec(WatchdogEffect.Nullify), p1);
            game.Attack(mine, p2.Battlefield[1]);
            Assert.AreEqual(Player.MaxHealth - 4, p2.Health, "It watches enemy attacks, not friendly ones");
            Watch(Spec(WatchdogEffect.Reveal), p1);
            game.Attack(brute, p1.Battlefield[1]);
            Assert.AreEqual(Player.MaxHealth - 6, p1.Health, "The older Nullify triggered");
            Assert.AreEqual(2, game.ActionsQueue.Watchdogs.Count, "The newer one kept its charge (with the strength one)");

            Fresh();
            var late = game.Summon(p2, game.Creature("Late", 3, 10), 0);
            var firewallData = game.Spell("Firewall", TargetType.Player, new CardEffect {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction> {
                    new EffectAction { actionType = ActionType.CreateWatchdog, targetType = TargetType.Enemy, statusDuration = 2,
                        watchdog = Spec(WatchdogEffect.Nullify, WatchedAction.Attack) }
                }
            });
            StringAssert.Contains("Watchdog: enemy attacks -> Nullify, 1 charge (Monitored 2)", Keywords.Plain(CardTextFormatter.GetRulesText(firewallData)));
            var firewall = game.GiveCard(p1, firewallData);
            game.Declare(late, p1.Battlefield[0]); // Planned first
            game.Play(firewall, p1, null);
            game.Resolve(); // The spell layer comes before attacks
            Assert.AreEqual(Player.MaxHealth, p1.Health, "A Watchdog made by a spell catches attacks queued before it");

            // Intelligence (Glomar Response): an enemy reveal is nullified, even an instant one, and they learn nothing
            Fresh();
            var hidden = game.Summon(p2, game.Creature("Hidden", 1, 3), 0);
            Watch(Spec(WatchdogEffect.Nullify, WatchedAction.Intelligence), p2);
            var tapData = game.Spell("Tap", TargetType.EnemyCreatures, new CardEffect {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction> {
                    new EffectAction { actionType = ActionType.ApplyStatus, targetType = TargetType.EnemyCreatures, statusEffectToApply = StatusEffectType.Revealed, statusDuration = 2 }
                }
            });
            var tap = game.GiveCard(p1, tapData);
            game.Play(tap, p1, p2.Battlefield[0]); // Instant: it resolves at once
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(hidden, StatusEffectType.Revealed), "Neither confirmed nor denied");
            CollectionAssert.DoesNotContain(p1.Hand, tap, "The spell is spent");
            game.Play(game.GiveCard(p1, tapData), p1, p2.Battlefield[0]);
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(hidden, StatusEffectType.Revealed), "Its one charge is gone");
        }

        [Test]
        public void OnWatch_RedirectAndReorder() {
            var aimedAt = game.Summon(p1, game.Creature("Aimed At", 0, 10), 0);
            var decoy = game.Summon(p1, game.Creature("Decoy", 0, 10), 3);
            var brute = game.Summon(p2, game.Creature("Brute", 3, 10), 0);
            Watch(Spec(WatchdogEffect.Redirect), p1, creature: brute); // "attacks by Creature C"
            game.Attack(brute, p1.Battlefield[0]);
            Assert.AreEqual(10, aimedAt.Health);
            Assert.AreEqual(7, decoy.Health, "To a random creature: the only one that's neither the attacker nor its target");

            var hot = game.Summon(p2, game.Creature("Hot Ally", 0, 10), 4);
            game.Run(new ApplyStatusEffectAction(hot, StatusEffectType.HeatSignature, 1, 2));
            Watch(Spec(WatchdogEffect.Redirect), p1);
            game.Attack(brute, p1.Battlefield[0]);
            Assert.AreEqual(10, aimedAt.Health);
            Assert.AreEqual(7, hot.Health, "Heat draws it, even onto the attacker's own side");

            Fresh();
            var slow = game.Summon(p1, game.Creature("Slow", 5, 3), 0);
            var fast = game.Summon(p2, game.Creature("Fast", 5, 3, speed: 2), 0);
            Watch(Spec(WatchdogEffect.Reorder), p1);
            game.Declare(slow, p2.Battlefield[0]);
            game.Declare(fast, p1.Battlefield[0]);
            game.Resolve();
            Assert.IsTrue(fast.IsDead, "Reorder moved the faster creature's attack after the slower one's");
            Assert.AreEqual(3, slow.Health);
        }

        [Test]
        public void Briefing_StartsTheDayAfter_PicksEveryOrTheStrongest_OneChargePerAction_AndDaysRunOut() {
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

            Watch(Spec(WatchdogEffect.Nullify, timing: WatchdogTiming.Briefing, pick: BriefingPick.Strongest), p1);
            Watch(Spec(WatchdogEffect.Nullify, charges: 5), p2, days: 1);
            game.StartTurn();
            Assert.AreEqual(1, game.ActionsQueue.Watchdogs.Count, "1 Day: the other one watched only the Day it was created");
            game.Declare(a, p1.Battlefield[0]);
            game.Declare(c, p1.Battlefield[2]);
            game.ActionsQueue.BriefWatchdogs();
            game.Resolve();
            Assert.AreEqual(Player.MaxHealth - 2 - 4 - 2, p1.Health, "It picked the strongest: only A's attack lands");

            // Backfire (Sabotage): the strongest attack doesn't happen, and its attacker takes its own Attack instead
            Watch(Spec(WatchdogEffect.Backfire, timing: WatchdogTiming.Briefing, pick: BriefingPick.Strongest), p1);
            game.StartTurn();
            int health = p1.Health;
            game.Declare(a, p1.Battlefield[0]);
            game.Declare(c, p1.Battlefield[2]);
            game.ActionsQueue.BriefWatchdogs();
            game.Resolve();
            Assert.AreEqual(health - 2, p1.Health, "Only A's attack lands...");
            Assert.AreEqual(10 - 4, c.Health, "...C's backfires on C");
        }
    }
}
