using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Fog of War (GDD 2.5): what each side can see of the other's plans, hidden Slot Effects and Watchdogs, how Reveal and
    /// Revealed lift it, and how Watchdogs are blind to Concealed actions and ignore Encrypted ones. Switching the fog off
    /// for debugging changes only what the screen shows. Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class FogOfWarTests {
        private TestGame game;
        private Player p1;
        private Player p2;
        private bool fogWasOn;

        [SetUp]
        public void SetUp() {
            game = new TestGame();
            p1 = game.Player1;
            p2 = game.Player2;
            fogWasOn = FogOfWar.Enabled;
            FogOfWar.Enabled = true;
        }

        [TearDown]
        public void TearDown() {
            FogOfWar.Enabled = fogWasOn; // Keep the debug choice made in the editor
            game.Dispose();
        }

        private bool P1Sees(IGameAction action) => Intel.CanSee(p1, action, game.ActionsQueue, game.ModifierManager);
        private IGameAction PlanOf(ICreature creature) => game.ActionsQueue.GetPendingActions().Single(a => Intel.PlannerOf(a) == creature);

        private static WatchdogSpec Nullify(int charges) => new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Nullify, charges = charges };

        [Test]
        public void ConcealedPlans_AreHidden_UntilRevealed_WatchdogsCantSeeThem_NorTouchEncryptedOnes() {
            var sneak = game.Summon(p2, game.Creature("Sneak", 2, 10), 0);
            game.Run(new ApplyStatusEffectAction(sneak, StatusEffectType.Concealed, 2, 0));
            game.Run(new CreateWatchdogAction(Nullify(1), p1, null, 0)); // Before anything is planned: Run resolves the queue
            game.Declare(sneak, p1.Battlefield[0]); // An empty slot: it would hit player 1
            var attack = PlanOf(sneak);
            Assert.IsFalse(P1Sees(attack), "Concealed: the other side can't see what it plans");
            Assert.IsTrue(Intel.CanSee(p2, attack, game.ActionsQueue, game.ModifierManager), "Its own side can");
            Assert.IsTrue(FogOfWar.Hides(p1, attack, game.ActionsQueue, game.ModifierManager), "The screen shows ??? instead");
            FogOfWar.Enabled = false;
            Assert.IsFalse(FogOfWar.Hides(p1, attack, game.ActionsQueue, game.ModifierManager), "Debug: with the fog off the screen shows it");
            Assert.IsFalse(P1Sees(attack), "...but the rules don't change");
            FogOfWar.Enabled = true;
            game.Resolve();
            Assert.AreEqual(Player.MaxHealth - 2, p1.Health, "A Watchdog can't match an action its controller can't see");
            Assert.AreEqual(1, game.ActionsQueue.Watchdogs.Single().ChargesLeft);

            var undercover = game.Creature("Mole Rat", 1, 10);
            undercover.ability = CreatureAbility.Undercover;
            var mole = game.Summon(p2, undercover, 1);
            game.Declare(mole, p1.Battlefield[1]);
            Assert.IsFalse(P1Sees(PlanOf(mole)), "Undercover conceals too");
            var eavesdrop = game.GiveCard(p1, game.Spell("Eavesdrop", TargetType.EnemyCreatures, new CardEffect {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction> { new EffectAction { actionType = ActionType.Reveal, targetType = TargetType.EnemyCreatures } }
            }));
            game.Play(eavesdrop, p1, p2.Battlefield[1]);
            CollectionAssert.DoesNotContain(p1.Hand, eavesdrop, "A Reveal is instant: it resolved as it was played, during Planning");
            Assert.IsTrue(P1Sees(PlanOf(mole)), "...and shows its caster what the creature there plans");
            Assert.AreEqual(1, game.ActionsQueue.GetPendingActionsCount(), "Nothing else resolved with it");
            game.Resolve();
            Assert.AreEqual(Player.MaxHealth - 2, p1.Health, "Revealed to the Watchdog's controller: it could match it");
            Assert.IsEmpty(game.ActionsQueue.Watchdogs);

            game.Run(new ApplyStatusEffectAction(sneak, StatusEffectType.Revealed, 1, 0));
            var cipher = game.Summon(p2, game.Creature("Cipher", 3, 10), 2);
            game.Run(new ApplyStatusEffectAction(cipher, StatusEffectType.Encrypted, 2, 0));
            game.Run(new CreateWatchdogAction(Nullify(2), p1, null, 0));
            game.Declare(sneak, p1.Battlefield[0]);
            game.Declare(cipher, p1.Battlefield[2]);
            Assert.IsTrue(P1Sees(PlanOf(sneak)), "Revealed cancels Concealed");
            game.Resolve();
            Assert.AreEqual(Player.MaxHealth - 2 - 3, p1.Health, "The Revealed attack is nullified; Watchdogs ignore an Encrypted creature's actions...");
            Assert.AreEqual(1, game.ActionsQueue.Watchdogs.Single().ChargesLeft, "...and spend no charge on them");

            game.Run(new PlaceSlotEffectAction(p2.Battlefield[4], SlotEffectType.Landmine, 2, 0, p2));
            var mine = p2.Battlefield[4].Effect;
            var watchdog = game.ActionsQueue.Watchdogs.Single();
            Assert.IsTrue(Intel.CanSee(p2, mine));
            Assert.IsFalse(Intel.CanSee(p1, mine), "Only its controller sees a hidden Slot Effect");
            Assert.IsFalse(Intel.CanSeeCondition(p2, watchdog), "...and a Watchdog's condition");
            game.Run(new RevealAction(p2.Battlefield[4], p1));
            game.Run(new RevealAction(p1, p2));
            Assert.IsTrue(Intel.CanSee(p1, mine), "Reveal on a slot shows its hidden Slot Effect");
            Assert.IsTrue(Intel.CanSeeCondition(p2, watchdog), "Reveal on a player shows their Watchdog conditions");
        }
    }
}
