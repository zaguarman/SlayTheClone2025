using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The board: one intent per creature a Day, and what attacks, spells and effects hit.
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

        // An empty board for the next part of a test
        private void Fresh() {
            GameAsserts.AssertBoardsConsistent(p1, p2);
            game.Dispose();
            SetUp();
        }

        [Test]
        public void Aiming_OneIntentADay_SpellsHitWhereDropped_FollowYourOwn_EnemiesCanDodge() {
            var runner = game.Summon(p1, game.Creature("Runner", 2, 5), 0);
            var target = game.Summon(p2, game.Creature("Target", 0, 10), 0);
            game.Declare(runner, p2.Battlefield[0]);
            game.ActionsQueue.AddAction(new MoveCreatureAction(runner, p1.Battlefield[0], p1.Battlefield[2], p1));
            Assert.AreEqual(1, game.ActionsQueue.GetPendingActionsCount(), "One intent a Day: moving after declaring an attack replaces the attack");
            game.Resolve();
            Assert.AreSame(runner, p1.Battlefield[2].OccupyingCreature);
            CollectionAssert.IsEmpty(game.History.DamageTo(target), "The replaced attack doesn't happen");
            game.ActionsQueue.AddAction(new MoveCreatureAction(runner, p1.Battlefield[2], p1.Battlefield[4], p1));
            game.Declare(runner, p2.Battlefield[0]);
            Assert.AreEqual(1, game.ActionsQueue.GetPendingActionsCount(), "...and an attack replaces a move");
            game.Resolve();
            Assert.AreSame(runner, p1.Battlefield[2].OccupyingCreature);
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(target));

            var bolt = game.Spell("Bolt", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures));
            var ox = game.Summon(p2, game.Creature("Ox", 0, 10), 1);
            game.Run(new PlayCardAction(game.GiveCard(p1, bolt), p1, p2.Battlefield[1]));
            Assert.AreEqual(8, target.Health, "A spell hits only the creature it was dropped on");
            Assert.AreEqual(7, ox.Health);
            game.Run(new PlayCardAction(game.GiveCard(p1, bolt), p1, p2.Battlefield[3]));
            Assert.AreEqual(Player.MaxHealth - 3, p2.Health, "...or that side's player on an empty slot");
            // An effect that requires its target to be of a faction: Cyberpsychosis hits Digital Society harder
            var trollData = game.Creature("Troll", 0, 10);
            trollData.faction = Faction.DigitalSociety;
            var troll = game.Summon(p2, trollData, 2);
            var psycho = game.Spell("Psycho", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.EnemyCreatures));
            psycho.effects[0].actions.Add(new EffectAction { actionType = ActionType.Damage, value = 3, targetType = TargetType.EnemyCreatures, requires = TargetRequisite.Online });
            game.Run(new PlayCardAction(game.GiveCard(p1, psycho), p1, p2.Battlefield[2]));
            game.Run(new PlayCardAction(game.GiveCard(p1, psycho), p1, p2.Battlefield[1]));
            Assert.AreEqual((5, 5), (troll.Health, ox.Health), "2 and 3 more on the terminally online, 2 on anyone else");
            // An empty enemy slot a creature is being summoned into this Day: summons land before spells, so a spell that
            // hits creatures can be aimed there, and hits it (Necronomicon on a summon)
            var doom = (Spell)game.GiveCard(p1, game.Spell("Doom", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Destroy, 0, TargetType.EnemyCreatures)));
            Assert.IsFalse(EffectResolver.CanAimAt(doom, p1, p2.Battlefield[4], game.ActionsQueue), "Nothing there yet: nothing to destroy");
            var summoned = game.GiveCard(p2, game.Creature("Summoned", 1, 3));
            game.Play(summoned, p2, p2.Battlefield[4]);
            Assert.IsTrue(EffectResolver.CanAimAt(doom, p1, p2.Battlefield[4], game.ActionsQueue), "A creature on its way there: it can be aimed at");
            game.Play(doom, p1, p2.Battlefield[4]);
            game.Resolve();
            Assert.IsTrue(((ICreature)summoned).IsDead, "...and it's hit as it lands");

            Fresh();
            var plating = game.Spell("Plating", TargetType.FriendlyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Armor, 2, TargetType.FriendlyCreatures));
            var mover = game.Summon(p1, game.Creature("Mover", 1, 5), 0);
            game.ActionsQueue.AddAction(new MoveCreatureAction(mover, p1.Battlefield[0], p1.Battlefield[1], p1));
            game.Play(game.GiveCard(p1, plating), p1, p1.Battlefield[0]);
            game.Resolve();
            Assert.AreEqual(2, mover.CurrentArmorPool, "Moves resolve first, and a spell on your own creature follows it");

            var newcomer = (ICreature)game.GiveCard(p1, game.Creature("Newcomer", 1, 5));
            game.Play(newcomer, p1, p1.Battlefield[2]);
            game.Play(game.GiveCard(p1, plating), p1, p1.Battlefield[2]); // Dropped on the queued creature
            game.Resolve();
            Assert.AreEqual(2, newcomer.CurrentArmorPool, "The spell homes on the creature queued there");
            var late = (ICreature)game.GiveCard(p1, game.Creature("Late", 1, 5));
            game.Play(late, p1, p1.Battlefield[3]);
            game.Play(game.GiveCard(p1, plating), p1, p1.Battlefield[3]);
            game.ActionsQueue.CancelPlay(late);
            game.ActionsQueue.AddAction(new MoveCreatureAction(newcomer, p1.Battlefield[2], p1.Battlefield[3], p1));
            game.Resolve();
            Assert.AreEqual(0, late.CurrentArmorPool);
            Assert.AreEqual(4, newcomer.CurrentArmorPool, "With the summon taken back, it lands on whatever is in the slot it was aimed at");

            Fresh(); // The hand is full
            bolt = game.Spell("Bolt", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures));
            var enemyPlating = game.Spell("Plating", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Armor, 2, TargetType.EnemyCreatures));
            var dodger = game.Summon(p2, game.Creature("Dodger", 1, 5), 0);
            var stepper = game.Summon(p2, game.Creature("Stepper", 1, 5), 2);
            game.ActionsQueue.AddAction(new MoveCreatureAction(dodger, p2.Battlefield[0], p2.Battlefield[1], p2));
            game.ActionsQueue.AddAction(new MoveCreatureAction(stepper, p2.Battlefield[2], p2.Battlefield[3], p2));
            game.Play(game.GiveCard(p1, enemyPlating), p1, p2.Battlefield[0]);
            game.Play(game.GiveCard(p1, bolt), p1, p2.Battlefield[2]);
            game.Resolve();
            Assert.AreEqual(0, dodger.CurrentArmorPool, "Enemy creatures can dodge: the Armor needs a creature in the slot, so it fizzles");
            Assert.AreEqual(5, stepper.Health);
            Assert.AreEqual(Player.MaxHealth - 3, p2.Health, "Damage doesn't need one: into an empty slot it hits that side's player");
        }

        [Test]
        public void Areas_GroupsHitEachMemberOnce_AdjacentAndSpreadingHitTheNeighbours() {
            var mine = game.Summon(p1, game.Creature("Mine", 0, 5), 0);
            var theirsA = game.Summon(p2, game.Creature("Theirs A", 0, 5), 0);
            var theirsB = game.Summon(p2, game.Creature("Theirs B", 0, 5), 3);
            var storm = game.Summon(p1, game.Creature("Storm", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.EnemyCreatures)), 1);
            Assert.AreEqual(5, mine.Health, "Enemy creatures only");
            var quake = game.Summon(p1, game.Creature("Quake", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.AllCreatures)), 2);
            foreach (var creature in new[] { mine, storm, quake }) Assert.AreEqual(4, creature.Health, $"{creature.Name}: all creatures, including itself");
            game.Summon(p1, game.Creature("Medic", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Heal, 1, TargetType.FriendlyCreatures)), 3);
            foreach (var creature in new[] { mine, storm, quake }) Assert.AreEqual(5, creature.Health, $"{creature.Name}: friendly creatures");
            foreach (var creature in new[] { theirsA, theirsB }) {
                CollectionAssert.AreEqual(new[] { 1, 1 }, game.History.DamageTo(creature), $"{creature.Name}: hit once by each enemy group effect, healed by none");
            }

            Fresh();
            var dogpile = game.Spell("Dogpile", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.EnemyCreatures, TargetModifier.WithAdjacent));
            var enemies = Enumerable.Range(0, 5).Select(i => game.Summon(p2, game.Creature($"Enemy {i}", 0, 10), i)).ToList();
            game.Run(new PlayCardAction(game.GiveCard(p1, dogpile), p1, p2.Battlefield[3]));
            // Landing on the left Headquarters: its neighbours in the formation are the left Flank and the other Headquarters slot
            Assert.AreEqual(new[] { 10, 9, 10, 9, 9 }, enemies.Select(e => e.Health).ToArray(), "A spell with Adjacent also hits the creatures a step away from where it lands");

            var cleaver = game.Summon(p1, game.Creature("Cleaver", 2, 5, 1,
                Effect(EffectTrigger.OnDamage, ActionType.Damage, 0, TargetType.EnemyCreatures, TargetModifier.WithAdjacent)), 0);
            var rage = game.GiveCard(p1, game.Spell("Rage", TargetType.FriendlyCreatures, Effect(EffectTrigger.OnPlay, ActionType.ModifyStat, 3, TargetType.FriendlyCreatures)));
            game.Declare(cleaver, p2.Battlefield[1]);
            game.ActionsQueue.AddAction(new PlayCardAction(rage, p1, p1.Battlefield[0])); // Spells land before attacks
            game.Resolve();
            // The left Flank and its neighbours in the formation: the Vanguard, the right Flank, the left Headquarters
            Assert.AreEqual(new[] { 5, 4, 5, 4, 9 }, enemies.Select(e => e.Health).ToArray(),
                "A spreading attack hits the target and its neighbours, for the attack it has when the hits land");

            // Team Building Exercise: on the side it's played on, either one, +1 Attack, and 1 damage outside Headquarters
            Fresh();
            var front = game.Summon(p1, game.Creature("Front", 1, 5), 0);
            var boss = game.Summon(p1, game.Creature("Boss", 1, 5), 3);
            var rival = game.Summon(p2, game.Creature("Rival", 1, 5), 1);
            var pep = Effect(EffectTrigger.OnPlay, ActionType.ModifyStat, 1, TargetType.AllCreatures, TargetModifier.AllSameTypeTargets | TargetModifier.DroppedSide);
            pep.actions[0].modifyAttack = true;
            pep.actions[0].modifyHealth = false;
            var hurt = Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.AllCreatures, TargetModifier.AllSameTypeTargets | TargetModifier.DroppedSide);
            hurt.actions[0].requires = TargetRequisite.OutsideHeadquarters;
            var retreat = game.Spell("Retreat", TargetType.AllCreatures, pep, hurt);
            StringAssert.Contains("every creature on the side it's played on outside Headquarters", Keywords.Plain(CardTextFormatter.GetRulesText(retreat)));
            game.Run(new PlayCardAction(game.GiveCard(p1, retreat), p1, p1));
            Assert.AreEqual((2, 4), (front.Attack, front.Health), "Ours: +1 Attack, and the staff take 1...");
            Assert.AreEqual((2, 5), (boss.Attack, boss.Health), "...Headquarters skips the pain");
            Assert.AreEqual((1, 5), (rival.Attack, rival.Health), "The other side is untouched");
            game.Run(new PlayCardAction(game.GiveCard(p1, retreat), p1, p2));
            Assert.AreEqual((2, 4), (rival.Attack, rival.Health), "Played on their side, it's theirs");
        }
    }
}
