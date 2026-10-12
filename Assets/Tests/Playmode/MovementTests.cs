using NUnit.Framework;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Creatures walk, push, Leapfrog and Swap along their row as GDD 2.4 describes, and each side's moves resolve in an
    /// order that makes every planned move work. Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class MovementTests {
        private TestGame game;
        private Player p1;

        [SetUp]
        public void SetUp() {
            game = new TestGame();
            p1 = game.Player1;
        }

        [TearDown]
        public void TearDown() => game.Dispose();

        private ICreature Place(string name, int slot, MovementKeyword movement = MovementKeyword.None) {
            var data = game.Creature(name, 1, 5);
            data.movement = movement;
            return game.Summon(p1, data, slot);
        }

        private void Plan(ICreature creature, int toSlot) =>
            game.ActionsQueue.AddAction(new MoveCreatureAction(creature, creature.Slot, p1.Battlefield[toSlot], p1));

        private void AssertAt(ICreature creature, int slot, string message = null) =>
            Assert.AreSame(p1.Battlefield[slot], creature.Slot, message ?? $"{creature.Name} should be in slot {slot}");

        [Test]
        public void Walking_GoesAnyDistanceToAnEmptySlot_ButStopsShortOfACreatureInTheWay() {
            var walker = Place("Walker", 0);
            var wall = Place("Wall", 3);

            Plan(walker, 2);
            game.Resolve();
            AssertAt(walker, 2);

            Plan(walker, 4); // The wall in slot 3 doesn't move
            game.Resolve();
            AssertAt(walker, 2, "Blocked on the way, it stays at the furthest empty slot it reached");
            AssertAt(wall, 3);
        }

        [Test]
        public void Pushing_MovesTheCreatureOneSlotFurther_OnlyIntoAnEmptySlot_AndNeverInAChain() {
            var pusher = Place("Pusher", 0);
            var pushed = Place("Pushed", 1);
            game.Run(new ApplyStatusEffectAction(pushed, StatusEffectType.Exhausted, 1, 0));

            Plan(pusher, 1);
            game.Resolve();
            AssertAt(pusher, 1);
            AssertAt(pushed, 2, "Being pushed isn't its own movement: even a stunned creature gets pushed");

            var third = Place("Third", 3);
            Plan(pusher, 2); // Pushing 'pushed' would need slot 3, which 'third' holds
            game.Resolve();
            AssertAt(pusher, 1, "Pushes never chain");
            AssertAt(pushed, 2);
            AssertAt(third, 3);
        }

        [Test]
        public void Leapfrog_JumpsOverCreatures_ToAnEmptySlot() {
            var frog = Place("Frog", 0, MovementKeyword.Leapfrog);
            Place("Log A", 1);
            Place("Log B", 2);

            Plan(frog, 4);
            game.Resolve();
            AssertAt(frog, 4);
        }

        [Test]
        public void Swap_TradesPlacesWithAFriendlyCreatureAtAnyDistance_EvenOneThatCannotAct() {
            var swapper = Place("Swapper", 0, MovementKeyword.Swap);
            Place("Between", 2);
            var partner = Place("Partner", 4);
            game.Run(new ApplyStatusEffectAction(partner, StatusEffectType.Exhausted, 1, 0));

            Plan(swapper, 4);
            game.Resolve();
            AssertAt(swapper, 4);
            AssertAt(partner, 0, "Being swapped isn't the partner's own movement");
        }

        [Test]
        public void TheGameMovesFirstWhoeverNeedsToMakeRoom_WhateverOrderTheMovesWerePlanned() {
            var a = Place("A", 1);
            var b = Place("B", 0);

            Plan(b, 2); // A is in the way...
            Plan(a, 4); // ...but planned to leave
            game.Resolve();

            AssertAt(a, 4);
            AssertAt(b, 2, "A moved first to let B through");
        }

        [Test]
        public void ThePreview_ShowsTheFinalFormation_WithoutMovingAnything() {
            var a = Place("A", 1);
            var b = Place("B", 0);
            Plan(b, 2);
            Plan(a, 4);

            var preview = MovementRules.Preview(p1.Battlefield, MovementRules.PlannedMovesOf(p1, game.ActionsQueue));

            Assert.AreSame(p1.Battlefield[4], preview.FinalSlots[a]);
            Assert.AreSame(p1.Battlefield[2], preview.FinalSlots[b]);
            AssertAt(a, 1, "Previewing moves nothing");
            CollectionAssert.IsEmpty(preview.Blocked);
        }

        [Test]
        public void TwoCreaturesThatWouldHaveToPassEachOther_IsAnImpossiblePlan() {
            var a = Place("A", 0);
            var b = Place("B", 2);
            Plan(b, 1);

            var (kind, blocked, hint) = MovementRules.Evaluate(a, p1.Battlefield[4], game.ActionsQueue);

            Assert.AreEqual(MoveKind.Walk, kind);
            Assert.IsTrue(blocked, "A can't walk past B, who walks the other way");
            Assert.AreEqual("needs Leapfrog", hint);
        }
    }
}
