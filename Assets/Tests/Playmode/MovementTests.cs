using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Creatures walk, trade places, Leapfrog and Swap in their formation as GDD 2.4 describes (the shape is
    /// <see cref="Formation"/>: 0 the Vanguard, 1 and 2 the Flanks, 3 and 4 Headquarters; a step goes Vanguard-Flank,
    /// Flank-Flank, Flank-the Headquarters slot behind it, Headquarters-Headquarters), and each side's moves resolve in an
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

        // An empty board for the next part of a test
        private void Fresh() {
            game.Dispose();
            SetUp();
        }

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
        public void Moves_Walk_SwapInMelee_Leapfrog_Swap_WhileDeploying_ButNotRooted_AndManeuvers() {
            var walker = Place("Walker", 3);
            var wall = Place("Wall", 1);
            Plan(walker, 4);
            game.Resolve();
            AssertAt(walker, 4, "Headquarters to Headquarters: the back row isn't blocked by the Flank ahead");
            Plan(walker, 0);
            game.Resolve();
            AssertAt(walker, 0, "Walking goes any distance along empty slots (round by the right Flank)");
            var other = Place("Other", 2);
            Plan(walker, 3); // Both Flanks taken: no way back
            game.Resolve();
            AssertAt(walker, 0, "Hemmed in, it stays put");
            AssertAt(wall, 1);
            AssertAt(other, 2);

            Fresh();
            var mover = Place("Mover", 0);
            var partner = Place("Partner", 1);
            game.Run(new ApplyStatusEffectAction(partner, StatusEffectType.Exhausted, 1, 0));
            Plan(mover, 1);
            game.Resolve();
            AssertAt(mover, 1);
            AssertAt(partner, 0, "A step away (melee range), the two swap; being moved isn't its own movement: even a stunned creature is");
            var far = Place("Far", 4);
            Plan(mover, 4); // Not a step away: Push is archived (the user, 2026-10-16)
            game.Resolve();
            AssertAt(mover, 2, "Further away, there's no swap without the Swap keyword: blocked, it gets as close as it can");
            AssertAt(far, 4, "...and the other one isn't moved");

            // Still deploying, a creature can take its place; Rooted (a building going up), it can't move nor be moved
            Fresh();
            var builderData = game.Creature("Builder", 1, 5);
            builderData.turnsUntilOperative = 1;
            var builder = game.Summon(p1, builderData, 0);
            Assert.IsFalse(builder.IsOperative);
            Plan(builder, 1);
            game.Resolve();
            AssertAt(builder, 1, "Deploying, it can move (the user, 2026-10-16)");
            game.Run(new ApplyStatusEffectAction(builder, StatusEffectType.Rooted, 2, 0));
            Plan(builder, 0);
            game.Resolve();
            AssertAt(builder, 1, "Rooted: it can't move...");
            var neighbour = Place("Neighbour", 0);
            Plan(neighbour, 1);
            game.Resolve();
            AssertAt(builder, 1, "...nor be swapped");
            AssertAt(neighbour, 0);

            Fresh();
            var frog = Place("Frog", 0, MovementKeyword.Leapfrog);
            Place("Log A", 1);
            Place("Log B", 2);
            Plan(frog, 4);
            game.Resolve();
            AssertAt(frog, 4, "Leapfrog jumps over creatures to an empty slot");

            var swapper = Place("Swapper", 0, MovementKeyword.Swap);
            game.Run(new ApplyStatusEffectAction(frog, StatusEffectType.Exhausted, 1, 0));
            Plan(swapper, 4);
            game.Resolve();
            AssertAt(swapper, 4, "Swap trades places with a friendly creature at any distance, over the ones between");
            AssertAt(frog, 0, "Being swapped isn't the partner's own movement: it works even if it can't act");

            // Maneuvers (the user, 2026-10-17): Push a step back, Pull a step forward, trading places with whoever is there;
            // Shuffle mixes a whole side. A Rooted creature stays put
            Fresh();
            var front = Place("Front", 0);
            var flank = Place("Flank", 1);
            game.Run(new PushAction(front, forward: false));
            AssertAt(front, 2, "Pushed back, the Vanguard goes to the empty Flank");
            game.Run(new PushAction(flank, forward: false));
            AssertAt(flank, 3, "A Flank goes to the Headquarters slot behind it");
            game.Run(new PushAction(flank, forward: false));
            AssertAt(flank, 3, "Headquarters can't go further back");
            game.Run(new PushAction(flank, forward: true));
            AssertAt(flank, 1, "Pulled forward: Headquarters to the Flank in front of it");
            var back = Place("Back", 4);
            game.Run(new PushAction(back, forward: true));
            AssertAt(back, 2, "Into an occupied slot...");
            AssertAt(front, 4, "...the two trade places");
            game.Run(new ApplyStatusEffectAction(back, StatusEffectType.Rooted, 2, 0));
            game.Run(new PushAction(back, forward: true));
            AssertAt(back, 2, "Rooted: not moved");
            game.Run(new ShuffleAction(p1));
            AssertAt(back, 2, "A shuffle leaves a Rooted creature where it is");
            Assert.IsTrue(new[] { front, flank }.All(c => c.Slot?.Owner == p1 && c.Slot.OccupyingCreature == c), "...and every other one lands in a slot of its side");
            Assert.AreEqual(3, p1.Battlefield.Count(s => s.IsOccupied()), "Nobody lost or doubled");
        }

        [Test]
        public void Planning_MovesWhoeverMakesRoomFirst_PreviewsTheFormation_AndSpotsImpossiblePlans() {
            var a = Place("A", 1);
            var b = Place("B", 3);
            Place("Post", 4); // The other way round is taken
            Plan(b, 0); // A is in the way...
            Plan(a, 2); // ...but planned to leave
            var preview = MovementRules.Preview(p1.Battlefield, MovementRules.PlannedMovesOf(p1, game.ActionsQueue));
            Assert.AreSame(p1.Battlefield[2], preview.FinalSlots[a], "The preview shows the final formation");
            Assert.AreSame(p1.Battlefield[0], preview.FinalSlots[b]);
            CollectionAssert.IsEmpty(preview.Blocked);
            AssertAt(a, 1, "Previewing moves nothing");
            game.Resolve();
            AssertAt(a, 2);
            AssertAt(b, 0, "A moved first to let B through, whatever order they were planned in");

            Fresh();
            var hemmed = Place("Hemmed", 0);
            Place("Left Flank", 1);
            Place("Right Flank", 2);
            var (kind, blocked, hint) = MovementRules.Evaluate(hemmed, p1.Battlefield[3], game.ActionsQueue);
            Assert.AreEqual(MoveKind.Walk, kind);
            Assert.IsTrue(blocked, "Both Flanks taken: the Vanguard can't walk back to Headquarters");
            Assert.AreEqual("needs Leapfrog", hint);

            // A slot its creature is leaving is walked into, not swapped (the user): both end where they were sent
            Fresh();
            var leaver = Place("Leaver", 0);
            var follower = Place("Follower", 1);
            Plan(leaver, 2);
            Assert.AreEqual(MoveKind.Walk, MovementRules.Evaluate(follower, p1.Battlefield[0], game.ActionsQueue).kind, "Its creature is leaving: a walk, not a swap");
            game.ActionsQueue.AddAction(new MoveCreatureAction(follower, p1.Battlefield[1], p1.Battlefield[0], p1, MovementRules.KindOf(follower, p1.Battlefield[0], game.ActionsQueue)));
            game.Resolve();
            AssertAt(leaver, 2);
            AssertAt(follower, 0, "...each where it was sent");
        }
    }
}
