using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Slot Effects (GDD 2.4, 'Slot Effects Compendium'): one per slot, they stay with the slot, triggered ones resolve before
    /// anything else (environment actions, GDD 3.7), and "1 Trigger" ones are used up. Runs the real rules without the scene.
    /// </summary>
    public class SlotEffectTests {
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

        private void Place(BoardSlot slot, SlotEffectType type, int value = 0, int days = 0, IPlayer controller = null) =>
            game.Run(new PlaceSlotEffectAction(slot, type, value, days, controller ?? slot.Owner.Opponent));

        private static CardEffect PlaceEffect(SlotEffectType type, int value, int days = 0) => new CardEffect {
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction { actionType = ActionType.PlaceSlotEffect, slotEffect = type, value = value, statusDuration = days, targetType = TargetType.AllCreatures }
            }
        };

        [Test]
        public void Traps_LandmineOnEntering_BarbedSnareOnMovingOut_GoOffBeforeAnythingElse() {
            Place(p2.Battlefield[1], SlotEffectType.Landmine, 2);
            Assert.IsTrue(p2.Battlefield[1].Effect.IsHidden);
            var summoned = game.Summon(p2, game.Creature("Summoned", 1, 5), 1);
            Assert.AreEqual(3, summoned.Health, "A Landmine hits the first creature summoned onto it...");
            Assert.IsNull(p2.Battlefield[1].Effect, "1 Trigger: used up");
            Place(p2.Battlefield[3], SlotEffectType.Landmine, 2);
            var walker = game.Summon(p2, game.Creature("Walker", 1, 5), 4);
            game.Run(new MoveCreatureAction(walker, p2.Battlefield[4], p2.Battlefield[3], p2));
            Assert.AreEqual(3, walker.Health, "...or moved onto it");

            var doomedWalker = game.Summon(p1, game.Creature("Doomed Walker", 1, 2), 0);
            var played = game.GiveCard(p1, game.Creature("Played", 1, 1));
            Place(p1.Battlefield[1], SlotEffectType.Landmine, 2);
            game.History.Clear();
            game.ActionsQueue.AddAction(new PlayCardAction(played, p1, p1.Battlefield[4]));
            game.ActionsQueue.AddAction(new MoveCreatureAction(doomedWalker, p1.Battlefield[0], p1.Battlefield[1], p1));
            game.Resolve();
            var kinds = game.History.ExecutedKinds();
            Assert.AreEqual("DamageCreature", kinds[1], "Stepping on it hits right away, after the move");
            Assert.Less(kinds.IndexOf("DestroyCreature"), kinds.IndexOf("PlayCard"), "...and it dies before the Summon layer");

            Fresh();
            var pusher = game.Summon(p1, game.Creature("Pusher", 1, 5), 0);
            var snared = game.Summon(p1, game.Creature("Snared", 1, 5), 1);
            var doomed = game.Summon(p1, game.Creature("Doomed", 0, 2), 3);
            Place(p1.Battlefield[1], SlotEffectType.BarbedSnare, 2);
            Place(p1.Battlefield[3], SlotEffectType.BarbedSnare, 2);
            game.Run(new MoveCreatureAction(pusher, p1.Battlefield[0], p1.Battlefield[1], p1)); // Pushes Snared to slot 2
            Assert.AreEqual(3, snared.Health, "A Barbed Snare hurts the creature that leaves, even one pushed out");
            Assert.AreEqual(5, pusher.Health, "Entering isn't leaving");
            Assert.IsNull(p1.Battlefield[1].Effect);

            game.Run(new DamageCreatureAction(doomed, 5));
            Assert.IsNotNull(p1.Battlefield[3].Effect, "Dying isn't leaving");
            var next = game.Summon(p1, game.Creature("Next", 0, 5), 3);
            Assert.AreEqual(5, next.Health, "Taking the slot isn't leaving either");
            game.Run(new MoveCreatureAction(next, p1.Battlefield[3], p1.Battlefield[4], p1));
            Assert.AreEqual(3, next.Health);

            var target = game.Summon(p2, game.Creature("Target", 0, 5), 0);
            var mover = game.Summon(p2, game.Creature("Mover", 1, 5), 1);
            game.ActionsQueue.AddAction(new MoveCreatureAction(mover, p2.Battlefield[1], p2.Battlefield[2], p2));
            using (game.ActionsQueue.QueueingEnvironment()) {
                game.ActionsQueue.AddAction(new DamageCreatureAction(target, 1));
            }
            game.History.Clear();
            game.Resolve();
            Assert.AreEqual("DamageCreature", game.History.ExecutedKinds()[0], "Environment actions go first, whatever their layer");
        }

        [Test]
        public void Placing_OnePerSlot_StaysWithTheSlot_Cleansable_AndTemporalShieldStopsCombatDamage() {
            var mover = game.Summon(p1, game.Creature("Mover", 1, 10), 0);
            var shield = game.GiveCard(p1, game.Spell("Shield Slot", TargetType.AllCreatures, PlaceEffect(SlotEffectType.TemporalShield, 0, 2)));
            game.Play(shield, p1, p1.Battlefield[0]); // Aimed at its own creature's slot
            game.ActionsQueue.AddAction(new MoveCreatureAction(mover, p1.Battlefield[0], p1.Battlefield[2], p1));
            game.Resolve();
            Assert.AreSame(p1.Battlefield[2], mover.Slot, "Moves resolve before spells");
            Assert.AreSame(p1, p1.Battlefield[0].Effect?.Controller, "Static: it lands on the slot it was aimed at");
            Assert.IsNull(p1.Battlefield[2].Effect);

            var shielded = game.Summon(p1, game.Creature("Shielded", 0, 10), 0);
            var brute = game.Summon(p2, game.Creature("Brute", 3, 10), 0);
            game.Attack(brute, p1.Battlefield[0]);
            Assert.AreEqual(10, shielded.Health, "A Temporal Shield stops combat damage");
            game.Run(new DamageCreatureAction(shielded, 2));
            Assert.AreEqual(8, shielded.Health, "...not other damage");
            game.StartTurn();
            Assert.IsNotNull(p1.Battlefield[0].Effect, "2 Days: this one and the next");
            game.StartTurn();
            Assert.IsNull(p1.Battlefield[0].Effect);
            game.Attack(brute, p1.Battlefield[0]);
            Assert.AreEqual(5, shielded.Health);

            Place(p2.Battlefield[3], SlotEffectType.Landmine, 2);
            Place(p2.Battlefield[3], SlotEffectType.ScorchedEarth);
            Assert.AreEqual(SlotEffectType.ScorchedEarth, p2.Battlefield[3].Effect.Type, "One per slot: the new one replaces the old");
            Place(p2.Battlefield[4], SlotEffectType.Landmine, 2);
            game.Run(new CleanseSlotAction(p2.Battlefield[4]));
            var safe = game.Summon(p2, game.Creature("Safe", 1, 5), 4);
            Assert.AreEqual(5, safe.Health, "Cleansed before it went off");

            var mine = game.Spell("Mine", TargetType.EnemyCreatures, PlaceEffect(SlotEffectType.Landmine, 3));
            StringAssert.Contains("Landmine 3 on target slot.", Keywords.Plain(CardTextFormatter.GetRulesText(mine)));
        }

        [Test]
        public void ScorchedEarth_StopsTriggeredEffects_AndTheAbility_EntryOnesLost_ItCanStillMove() {
            var spiky = game.Summon(p1, game.Creature("Spiky", 0, 10, 1, Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 0);
            spiky.Ability = CreatureAbility.Spite;
            var hitter = game.Summon(p2, game.Creature("Hitter", 2, 10), 0);
            Place(p1.Battlefield[0], SlotEffectType.ScorchedEarth);
            game.Attack(hitter, p1.Battlefield[0]);
            Assert.AreEqual(10, hitter.Health, "Its retaliation doesn't trigger");
            Assert.IsFalse(Abilities.Has(spiky, CreatureAbility.Spite), "Its ability is switched off too");
            game.Run(new MoveCreatureAction(spiky, p1.Battlefield[0], p1.Battlefield[1], p1));
            game.Attack(hitter, p1.Battlefield[1]);
            Assert.AreEqual(9, hitter.Health, "It can still move; off the scorched slot, its effects work again...");
            Assert.IsTrue(Abilities.Has(spiky, CreatureAbility.Spite), "...and so does its ability");

            game.Summon(p1, game.Creature("Arrival", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Enemy)), 0);
            CollectionAssert.IsEmpty(game.History.DamageTo(p2), "Landing on Scorched Earth: no entry effect");

            var lateData = game.Creature("Late", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.Enemy));
            lateData.turnsUntilOperative = 1;
            Place(p1.Battlefield[2], SlotEffectType.ScorchedEarth);
            var late = game.Summon(p1, lateData, 2);
            game.StartTurn();
            game.StartTurn();
            Assert.IsTrue(late.IsOperative);
            CollectionAssert.IsEmpty(game.History.DamageTo(p2), "Becoming ready on it: no entry effect either");
            game.Run(new MoveCreatureAction(late, p1.Battlefield[2], p1.Battlefield[3], p1));
            game.StartTurn();
            CollectionAssert.IsEmpty(game.History.DamageTo(p2), "Lost, not delayed: moving off doesn't bring it back");
        }
    }
}
