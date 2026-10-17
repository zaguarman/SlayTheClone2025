using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Zeitgeist.Enums;

namespace Zeitgeist.Tests {
    /// <summary>
    /// An overview of how the game plays, one test per area: cards moving between zones, combat, and the board.
    /// Each runs the real rules headless (see <see cref="TestGame"/>); actions are queued, then resolved like an end of turn.
    /// </summary>
    public class GameplayTests {
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
        public void Cards_MoveFromDeckToHandToBoardToDiscardAndBack() {
            var wolf = game.Creature("Wolf", 2, 3, 1, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Enemy));
            var bolt = game.Spell("Bolt", TargetType.EnemyCreatures, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures));
            var fish = game.Creature("Fish", 1, 1);
            var bear = game.Creature("Bear", 1, 3);
            var memo = game.Spell("Memo", TargetType.Player);
            var p1Deck = new List<CardData> { wolf, bolt, fish, fish, memo };
            var p2Deck = new List<CardData> { bear };
            game.SetDecks(p1Deck, p2Deck);

            // Dealing draws the shuffled deck into the hand: 3 creatures and 2 spells
            game.CardDealingService.DealInitialHands(p1, p2, 5);
            Assert.AreEqual((3, 2), (Player.CreaturesIn(p1.Hand), Player.SpellsIn(p1.Hand)));
            Assert.AreEqual(0, p1.Deck.CardsRemaining);
            var dealtOrder = p1.Hand.Select(c => c.Name).ToList();

            // A creature played on a slot joins that board
            var bearCard = (ICreature)game.InHand(p2, "Bear");
            game.Run(new PlayCardAction(bearCard, p2, p2.Battlefield[0]));
            Assert.AreSame(bearCard, p2.Battlefield[0].OccupyingCreature);

            // ...leaves the hand, knows its slot, and fires its OnPlay effect (2 damage to the enemy player)
            var wolfCard = (ICreature)game.InHand(p1, "Wolf");
            int p2Health = p2.Health;
            game.Run(new PlayCardAction(wolfCard, p1, p1.Battlefield[1]));
            Assert.AreSame(wolfCard, p1.Battlefield[1].OccupyingCreature);
            Assert.AreSame(p1.Battlefield[1], wolfCard.Slot);
            CollectionAssert.DoesNotContain(p1.Hand, wolfCard);
            Assert.AreEqual(p2Health - 2, p2.Health);

            // A spell dropped on an enemy slot hits the creature there; the dead creature and the spent spell are discarded,
            // as fresh copies (see AssertDiscarded)
            var boltCard = game.InHand(p1, "Bolt");
            game.Run(new PlayCardAction(boltCard, p1, p2.Battlefield[0]));
            Assert.IsTrue(bearCard.IsDead);
            Assert.IsFalse(p2.Battlefield[0].IsOccupied());
            AssertDiscarded(p2, bearCard);
            AssertDiscarded(p1, boltCard);

            // The Day's end discards the creatures left in the hand, the spells stay; drawing a spell with none in the deck
            // shuffles the discard pile back in, and with no spell left there it takes a creature
            game.Run(new DiscardHandAction(p1));
            CollectionAssert.AreEqual(new[] { "Memo" }, p1.Hand.Select(c => c.Name).ToList());
            Assert.AreEqual(3, p1.Deck.GetDiscardPilePreview().Count); // Bolt and the two Fish

            game.Run(new DrawCardsAction(p1, 2));
            CollectionAssert.AreEquivalent(new[] { "Memo", "Bolt", "Fish" }, p1.Hand.Select(c => c.Name).ToList());
            Assert.AreEqual(1, p1.Deck.CardsRemaining);
            Assert.AreEqual(0, p1.Deck.GetDiscardPilePreview().Count);

            // Extraction: a creature goes back to its owner's hand, with the next Day's hand. Extracted while Revealed, it
            // comes back with a new face: played again, it arrives Concealed, unknown
            var spy = game.Summon(p1, game.Creature("Spy", 2, 2), 2);
            game.Run(new ApplyStatusEffectAction(spy, StatusEffectType.Revealed, 2, 0, controller: p2));
            int cards = GameAsserts.CountCards(p1);
            game.Run(new ReturnToHandAction(spy));
            Assert.IsNull(spy.Slot, "Extracted: off the board...");
            CollectionAssert.DoesNotContain(p1.Hand.Select(c => c.Name).ToList(), "Spy", "...not in this hand, which is discarded at Dusk...");
            game.Run(new DiscardHandAction(p1));
            var back = (ICreature)game.InHand(p1, "Spy");
            Assert.AreEqual(cards, GameAsserts.CountCards(p1), "...but in the next one");
            game.Run(new PlayCardAction(back, p1, p1.Battlefield[2]));
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(back, StatusEffectType.Concealed) && !Intel.KnowsWhatItIs(p2, back), "A new face: Concealed, unknown");

            // He Didn't Kill Himself: it "dies", so its Last Will happens, then it goes back to the hand
            var martyr = game.Summon(p1, game.Creature("Martyr", 1, 1, 1, TestGame.Effect(EffectTrigger.OnDeath, ActionType.Damage, 3, TargetType.Enemy)), 3);
            p2Health = p2.Health;
            cards = GameAsserts.CountCards(p1);
            game.Run(new ReturnToHandAction(martyr, fakesItsDeath: true));
            Assert.AreEqual(p2Health - 3, p2.Health, "Its Last Will happens");
            Assert.IsTrue(p1.ComingBack.Any(c => c.Name == "Martyr") && !martyr.IsDead, "...and it's on its way back to the hand, alive");
            Assert.AreEqual(cards, GameAsserts.CountCards(p1));

            // Witness Protection Program: off the board until the next Dawn, then back with a new identity
            var witness = game.Summon(p1, game.Creature("Witness", 2, 5), 4);
            cards = GameAsserts.CountCards(p1);
            game.Run(new VanishAction(witness));
            Assert.IsNull(witness.Slot, "Gone dark");
            Assert.AreEqual(cards, GameAsserts.CountCards(p1), "Still one of the player's cards");
            game.StartTurn();
            Assert.AreSame(witness, p1.Battlefield[4].OccupyingCreature, "Back at Dawn, in its slot");
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(witness, StatusEffectType.Concealed) && !Intel.KnowsWhatItIs(p2, witness), "...as ???");
            Assert.AreEqual(cards, GameAsserts.CountCards(p1));
            // Its stat changes stayed behind: a Health raise it was living on takes its Health back, and that can kill it
            // (found by the AI games: a decoy came back at 0 Health and stayed on the board)
            game.Run(new ModifyAction(witness, 3, false, true, false, controller: p1));
            game.Run(TargetedActions.Damage(witness, 6));
            game.Run(new VanishAction(witness));
            game.StartTurn();
            Assert.IsTrue(witness.IsDead, "Back without its +3 Health, 2 left minus 3: it dies");
            GameAsserts.AssertBoardsConsistent(p1, p2);

            // The same random seed shuffles the same way, so a game can be replayed
            using (var replay = new TestGame()) {
                replay.SetDecks(p1Deck, p2Deck);
                replay.CardDealingService.DealInitialHands(replay.Player1, replay.Player2, 5);
                CollectionAssert.AreEqual(dealtOrder, replay.Player1.Hand.Select(c => c.Name).ToList());
            }
        }

        [Test]
        public void Combat_HitsWhateverIsInTheTargetSlot() {
            var hound = game.Summon(p1, game.Creature("Hound", 3, 5), 0);
            var sheep = game.Summon(p2, game.Creature("Sheep", 1, 3, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 0);
            var porcupine = game.Summon(p2, game.Creature("Porcupine", 0, 10, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 1);

            // Attacking an occupied slot damages its creature; a creature that dies leaves the board for the discard pile.
            // A killing blow still fires OnDamage effects (GDD 3.9), so the Sheep strikes back as it dies.
            Attack(hound, p2.Battlefield[0]);
            Assert.IsTrue(sheep.IsDead);
            Assert.AreEqual(hound.MaxHealth - 1, hound.Health);
            Assert.IsFalse(p2.Battlefield[0].IsOccupied());
            AssertDiscarded(p2, sheep);

            // Attacking an empty slot damages the player who owns it, announced once
            int p2Health = p2.Health;
            var playerHits = new List<int>();
            game.Mediator.PlayerDamaged += (player, damage) => playerHits.Add(damage);
            Attack(hound, p2.Battlefield[0]);
            Assert.AreEqual(p2Health - hound.Attack, p2.Health);
            CollectionAssert.AreEqual(new[] { hound.Attack }, playerHits);

            // An enemy-targeted OnDamage effect strikes back at the attacker, on every hit
            var jackal = game.Summon(p1, game.Creature("Jackal", 1, 4), 1);
            int houndHealth = hound.Health;
            game.CombatHandler.HandleCreatureCombat(hound, p2.Battlefield[1]);
            game.CombatHandler.HandleCreatureCombat(jackal, p2.Battlefield[1]);
            game.Resolve();
            Assert.AreEqual(porcupine.MaxHealth - hound.Attack - jackal.Attack, porcupine.Health);
            Assert.AreEqual(houndHealth - 1, hound.Health);
            Assert.AreEqual(jackal.MaxHealth - 1, jackal.Health);

            // Effects that change stats go through the queue too, landing before anything else that waits
            var cucumber = game.Summon(p2, game.Creature("Cucumber", 0, 4, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.ModifyStat, 2, TargetType.FriendlyCreatures)), 3);
            Attack(hound, p2.Battlefield[3]);
            Assert.AreEqual(2, cucumber.Attack);
            Assert.AreEqual(6 - hound.Attack, cucumber.Health); // Max health 4 + 2, minus the hit

            // Attacks declared in the same turn resolve by speed: a creature killed first never lands its hit
            var falcon = game.Summon(p1, game.Creature("Falcon", 4, 2, speed: 3), 2);
            var boar = game.Summon(p2, game.Creature("Boar", 4, 2, speed: 1), 2);
            int falconChanges = 0, boarChanges = 0;
            falcon.Changed += _ => falconChanges++;
            boar.Changed += _ => boarChanges++;
            game.CombatHandler.HandleCreatureCombat(falcon, p2.Battlefield[2]);
            game.CombatHandler.HandleCreatureCombat(boar, p1.Battlefield[2]);
            game.Resolve();
            Assert.IsTrue(boar.IsDead);
            Assert.IsFalse(falcon.IsDead);
            Assert.AreEqual(falcon.MaxHealth, falcon.Health);

            // Each creature announces its own changes (what its card listens to): only the boar was hit and died
            Assert.AreEqual(2, boarChanges);
            Assert.AreEqual(0, falconChanges);

            // Effects can trigger each other forever: two Rams that grow from every hit and hurt every enemy creature when hit.
            // The queue stops after a set number of actions, reports it as an error and drops the rest.
            var ram = game.Creature("Ram", 1, 5, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.ModifyStat, 1, TargetType.Self),
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.EnemyCreatures, TargetModifier.AllSameTypeTargets));
            var p1Ram = game.Summon(p1, ram, 3);
            game.Summon(p2, ram, 4);
            LogAssert.Expect(LogType.Error, new Regex("Stopped resolving after"));
            Attack(p1Ram, p2.Battlefield[4]);
            Assert.AreEqual(0, game.ActionsQueue.GetPendingActionsCount());
        }

        [Test]
        public void Board_PositionsDecideMovesReplacementsAndChains() {
            var eel = game.Summon(p1, game.Creature("Eel", 3, 4, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 4, TargetType.EnemyCreatures, TargetModifier.Chained)), 0);
            var crabs = new[] {
                game.Summon(p2, game.Creature("Crab A", 0, 10), 1),
                game.Summon(p2, game.Creature("Crab B", 0, 10), 3),
                game.Summon(p2, game.Creature("Crab C", 0, 10), 4)
            };

            // A chain attack jumps from the creature hit to the next one a step away in the formation, halving each jump:
            // the left Flank, then the left Headquarters behind it, then the other Headquarters slot
            Attack(eel, p2.Battlefield[1]);
            Assert.AreEqual(10 - 3, crabs[0].Health); // The attack itself
            Assert.AreEqual(10 - 4, crabs[1].Health); // First jump
            Assert.AreEqual(10 - 2, crabs[2].Health); // Second jump

            // Moving to an empty slot relocates the creature
            game.Run(new MoveCreatureAction(eel, p1.Battlefield[0], p1.Battlefield[4], p1));
            Assert.IsFalse(p1.Battlefield[0].IsOccupied());
            Assert.AreSame(p1.Battlefield[4], eel.Slot);

            // Moving onto a friendly creature a step away: they trade places
            var turtle = game.Summon(p1, game.Creature("Turtle", 1, 6), 3);
            game.Run(new MoveCreatureAction(eel, p1.Battlefield[4], p1.Battlefield[3], p1));
            Assert.AreSame(eel, p1.Battlefield[3].OccupyingCreature);
            Assert.AreSame(turtle, p1.Battlefield[4].OccupyingCreature);

            // Summoning onto an occupied slot replaces its creature, which is discarded, and what it planned goes with it
            var sharkData = game.Creature("Shark", 4, 4);
            sharkData.movement = MovementKeyword.Swap;
            int p2HealthBeforeReplacing = p2.Health;
            game.Declare(eel, p2.Battlefield[0]); // An empty slot: the attack would hit the player
            var shark = game.Summon(p1, sharkData, 3);
            Assert.AreSame(shark, p1.Battlefield[3].OccupyingCreature);
            Assert.IsNull(eel.Slot);
            AssertDiscarded(p1, eel);
            Assert.AreEqual(p2HealthBeforeReplacing, p2.Health, "A replaced creature's attack leaves with it");

            // Effects that picked it before it left fizzle: it can't be hurt or destroyed again, nor discarded twice (found by the AI tournament)
            int cards = GameAsserts.CountCards(p1);
            game.Run(TargetedActions.Damage(eel, 9));
            game.Run(new DestroyCreatureAction(eel, unconditional: true));
            Assert.AreEqual(cards, GameAsserts.CountCards(p1), "A creature that left play went to the discard pile again");
            Assert.AreEqual(1, p1.Deck.GetDiscardPilePreview().Count(c => c.CardId == eel.CardId));

            // Late Arrivals (the user, 2026-10-16): He Protec on the creature in the slot, or Sorry I'm Late on the newcomer, holds
            // the replacement until after the attacks: the one there takes the Day's hits first, then it's replaced
            var guardData = game.Creature("Guard Dog", 0, 9);
            guardData.ability = CreatureAbility.HeProtec;
            var guard = game.Summon(p1, guardData, 1);
            var biter = game.Summon(p2, game.Creature("Biter", 3, 5), 2);
            var pup = (ICreature)game.GiveCard(p1, game.Creature("Pup", 1, 2));
            game.Play(pup, p1, p1.Battlefield[1]);
            game.Declare(biter, p1.Battlefield[1]);
            Assert.AreEqual(ResolutionLayer.LateArrivals, game.ActionsQueue.LayerOfPending(game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>().Single()),
                "It waits in the last layer, after the attacks");
            game.Resolve();
            Assert.AreEqual(6, guard.Health, "He Protec: it stays through the attacks and takes the hit on its slot...");
            Assert.AreSame(pup, p1.Battlefield[1].OccupyingCreature, "...then the newcomer takes its place");
            Assert.AreEqual(2, pup.Health, "...unhurt");
            AssertDiscarded(p1, guard);
            var lateData = game.Creature("Latecomer", 2, 4);
            lateData.ability = CreatureAbility.SorryImLate;
            var latecomer = (ICreature)game.GiveCard(p1, lateData);
            game.Play(latecomer, p1, p1.Battlefield[1]);
            game.Declare(biter, p1.Battlefield[1]);
            game.Resolve();
            Assert.IsTrue(pup.IsDead, "Sorry I'm Late: the creature it replaces takes the Day's hits (and dies of this one)...");
            Assert.AreSame(latecomer, p1.Battlefield[1].OccupyingCreature, "...and it arrives after them, into the free slot");
            Assert.AreEqual(4, latecomer.Health);
            game.Run(new DestroyCreatureAction(latecomer, unconditional: true)); // Out of the way of what follows

            // Irreplaceable (Demagogue's drawback): no summon can take its slot; and an effect on "other" creatures leaves
            // its own creature out
            var leaderData = game.Creature("Leader", 2, 4, 1, TestGame.Effect(EffectTrigger.OnPlay, ActionType.ModifyStat, 2, TargetType.FriendlyCreatures, TargetModifier.Others));
            leaderData.effects[0].actions[0].modifyAttack = true;
            leaderData.effects[0].actions[0].modifyHealth = false;
            leaderData.ability = CreatureAbility.Irreplaceable;
            int turtleAttack = turtle.Attack;
            var leader = game.Summon(p1, leaderData, 1);
            Assert.AreEqual(2, leader.Attack, "Its +2 Attack goes to the other friendly creatures, not itself...");
            Assert.AreEqual(turtleAttack + 2, turtle.Attack, "...the others get it");
            var usurper = (ICreature)game.GiveCard(p1, game.Creature("Usurper", 3, 3));
            game.Play(usurper, p1, p1.Battlefield[1]);
            Assert.IsEmpty(game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>(), "Irreplaceable: a summon onto it is refused...");
            CollectionAssert.Contains(p1.Hand, usurper, "...and the card stays in the hand");
            game.Run(new DestroyCreatureAction(leader, unconditional: true));
            game.Run(new DestroyCreatureAction(biter, unconditional: true));

            // An exhausted (stunned) creature can't move or attack
            game.Run(new ApplyStatusEffectAction(turtle, StatusEffectType.Exhausted, 1, 0));
            game.Run(new MoveCreatureAction(turtle, p1.Battlefield[4], p1.Battlefield[0], p1));
            Assert.AreSame(turtle, p1.Battlefield[4].OccupyingCreature);
            Assert.IsFalse(p1.Battlefield[0].IsOccupied());
            int healthBeforeStunnedAttack = p2.Health;
            Attack(turtle, p2.Battlefield[4]); // An empty slot: the attack would hit the player
            Assert.AreEqual(healthBeforeStunnedAttack, p2.Health);

            // A creature that needs time to become operative can't attack yet and its effects are off, but it can move (the
            // user, 2026-10-16); its on-play effect (2 damage to the enemy player) waits until it is operative
            var golemData = game.Creature("Golem", 3, 5, 1, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Enemy));
            golemData.turnsUntilOperative = 1;
            int p2Health = p2.Health;
            var golem = game.Summon(p1, golemData, 0);
            Assert.IsFalse(golem.IsOperative);
            Assert.AreEqual(p2Health, p2.Health);

            Attack(golem, p2.Battlefield[0]);
            Assert.AreEqual(p2Health, p2.Health);
            game.Run(new MoveCreatureAction(golem, p1.Battlefield[0], p1.Battlefield[1], p1));
            Assert.AreSame(golem, p1.Battlefield[1].OccupyingCreature, "Deploying, it still takes its place");
            game.Run(new MoveCreatureAction(golem, p1.Battlefield[1], p1.Battlefield[0], p1)); // ...and back, for what follows

            // ...and an operative creature with Swap can trade places with it, over the turtle in between
            game.Run(new MoveCreatureAction(shark, p1.Battlefield[3], p1.Battlefield[0], p1));
            Assert.AreSame(golem, p1.Battlefield[3].OccupyingCreature);
            Assert.AreSame(shark, p1.Battlefield[0].OccupyingCreature);

            // Once it has sat out a full Day after the one it arrived in, it is operative: the on-play effect fires and it can attack
            game.StartTurn();
            Assert.IsFalse(golem.IsOperative, "Deployment Time never counts down at the Dusk of the Day the creature arrived");
            game.StartTurn();
            Assert.IsTrue(golem.IsOperative);
            Assert.AreEqual(p2Health - 2, p2.Health);
            Attack(golem, p2.Battlefield[0]);
            Assert.AreEqual(p2Health - 2 - golem.Attack, p2.Health);

            // A Gift is played into an empty enemy slot and joins their side: each Dusk it hurts every creature there, itself
            // included. It's still its giver's card, and goes back to the giver's discard pile
            var giftData = game.Creature("Bad Apple", 0, 1, 1,
                TestGame.Effect(EffectTrigger.EndOfTurn, ActionType.Damage, 1, TargetType.FriendlyCreatures, TargetModifier.AllSameTypeTargets));
            giftData.gift = true;
            var apple = (ICreature)game.GiveCard(p1, giftData);
            int p1Cards = GameAsserts.CountCards(p1), p2Cards = GameAsserts.CountCards(p2);
            game.Play(apple, p1, p2.Battlefield[2]); // The right Flank, empty
            game.Resolve();
            Assert.AreSame(apple, p2.Battlefield[2].OccupyingCreature, "A Gift lands in the enemy slot it was given into...");
            Assert.AreSame(p2, apple.Owner, "...on their side");
            Assert.AreEqual(p1Cards, GameAsserts.CountCards(p1), "It's still the giver's card");
            Assert.AreEqual(p2Cards, GameAsserts.CountCards(p2));
            int crabHealth = crabs[0].Health;
            game.StartTurn();
            Assert.AreEqual(crabHealth - 1, crabs[0].Health, "At Dusk it hurts the creatures on its side...");
            Assert.IsTrue(apple.IsDead, "...itself included");
            AssertDiscarded(p1, apple);
            Assert.AreEqual(p1Cards, GameAsserts.CountCards(p1), "Back in the giver's discard pile");
            Assert.AreEqual(p2Cards, GameAsserts.CountCards(p2));

            var secondApple = game.GiveCard(p1, giftData);
            game.Play(secondApple, p1, p2.Battlefield[1]);
            game.Resolve();
            Assert.AreSame(crabs[0], p2.Battlefield[1].OccupyingCreature, "Given into a taken slot, a Gift fizzles: it never replaces their creature");
            Assert.AreEqual(2, p1.Deck.GetDiscardPilePreview().Count(c => c.CardId == apple.CardId), "...and goes back to the giver's discard pile");
            // A Gift's arrival hits its new side (Suspicious USB Stick in the Parking Lot): one of theirs is Exhausted, never itself
            var stickData = game.Creature("USB Stick", 0, 1, 1,
                TestGame.Effect(EffectTrigger.OnPlay, ActionType.Stun, 1, TargetType.FriendlyCreatures, TargetModifier.Random | TargetModifier.Others));
            stickData.gift = true;
            var stick = (ICreature)game.GiveCard(p1, stickData);
            var free = p2.Battlefield.First(slot => !slot.IsOccupied());
            game.Play(stick, p1, free);
            game.Resolve();
            Assert.AreSame(p2, stick.Owner);
            Assert.IsTrue(p2.Battlefield.Select(slot => slot.OccupyingCreature).Any(c => c != null && c != stick && game.ModifierManager.HasStatusEffect(c, StatusEffectType.Exhausted)),
                "Its arrival infects one of its new side");
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(stick, StatusEffectType.Exhausted), "...not itself");

            // Taking control (GDD 5.4): the creature goes to the slot facing it, on its new controller's side, for good.
            // Operation Paperclip only takes creatures with 2 Attack or less
            var paperclip = game.Spell("Paperclip", TargetType.EnemyCreatures, TestGame.Effect(EffectTrigger.OnPlay, ActionType.TakeControl, 2, TargetType.EnemyCreatures));
            paperclip.effects[0].actions[0].requires = TargetRequisite.AttackAtMost;
            var clip = game.GiveCard(p1, paperclip);
            p1Cards = GameAsserts.CountCards(p1);
            p2Cards = GameAsserts.CountCards(p2);
            game.Play(clip, p1, p2.Battlefield[1]);
            game.Resolve();
            Assert.AreSame(crabs[0], p1.Battlefield[1].OccupyingCreature, "It changes sides, to the slot facing it...");
            Assert.AreSame(p1, crabs[0].Owner, "...under its new controller");
            Assert.AreEqual((p1Cards, p2Cards), (GameAsserts.CountCards(p1), GameAsserts.CountCards(p2)), "It's still its owner's card");
            var brute = game.Summon(p2, game.Creature("Brute", 5, 5), 0);
            game.Play(game.GiveCard(p1, paperclip), p1, p2.Battlefield[0]);
            game.Resolve();
            Assert.AreSame(p2, brute.Owner, "5 Attack: too strong to take");

            // Coup d'État: their strongest, only if they have fewer creatures. It lands on the shark, which is discarded
            var coup = game.Spell("Coup", TargetType.EnemyCreatures,
                TestGame.Effect(EffectTrigger.OnPlay, ActionType.TakeControl, 0, TargetType.EnemyCreatures, TargetModifier.Strongest));
            coup.effects[0].actions[0].requires = TargetRequisite.Outnumbered;
            game.Play(game.GiveCard(p1, coup), p1, null);
            game.Resolve();
            Assert.AreSame(brute, p1.Battlefield[0].OccupyingCreature, "Coup d'État: 4 creatures against 3, their strongest comes over");
            AssertDiscarded(p1, shark);
            game.Run(new DestroyCreatureAction(crabs[0], unconditional: true));
            AssertDiscarded(p2, crabs[0]);
            Assert.AreEqual(p2Cards + 1, GameAsserts.CountCards(p2), "A stolen creature goes back to its owner's discard pile (the Brute was summoned)");
            GameAsserts.AssertBoardsConsistent(p1, p2);
        }

        // A card that leaves play goes to its owner's discard pile as a fresh copy: damage, buffs and death don't carry over
        private static void AssertDiscarded(Player owner, ICard card) {
            var copy = owner.Deck.GetDiscardPilePreview().FirstOrDefault(c => c.CardId == card.CardId);
            Assert.IsNotNull(copy, $"{card.Name} is not in {owner.Name}'s discard pile");
            Assert.AreNotSame(card, copy);
            if (copy is ICreature creature) {
                Assert.IsFalse(creature.IsDead);
                Assert.AreEqual(creature.MaxHealth, creature.Health);
            }
        }

        // Declares an attack the way dragging a creature onto a slot does, then resolves it
        private void Attack(ICreature attacker, BoardSlot target) {
            game.CombatHandler.HandleCreatureCombat(attacker, target);
            game.Resolve();
        }
    }
}
