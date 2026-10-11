using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The order a turn resolves in: by kind of action, then by speed, with effects triggered along the way
    /// resolving in the same turn. Runs the real rules without the scene (see <see cref="TestGame"/>).
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
        public void ActionsResolveByKind_SummonsMovesPlaysCombatDamageDiscardsThenDraws() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            var walker = game.Summon(p1, game.Creature("Walker", 1, 3), 0);
            var played = game.GiveCard(p1, game.Creature("Played", 1, 1));
            var summoned = (ICreature)CardFactory.CreateCard(game.Creature("Summoned", 1, 1));
            game.History.Clear();

            // Queued in the opposite order
            game.ActionsQueue.AddAction(new DrawCardsAction(p1, 1));
            game.ActionsQueue.AddAction(new DiscardHandAction(p2));
            game.ActionsQueue.AddAction(new DamagePlayerAction(p2, 1));
            game.Declare(walker, p2.Battlefield[4]);
            game.ActionsQueue.AddAction(new PlayCardAction(played, p1, p1.Battlefield[3]));
            game.ActionsQueue.AddAction(new MoveCreatureAction(walker, p1.Battlefield[0], p1.Battlefield[1], p1));
            game.ActionsQueue.AddAction(new SummonCreatureAction(summoned, p2, p2.Battlefield[0]));
            game.Resolve();

            CollectionAssert.AreEqual(new[] {
                "SummonCreature", "MoveCreature",
                "PlayCard", "SummonCreature", // The play summons its creature right away
                "BattlefieldCombat",
                "DamagePlayer", "DamagePlayer", // The damage queued directly, then the attack's
                "DiscardHand", "DrawCards"
            }, game.History.ExecutedKinds());
        }

        [Test]
        public void WithinAKind_FasterCreaturesGoFirst_AndEqualSpeedsKeepTheOrderTheyWereDeclaredIn() {
            var slowA = game.Summon(p1, game.Creature("Slow A", 1, 3, speed: 1), 0);
            var fast = game.Summon(p1, game.Creature("Fast", 1, 3, speed: 3), 1);
            var mid = game.Summon(p1, game.Creature("Mid", 1, 3, speed: 2), 2);
            var slowB = game.Summon(p1, game.Creature("Slow B", 1, 3, speed: 1), 3);

            foreach (var attacker in new[] { slowA, fast, mid, slowB }) {
                game.Declare(attacker, p2.Battlefield[attacker.Slot.Index]);
            }
            game.Resolve();

            var expected = new[] { "Fast", "Mid", "Slow A", "Slow B" };
            CollectionAssert.AreEqual(expected, game.History.ExecutedOf<BattlefieldCombatAction>().Select(a => a.GetAttacker().Name));
            CollectionAssert.AreEqual(expected, game.History.ExecutedOf<DamagePlayerAction>().Select(a => a.GetAttacker().Name));
        }

        [Test]
        public void AFasterCreature_KillsASlowerOneBeforeItCanStrike() {
            var boar = game.Summon(p2, game.Creature("Boar", 4, 2, speed: 1), 0);
            var falcon = game.Summon(p1, game.Creature("Falcon", 4, 2, speed: 3), 0);

            // The slow one declares first: speed decides, not the order of declaring
            game.Declare(boar, p1.Battlefield[0]);
            game.Declare(falcon, p2.Battlefield[0]);
            game.Resolve();

            Assert.IsTrue(boar.IsDead);
            Assert.IsFalse(falcon.IsDead);
            CollectionAssert.IsEmpty(game.History.DamageTo(falcon), "The boar died before it could strike");
        }

        [Test]
        public void CreaturesOfEqualSpeed_StrikeEachOtherAtTheSameTime() {
            var knightA = game.Summon(p1, game.Creature("Knight A", 4, 2, speed: 2), 0);
            var knightB = game.Summon(p2, game.Creature("Knight B", 4, 2, speed: 2), 0);

            game.Declare(knightA, p2.Battlefield[0]);
            game.Declare(knightB, p1.Battlefield[0]);
            game.Resolve();

            Assert.IsTrue(knightA.IsDead, "Equal speeds hit at the same time, so both should die");
            Assert.IsTrue(knightB.IsDead, "Equal speeds hit at the same time, so both should die");
        }

        [Test]
        public void OnPlayBuffs_LandBeforeThisTurnsAttacks() => AssertOnPlayHelpLandsBeforeAttacks(ActionType.ModifyStat);

        [Test, Category(TestCategories.KnownBug), Description("#48: armor isn't in the priority list, so it lands after combat")]
        public void OnPlayArmor_LandsBeforeThisTurnsAttacks_LikeBuffs() => AssertOnPlayHelpLandsBeforeAttacks(ActionType.Armor);

        // A 2-health guard is attacked for 2 the same turn a creature that gives its friends +2 enters
        private void AssertOnPlayHelpLandsBeforeAttacks(ActionType help) {
            var guard = game.Summon(p1, game.Creature("Guard", 0, 2), 0);
            var brute = game.Summon(p2, game.Creature("Brute", 2, 3), 0);
            var helper = game.GiveCard(p1, game.Creature("Helper", 0, 3, 1, Effect(EffectTrigger.OnPlay, help, 2, TargetType.FriendlyCreatures)));

            game.Declare(brute, p1.Battlefield[0]);
            game.ActionsQueue.AddAction(new PlayCardAction(helper, p1, p1.Battlefield[1]));
            game.Resolve();

            Assert.IsFalse(guard.IsDead, $"The helper's +2 {help} should land before the brute's attack");
        }

        [Test]
        public void ADeclaredStatus_StopsAnAttackDeclaredTheSameTurn() {
            var brute = game.Summon(p1, game.Creature("Brute", 3, 3), 0);

            game.Declare(brute, p2.Battlefield[0]);
            game.ActionsQueue.AddAction(new ApplyStatusEffectAction(brute, StatusEffectType.Paralyzed, 1, 0));
            game.Resolve();

            Assert.AreEqual(Player.MaxHealth, p2.Health, "A creature paralyzed before its attack resolves doesn't attack");
        }

        [Test]
        public void RedeclaringAnAttack_ReplacesTheFirstOne() {
            var hound = game.Summon(p1, game.Creature("Hound", 2, 5), 0);
            var first = game.Summon(p2, game.Creature("First", 0, 5), 0);
            var second = game.Summon(p2, game.Creature("Second", 0, 5), 1);

            game.Declare(hound, p2.Battlefield[0]);
            game.Declare(hound, p2.Battlefield[1]);
            game.Resolve();

            CollectionAssert.IsEmpty(game.History.DamageTo(first));
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(second));
        }

        [Test]
        public void ACancelledAttack_DoesNotResolve() {
            var hound = game.Summon(p1, game.Creature("Hound", 2, 5), 0);

            game.Declare(hound, p2.Battlefield[0]);
            Assert.IsTrue(game.CombatHandler.CancelAttack(hound));
            game.Resolve();

            Assert.AreEqual(Player.MaxHealth, p2.Health);
            CollectionAssert.IsEmpty(game.History.ExecutedOf<BattlefieldCombatAction>());
        }

        [Test]
        public void EffectsTriggeredWhileResolving_ResolveInTheSameTurn() {
            game.Summon(p2, game.Creature("Bomb", 0, 1, 1, Effect(EffectTrigger.OnDeath, ActionType.Damage, 2, TargetType.EnemyCreatures)), 0);
            var hound = game.Summon(p1, game.Creature("Hound", 3, 5), 0);

            game.Attack(hound, p2.Battlefield[0]);

            Assert.AreEqual(0, game.ActionsQueue.GetPendingActionsCount(), "Something was left in the queue");
            Assert.AreEqual(3, hound.Health, "The bomb's death effect should have resolved");
        }
    }
}
