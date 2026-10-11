using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The board: where creatures land, how they move and swap, and what attacks, spells and effects hit.
    /// Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class BattlefieldTests {
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
        public void TearDown() {
            GameAsserts.AssertBoardsConsistent(p1, p2);
            game.Dispose();
        }

        #region Placing and moving
        [Test]
        public void APlayedCreature_LandsInTheSlotItWasPlayedOn() {
            var wolf = game.GiveCard(p1, game.Creature("Wolf", 2, 3));

            game.Run(new PlayCardAction(wolf, p1, p1.Battlefield[3]));

            Assert.AreSame(wolf, p1.Battlefield[3].OccupyingCreature);
            Assert.AreSame(p1, ((ICreature)wolf).Owner);
            CollectionAssert.DoesNotContain(p1.Hand, wolf);
        }

        [Test]
        public void MovingToAnEmptySlot_RelocatesTheCreature() {
            var eel = game.Summon(p1, game.Creature("Eel", 1, 3), 0);

            game.Run(new MoveCreatureAction(eel, p1.Battlefield[0], p1.Battlefield[4], p1));

            Assert.IsFalse(p1.Battlefield[0].IsOccupied());
            Assert.AreSame(p1.Battlefield[4], eel.Slot);
        }

        [Test]
        public void MovingOntoAnotherCreature_SwapsThem() {
            var eel = game.Summon(p1, game.Creature("Eel", 1, 3), 0);
            var turtle = game.Summon(p1, game.Creature("Turtle", 1, 3), 1);

            game.Run(new MoveCreatureAction(eel, p1.Battlefield[0], p1.Battlefield[1], p1));

            Assert.AreSame(turtle, p1.Battlefield[0].OccupyingCreature);
            Assert.AreSame(eel, p1.Battlefield[1].OccupyingCreature);
        }

        [Test]
        public void CreaturesThatCantAct_StayPut_ButCanStillBeSwappedWith() {
            var frozen = game.Summon(p1, game.Creature("Paralyzed", 1, 3), 0);
            var sleeperData = game.Creature("Sleeper", 1, 3);
            sleeperData.turnsUntilOperative = 1;
            var sleeper = game.Summon(p1, sleeperData, 1);
            var mover = game.Summon(p1, game.Creature("Mover", 1, 3), 2);
            game.Run(new ApplyStatusEffectAction(frozen, StatusEffectType.Paralyzed, 1, 0));

            game.Run(new MoveCreatureAction(frozen, p1.Battlefield[0], p1.Battlefield[3], p1));
            game.Run(new MoveCreatureAction(sleeper, p1.Battlefield[1], p1.Battlefield[4], p1));
            Assert.AreSame(frozen, p1.Battlefield[0].OccupyingCreature, "A paralyzed creature can't move");
            Assert.AreSame(sleeper, p1.Battlefield[1].OccupyingCreature, "A creature that isn't operative can't move");

            game.Run(new MoveCreatureAction(mover, p1.Battlefield[2], p1.Battlefield[0], p1));
            Assert.AreSame(mover, p1.Battlefield[0].OccupyingCreature, "...but another creature can swap with it");
            Assert.AreSame(frozen, p1.Battlefield[2].OccupyingCreature);
        }

        [Test]
        public void SummoningOntoACreature_ReplacesIt_WhichIsDiscardedWithoutDying() {
            var martyr = game.Summon(p1, game.Creature("Martyr", 1, 3, 1, Effect(EffectTrigger.OnDeath, ActionType.Damage, 3, TargetType.Enemy)), 2);
            var newcomer = game.GiveCard(p1, game.Creature("Newcomer", 2, 2));

            game.Run(new PlayCardAction(newcomer, p1, p1.Battlefield[2]));

            Assert.AreSame(newcomer, p1.Battlefield[2].OccupyingCreature);
            Assert.IsNull(martyr.Slot);
            Assert.AreEqual(1, p1.Deck.GetDiscardPilePreview().Count(c => c.Name == "Martyr"));
            Assert.AreEqual(Player.MaxHealth, p2.Health, "A replaced creature doesn't die, so its death effect doesn't fire");
        }

        [Test]
        public void ASecondMoveForTheSameCreature_NeverMovesAnotherCreature() {
            var walker = game.Summon(p1, game.Creature("Walker", 1, 3), 0);
            var bystander = game.Summon(p1, game.Creature("Bystander", 1, 3), 1);

            // The board lets a creature be dragged twice: its slot only changes once the moves resolve
            game.ActionsQueue.AddAction(new MoveCreatureAction(walker, p1.Battlefield[0], p1.Battlefield[1], p1));
            game.ActionsQueue.AddAction(new MoveCreatureAction(walker, p1.Battlefield[0], p1.Battlefield[2], p1));
            game.Resolve();

            Assert.AreNotEqual(2, bystander.Slot?.Index, "The walker's second move carried the bystander off to slot 2");
            Assert.AreEqual(2, walker.Slot.Index, "The second move replaces the first");
            Assert.AreEqual(1, bystander.Slot.Index);
        }

        [Test]
        public void AMove_StartsFromWhereTheCreatureIsWhenItResolves() {
            var walker = game.Summon(p1, game.Creature("Walker", 1, 3), 0);
            var pusher = game.Summon(p1, game.Creature("Pusher", 1, 3), 1);

            // The pusher's move resolves first and swaps it into the walker's slot, so the walker's move starts from slot 1
            game.ActionsQueue.AddAction(new MoveCreatureAction(pusher, p1.Battlefield[1], p1.Battlefield[0], p1));
            game.ActionsQueue.AddAction(new MoveCreatureAction(walker, p1.Battlefield[0], p1.Battlefield[2], p1));
            game.Resolve();

            Assert.AreEqual(0, pusher.Slot.Index, "The pusher shouldn't be carried along by the walker's move");
            Assert.AreEqual(2, walker.Slot.Index);
        }

        [Test]
        public void ACreature_CanMoveAndAttackInTheSameTurn() {
            var walker = game.Summon(p1, game.Creature("Walker", 2, 3), 0);

            game.ActionsQueue.AddAction(new MoveCreatureAction(walker, p1.Battlefield[0], p1.Battlefield[1], p1));
            game.Declare(walker, p2.Battlefield[3]);
            game.Resolve();

            Assert.AreEqual(1, walker.Slot.Index);
            Assert.AreEqual(Player.MaxHealth - 2, p2.Health);
        }
        #endregion

        #region What gets hit
        [Test]
        public void AnAttack_HitsTheCreatureInTheSlot_OrThePlayerWhenItIsEmpty() {
            var hound = game.Summon(p1, game.Creature("Hound", 3, 5), 0);
            var ox = game.Summon(p2, game.Creature("Ox", 0, 10), 0);

            game.Attack(hound, p2.Battlefield[0]);
            Assert.AreEqual(7, ox.Health);
            Assert.AreEqual(Player.MaxHealth, p2.Health);

            game.Attack(hound, p2.Battlefield[1]);
            Assert.AreEqual(Player.MaxHealth - 3, p2.Health);
            Assert.AreEqual(7, ox.Health);
        }

        [Test]
        public void ASpell_HitsTheCreatureItIsDroppedOn_OrThatSidesPlayerOnAnEmptySlot() {
            var bolt = game.Spell("Bolt", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures));
            var oxA = game.Summon(p2, game.Creature("Ox A", 0, 10), 0);
            var oxB = game.Summon(p2, game.Creature("Ox B", 0, 10), 1);

            game.Run(new PlayCardAction(game.GiveCard(p1, bolt), p1, p2.Battlefield[1]));
            Assert.AreEqual(10, oxA.Health, "Only the creature it was dropped on is hit");
            Assert.AreEqual(7, oxB.Health);

            game.Run(new PlayCardAction(game.GiveCard(p1, bolt), p1, p2.Battlefield[3]));
            Assert.AreEqual(Player.MaxHealth - 3, p2.Health);
        }

        [Test]
        public void GroupEffects_HitEveryCreatureOfTheirGroup_Once() {
            var mine = game.Summon(p1, game.Creature("Mine", 0, 5), 0);
            var theirsA = game.Summon(p2, game.Creature("Theirs A", 0, 5), 0);
            var theirsB = game.Summon(p2, game.Creature("Theirs B", 0, 5), 3);

            var storm = game.Summon(p1, game.Creature("Storm", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.EnemyCreatures)), 1);
            Assert.AreEqual(4, theirsA.Health);
            Assert.AreEqual(4, theirsB.Health);
            Assert.AreEqual(5, mine.Health, "Enemy creatures only");

            var quake = game.Summon(p1, game.Creature("Quake", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.AllCreatures)), 2);
            foreach (var creature in new[] { mine, storm, quake }) Assert.AreEqual(4, creature.Health, $"{creature.Name}: all creatures, including itself");
            foreach (var creature in new[] { theirsA, theirsB }) Assert.AreEqual(3, creature.Health, $"{creature.Name}: all creatures");

            game.Summon(p1, game.Creature("Medic", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Heal, 1, TargetType.FriendlyCreatures)), 3);
            foreach (var creature in new[] { mine, storm, quake }) Assert.AreEqual(5, creature.Health, $"{creature.Name}: friendly creatures");
            foreach (var creature in new[] { theirsA, theirsB }) Assert.AreEqual(3, creature.Health, $"{creature.Name}: friendly creatures only");

            foreach (var creature in new[] { theirsA, theirsB }) {
                CollectionAssert.AreEqual(new[] { 1, 1 }, game.History.DamageTo(creature), $"{creature.Name} was hit the wrong number of times");
            }
        }

        [Test]
        public void ARandomTarget_IsExactlyOneTarget() {
            var enemies = Enumerable.Range(0, 3).Select(i => game.Summon(p2, game.Creature($"Enemy {i}", 0, 5), i)).ToList();

            game.Summon(p1, game.Creature("Sniper", 1, 3, 1,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.EnemyCreatures, TargetModifier.Random)), 0);

            Assert.AreEqual(1, enemies.Count(e => e.Health < e.MaxHealth), "Exactly one enemy should be hit");
            Assert.AreEqual(1, game.History.CreatureDamage.Count);
        }

        [Test]
        public void ASpreadingAttack_AlsoHitsTheTargetsNeighbours() {
            var cleaver = game.Summon(p1, game.Creature("Cleaver", 2, 5, 1,
                Effect(EffectTrigger.OnDamage, ActionType.Damage, 0, TargetType.EnemyCreatures, TargetModifier.WithAdjacent)), 0);
            var enemies = Enumerable.Range(0, 4).Select(i => game.Summon(p2, game.Creature($"Enemy {i}", 0, 5), i)).ToList();

            game.Attack(cleaver, p2.Battlefield[1]);

            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(enemies[1]), "The target");
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(enemies[0]), "Its left neighbour");
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(enemies[2]), "Its right neighbour");
            CollectionAssert.IsEmpty(game.History.DamageTo(enemies[3]), "Not a neighbour");
        }

        [Test]
        public void AChainAttack_FollowsTheLongerRow_AndStopsAtAGap() {
            var eel = game.Summon(p1, game.Creature("Eel", 3, 4, 1,
                Effect(EffectTrigger.OnDamage, ActionType.Damage, 4, TargetType.EnemyCreatures, TargetModifier.Chained)), 0);
            var left = game.Summon(p2, game.Creature("Left", 0, 10), 1);
            var target = game.Summon(p2, game.Creature("Target", 0, 10), 2);
            var right = game.Summon(p2, game.Creature("Right", 0, 10), 3);
            var farRight = game.Summon(p2, game.Creature("Far Right", 0, 10), 4);

            game.Attack(eel, p2.Battlefield[2]);

            CollectionAssert.AreEqual(new[] { 3 }, game.History.DamageTo(target));
            CollectionAssert.AreEqual(new[] { 4 }, game.History.DamageTo(right));
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(farRight));
            CollectionAssert.IsEmpty(game.History.DamageTo(left), "The row to the right is longer, so the chain goes right");
        }
        #endregion

        [Test]
        public void TheBoardAndItsCreatures_AgreeOnPositions_AfterAnyMix_OfMovesSwapsAndReplacements() {
            var creatures = Enumerable.Range(0, 4).Select(i => game.Summon(p1, game.Creature($"C{i}", 1, 3), i)).ToList();

            game.ActionsQueue.AddAction(new MoveCreatureAction(creatures[0], p1.Battlefield[0], p1.Battlefield[4], p1));
            game.ActionsQueue.AddAction(new MoveCreatureAction(creatures[1], p1.Battlefield[1], p1.Battlefield[2], p1));
            game.ActionsQueue.AddAction(new MoveCreatureAction(creatures[3], p1.Battlefield[3], p1.Battlefield[0], p1));
            game.Resolve();
            GameAsserts.AssertBoardsConsistent(p1, p2);

            game.Summon(p1, game.Creature("Replacement", 1, 3), 2);
            game.Run(new DamageCreatureAction(p1.Battlefield[4].OccupyingCreature, 10));
            GameAsserts.AssertBoardsConsistent(p1, p2);
            Assert.AreEqual(3, p1.Battlefield.Count(s => s.IsOccupied()));
        }
    }
}
