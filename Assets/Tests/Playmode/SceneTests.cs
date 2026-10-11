using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The whole game in its real scene: turns end through the End Turn button and play back on screen, and the
    /// views must keep showing what the rules hold. Any error logged during a test fails it.
    /// </summary>
    public class SceneTests {
        private const int Seed = 1234;
        private SceneGame game;
        private Player p1;
        private Player p2;

        [UnitySetUp]
        public IEnumerator SetUp() {
            game = new SceneGame();
            yield return game.Load(Seed);
            p1 = game.Player1;
            p2 = game.Player2;
        }

        [TearDown]
        public void TearDown() => game.Dispose();

        [UnityTest]
        public IEnumerator TheGameStarts_WithFullHealth_FiveCardsEach_AndEmptyBoards() {
            foreach (var player in game.Players) {
                Assert.AreEqual(Player.MaxHealth, player.Health);
                Assert.AreEqual(5, player.Hand.Count);
                Assert.IsTrue(player.Battlefield.All(s => !s.IsOccupied()));
                GameAsserts.AssertEachCardInOneZone(player);
            }
            Assert.AreEqual(0, game.Turns.TurnNumber);
            game.AssertViewsMatchRules();
            yield break;
        }

        [UnityTest]
        public IEnumerator EndingTheTurn_DiscardsAndRedrawsBothHands() {
            var cardCounts = game.Players.ToDictionary(p => p, GameAsserts.CountCards);
            var oldHands = game.Players.ToDictionary(p => p, p => p.Hand.ToList());

            yield return game.EndTurn();

            foreach (var player in game.Players) {
                Assert.AreEqual(Math.Min(5, cardCounts[player]), player.Hand.Count, $"{player.Name} should have drawn a new hand");
                CollectionAssert.IsEmpty(player.Hand.Intersect(oldHands[player]), $"{player.Name}'s old hand should have been discarded");
                Assert.AreEqual(cardCounts[player], GameAsserts.CountCards(player), $"{player.Name} lost or gained cards");
                GameAsserts.AssertEachCardInOneZone(player);
            }
            Assert.AreEqual(1, game.Turns.TurnNumber);
            game.AssertViewsMatchRules();
        }

        [UnityTest]
        public IEnumerator ClickingEndTurnAgainWhileItResolves_DoesNothing() {
            var endTurn = game.References.GetResolveActionsButton();

            endTurn.onClick.Invoke();
            endTurn.onClick.Invoke();
            yield return Wait.For(() => !game.Turns.IsResolving, "the turn to resolve");

            Assert.AreEqual(1, game.Turns.TurnNumber, "Two clicks ended two turns");
        }

        [UnityTest]
        public IEnumerator APlayedCreature_ShowsAsAGhostUntilTheTurnEnds_ThenLandsOnTheBoard() {
            var wolf = game.GiveCard(p1, game.Cards.Creature("Test Wolf", 2, 3));
            var slot = p1.Battlefield[2];

            game.Play(wolf, p1, slot);
            yield return null;
            CollectionAssert.DoesNotContain(game.HandCards(p1).Select(c => c.GetLinkedCardInstance()), wolf, "A queued creature leaves the hand view");
            Assert.IsTrue(game.HasGhost(slot), "A queued creature shows as a ghost in its slot");
            game.AssertViewsMatchRules();

            yield return game.EndTurn();
            Assert.AreSame(wolf, slot.OccupyingCreature);
            game.AssertViewsMatchRules();
        }

        [UnityTest]
        public IEnumerator AQueuedSpell_StaysInTheHandMarkedQueued_ThenHitsItsTarget() {
            var ox = game.Summon(p2, game.Cards.Creature("Test Ox", 0, 6), 0);
            var bolt = game.GiveCard(p1, game.Cards.Spell("Test Bolt", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures)));

            game.Play(bolt, p1, p2.Battlefield[0]);
            yield return null;
            var shown = game.HandCards(p1).SingleOrDefault(c => c.GetLinkedCardInstance() == bolt);
            Assert.IsNotNull(shown, "A queued spell stays in the hand view");
            Assert.IsTrue(shown.IsQueued, "...marked as queued");

            yield return game.EndTurn();
            Assert.AreEqual(3, ox.Health);
            var allOfP1sCards = p1.Deck.GetDeckPreview().Concat(p1.Deck.GetDiscardPilePreview()).Concat(p1.Hand);
            Assert.AreEqual(1, allOfP1sCards.Count(c => c.Name == "Test Bolt"), "The cast spell should be in player 1's cards exactly once");
            game.AssertViewsMatchRules();
        }

        [UnityTest]
        public IEnumerator AnAttackOnAnEmptySlot_DamagesThePlayer_AndTheHealthTextFollows() {
            var brute = game.Summon(p1, game.Cards.Creature("Test Brute", 4, 5), 0);

            game.Attack(brute, p2.Battlefield[0]);
            yield return game.EndTurn();

            Assert.AreEqual(Player.MaxHealth - 4, p2.Health);
            game.AssertViewsMatchRules();
        }

        [UnityTest]
        public IEnumerator ACreatureKilledInCombat_LeavesTheBoard_AndTheSurvivorsCardShowsItsDamage() {
            var brute = game.Summon(p1, game.Cards.Creature("Test Brute", 4, 5), 0);
            var mouse = game.Summon(p2, game.Cards.Creature("Test Mouse", 1, 2), 0);

            game.Attack(brute, p2.Battlefield[0]);
            game.Attack(mouse, p1.Battlefield[0]);
            yield return game.EndTurn();

            Assert.IsTrue(mouse.IsDead);
            Assert.IsNull(game.CardIn(p2.Battlefield[0]), "The dead creature's card is still on the board");
            Assert.AreEqual(4, brute.Health, "Equal speeds strike at the same time, so the mouse hits back");
            game.AssertViewsMatchRules();
        }

        [UnityTest]
        public IEnumerator SeveralTurnsOfPlay_KeepTheScreenInStepWithTheRules() {
            var cardCounts = game.Players.ToDictionary(p => p, GameAsserts.CountCards);

            for (int turn = 1; turn <= 6; turn++) {
                foreach (var player in game.Players) {
                    PlayCreatures(player);
                    foreach (var slot in player.Battlefield.Where(s => s.IsOccupied())) {
                        game.Attack(slot.OccupyingCreature, player.Opponent.Battlefield[slot.Index]);
                    }
                }
                yield return null;
                game.AssertViewsMatchRules();

                yield return game.EndTurn();
                game.AssertViewsMatchRules();
                foreach (var player in game.Players) {
                    Assert.AreEqual(cardCounts[player], GameAsserts.CountCards(player), $"{player.Name} lost or gained cards on turn {turn}");
                    GameAsserts.AssertEachCardInOneZone(player);
                }
                GameAsserts.AssertBoardsConsistent(p1, p2);
            }
        }

        // Every creature in the hand goes to an empty slot of its player's board
        private void PlayCreatures(Player player) {
            var freeSlots = new Queue<BoardSlot>(player.Battlefield.Where(s => !s.IsOccupied()));
            foreach (var creature in player.Hand.OfType<ICreature>().ToList()) {
                if (freeSlots.Count == 0) break;
                game.Play(creature, player, freeSlots.Dequeue());
            }
        }
    }
}
