using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Zeitgeist.Enums;

namespace Zeitgeist.Tests {
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
            Assert.IsTrue(Intel.KnowsWhatItIs(p1, sneak), "A creature shows what it is when its summon finishes");
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
            Assert.AreEqual(Player.StartingHealth - 2, p1.Health, "A Watchdog can't match an action its controller can't see");
            Assert.AreEqual(1, game.ActionsQueue.Watchdogs.Single().ChargesLeft);

            var undercover = game.Creature("Mole Rat", 1, 10);
            undercover.ability = CreatureAbility.Undercover;
            var mole = game.Summon(p2, undercover, 1);
            Assert.IsFalse(Intel.KnowsWhatItIs(p1, mole), "...unless it's stealthy: it stays ??? to the other side");
            game.Declare(mole, p1.Battlefield[1]);
            Assert.IsFalse(P1Sees(PlanOf(mole)), "Undercover conceals its plans too");
            var eavesdrop = game.GiveCard(p1, game.Spell("Eavesdrop", TargetType.EnemyCreatures, new CardEffect {
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction> { new EffectAction { actionType = ActionType.Reveal, targetType = TargetType.EnemyCreatures } }
            }));
            game.Play(eavesdrop, p1, p2.Battlefield[1]);
            CollectionAssert.DoesNotContain(p1.Hand, eavesdrop, "A Reveal is instant: it resolved as it was played, during Planning");
            Assert.IsTrue(P1Sees(PlanOf(mole)), "...and shows its caster what the creature there plans");
            Assert.IsTrue(Intel.KnowsWhatItIs(p1, mole), "...and what it is, for good");
            Assert.AreEqual(1, game.ActionsQueue.GetPendingActionsCount(), "Nothing else resolved with it");
            game.Resolve();
            Assert.AreEqual(Player.StartingHealth - 2, p1.Health, "Revealed to the Watchdog's controller: it could match it");
            Assert.IsEmpty(game.ActionsQueue.Watchdogs);

            game.Run(new ApplyStatusEffectAction(sneak, StatusEffectType.Revealed, 1, 0));
            var cipher = game.Summon(p2, game.Creature("Cipher", 3, 10), 2);
            game.Run(new ApplyStatusEffectAction(cipher, StatusEffectType.Encrypted, 2, 0));
            game.Run(new CreateWatchdogAction(Nullify(2), p1, null, 0));
            game.Declare(sneak, p1.Battlefield[0]);
            game.Declare(cipher, p1.Battlefield[2]);
            Assert.IsTrue(P1Sees(PlanOf(sneak)), "Revealed cancels Concealed");
            game.Resolve();
            Assert.AreEqual(Player.StartingHealth - 2 - 3, p1.Health, "The Revealed attack is nullified; Watchdogs ignore an Encrypted creature's actions...");
            Assert.AreEqual(1, game.ActionsQueue.Watchdogs.Single().ChargesLeft, "...and spend no charge on them");

            var incoming = game.GiveCard(p2, game.Creature("Incoming", 1, 1));
            game.Play(incoming, p2, p2.Battlefield[3]);
            var summon = game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>().Single();
            Assert.IsFalse(Intel.CanSeeSummon(p1, summon, game.ActionsQueue), "The other side sees something is being summoned, not what");
            Assert.IsTrue(Intel.CanSeeSummon(p2, summon, game.ActionsQueue));
            Assert.IsTrue(FogOfWar.HidesSummon(p1, summon, game.ActionsQueue));

            game.Run(new PlaceSlotEffectAction(p2.Battlefield[4], SlotEffectType.Landmine, 2, 0, p2));
            Assert.IsTrue(Intel.KnowsWhatItIs(p1, (ICreature)incoming), "Its summon finished: now it shows what it is");
            var mine = p2.Battlefield[4].Effect;
            var watchdog = game.ActionsQueue.Watchdogs.Single();
            Assert.IsTrue(Intel.CanSee(p2, mine));
            Assert.IsFalse(Intel.CanSee(p1, mine), "Only its controller sees a hidden Slot Effect");
            Assert.IsFalse(Intel.CanSeeCondition(p2, watchdog), "...and a Watchdog's condition");
            game.Run(new RevealAction(p2.Battlefield[4], p1));
            game.Run(new RevealAction(p1, p2));
            Assert.IsTrue(Intel.CanSee(p1, mine), "Reveal on a slot shows its hidden Slot Effect");
            Assert.IsTrue(Intel.CanSeeCondition(p2, watchdog), "Reveal on a player shows their Watchdog conditions");

            // A random Reveal picks among the creatures that still hide something from its caster
            foreach (var creature in new[] { sneak, mole, cipher, (ICreature)incoming }) {
                game.Run(new ApplyStatusEffectAction(creature, StatusEffectType.Revealed, 3, 0));
            }
            game.Run(new PlaceSlotEffectAction(p2.Battlefield[2], SlotEffectType.Landmine, 2, 0, p2));
            Assert.IsFalse(Intel.HidesSomethingFrom(p1, mole, game.ActionsQueue), "Known and Revealed: nothing left to learn");
            Assert.IsTrue(Intel.HidesSomethingFrom(p1, cipher, game.ActionsQueue), "A hidden Slot Effect under it is something to learn");
            var lookAround = game.GiveCard(p1, game.Spell("Look Around", TargetType.EnemyCreatures, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Reveal, 0, TargetType.EnemyCreatures, TargetModifier.Random)));
            game.Play(lookAround, p1, p2.Battlefield[0]);
            Assert.IsTrue(Intel.CanSee(p1, p2.Battlefield[2].Effect), "...so it's the one picked, whatever the luck");

            // A decoy (Ghost Army): until it's revealed, the other side sees its fake card, on the screen and in its AI's view.
            // A token is nobody's card: it doesn't count, and it's gone when it dies
            using var g = new TestGame();
            var sherman = g.Creature("Sherman", 6, 6);
            var balloonData = g.Creature("Balloon", 0, 1);
            balloonData.token = true;
            balloonData.disguise = sherman;
            int cards = GameAsserts.CountCards(g.Player2);
            g.Run(new SummonDecoysAction(g.Player2, balloonData, 2));
            var balloons = g.Player2.Battlefield.Select(s => s.OccupyingCreature).Where(c => c?.Name == "Balloon").ToList();
            Assert.AreEqual(2, balloons.Count, "Two decoys, each in its own slot");
            var balloon = balloons[0];
            Assert.AreSame(sherman, FogOfWar.DisguiseFor(g.Player1, balloon), "The other side sees a Sherman...");
            Assert.AreEqual(6, new AIView(g.Player1, g.ActionsQueue).Of(balloon).Attack, "...and so does its AI");
            Assert.IsNull(FogOfWar.DisguiseFor(g.Player2, balloon), "Its own side knows it's a balloon");
            Assert.AreEqual(cards, GameAsserts.CountCards(g.Player2), "A token is nobody's card");
            g.Run(new RevealAction(balloon.Slot, g.Player1));
            Assert.IsNull(FogOfWar.DisguiseFor(g.Player1, balloon), "Revealed: the truth");
            g.Run(TargetedActions.Damage(balloon, 1));
            Assert.IsTrue(balloon.IsDead);
            Assert.AreEqual(cards, GameAsserts.CountCards(g.Player2), "Gone: no discard pile for a balloon");
        }

        /// <summary>
        /// The AI plays imperfect information too: at every level it plans exactly the same Day whatever it can't see (the
        /// other hand, their plans, hidden traps, what an unknown creature really is), and differently once it can see it.
        /// </summary>
        [Test]
        public void TheAI_AtEveryLevel_PlansTheSame_WhateverItCantSee() {
            foreach (AILevel level in System.Enum.GetValues(typeof(AILevel))) {
                var plans = PlanWith(level, hiddenDiffers: false, revealed: false);
                CollectionAssert.IsNotEmpty(plans, $"{level}: it plans something");
                CollectionAssert.AreEqual(plans, PlanWith(level, hiddenDiffers: true, revealed: false), $"{level}: its plans changed with what it can't see");
                if (level != AILevel.Easy) {
                    CollectionAssert.AreNotEqual(plans, PlanWith(level, hiddenDiffers: true, revealed: true), $"{level}: it ignores what it does see");
                }
            }

            // Very Hard plays the long game: it extracts one of ours that's Doomed (lost, though not today), and leaves alone
            // one of theirs it gave a Kill Order (dead already); Hard sees neither
            foreach (var level in new[] { AILevel.Hard, AILevel.VeryHard }) {
                using var g = new TestGame();
                var (me, them) = (g.Player1, g.Player2);
                var doomed = g.Summon(me, g.Creature("Doomed", 1, 9), 0);
                g.Run(new ApplyStatusEffectAction(doomed, StatusEffectType.Doomed, 3, 0, controller: them));
                g.Summon(me, g.Creature("Hitter", 3, 5), 1);
                var marked = g.Summon(them, g.Creature("Marked", 4, 3), 1);
                g.Run(new ApplyStatusEffectAction(marked, StatusEffectType.KillOrder, 2, 0, controller: me));
                g.GiveCard(me, g.Spell("Extraction", TargetType.FriendlyCreatures, TestGame.Effect(EffectTrigger.OnPlay, ActionType.ReturnToHand, 0, TargetType.FriendlyCreatures)));
                new AIPlanner(g.ActionsQueue, g.CombatHandler, level, seed: 7).PlanDay(me);
                var pending = g.ActionsQueue.GetPendingActions();
                bool extracts = pending.OfType<PlayCardAction>().Any(p => p.GetCard().Name == "Extraction");
                bool hitsMarked = pending.OfType<BattlefieldCombatAction>().Any(a => a.GetTargetSlot() == them.Battlefield[1]);
                Assert.AreEqual(level == AILevel.VeryHard, extracts, $"{level}: extracting the Doomed creature");
                Assert.AreEqual(level != AILevel.VeryHard, hitsMarked, $"{level}: attacking the creature it gave a Kill Order");
            }
        }

        // Plans player 1's Day in a game of its own. The board and hand player 1 sees are always the same; with hiddenDiffers,
        // all it can't see is different: player 2's hand and plans, traps they hid, and what their unknown creature is
        private static List<string> PlanWith(AILevel level, bool hiddenDiffers, bool revealed) {
            using var g = new TestGame();
            var (me, them) = (g.Player1, g.Player2);
            var brute = g.Summon(me, g.Creature("Brute", 3, 4), 0);
            g.Summon(me, g.Creature("Squire", 1, 2), 1);
            var guard = g.Summon(them, g.Creature("Guard", 1, 5), 0); // Zap can't kill it; the stranger it can, if it's what it seems
            var stranger = g.Summon(them, hiddenDiffers
                ? g.Creature("Titan", 9, 9, 1, TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 0, TargetType.Enemy))
                : g.Creature("Pawn", 2, 3), 2);
            if (!revealed) stranger.KnownTo.Remove(me); // A card back to player 1
            g.GiveCard(me, g.Spell("Zap", TargetType.EnemyCreatures, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures)));
            g.GiveCard(me, g.Spell("Pep Talk", TargetType.FriendlyCreatures, TestGame.Effect(EffectTrigger.OnPlay, ActionType.ModifyStat, 2, TargetType.FriendlyCreatures)));
            g.GiveCard(me, g.Creature("Recruit", 2, 2));

            if (hiddenDiffers) {
                g.Run(new ApplyStatusEffectAction(brute, StatusEffectType.KillOrder, 2, 0, controller: them)); // Secret from player 1
                g.Run(new PlaceSlotEffectAction(me.Battlefield[3], SlotEffectType.Landmine, 5, 0, them)); // Where player 1 would place a creature
                g.Run(new PlaceSlotEffectAction(them.Battlefield[4], SlotEffectType.Landmine, 5, 0, them));
                var nuke = g.GiveCard(them, g.Spell("Nuke", TargetType.EnemyCreatures, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 9, TargetType.EnemyCreatures)));
                g.GiveCard(them, g.Creature("Giant", 8, 8));
                g.Play(nuke, them, me.Battlefield[0]);
                g.Declare(guard, me.Battlefield[1]);
            }

            new AIPlanner(g.ActionsQueue, g.CombatHandler, level, seed: 7).PlanDay(me);

            string Where(ITarget aim) => aim switch {
                HomingTarget homing => homing.Creature.Name,
                BoardSlot slot => $"{(slot.Owner == me ? "our" : "their")} slot {slot.Index}",
                IPlayer player => player == me ? "us" : "them",
                _ => "?"
            };
            return g.ActionsQueue.GetPendingActions().Select(action => action switch {
                PlayCardAction play when play.GetOwner() == me => $"play {play.GetCard().Name} on {Where(play.GetTarget())}",
                BattlefieldCombatAction attack when attack.GetAttacker().Owner == me => $"{attack.GetAttacker().Name} attacks {Where(attack.GetTargetSlot())}",
                UseAbilityAction use when use.Creature.Owner == me => $"{use.Creature.Name} uses its ability on {Where(use.TargetSlot)}",
                MoveCreatureAction move when move.GetCreature().Owner == me => $"{move.GetCreature().Name} moves to {Where(move.GetToSlot())}",
                _ => null
            }).Where(plan => plan != null).OrderBy(plan => plan).ToList();
        }
    }
}
