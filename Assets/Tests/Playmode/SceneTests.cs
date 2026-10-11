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
        public IEnumerator TheGameStarts_WithTheScenesDecks_FullHealth_FiveCardsEach_AndEmptyBoards() {
            SceneGame.AssertPlays(p1, game.References.Player1Deck);
            SceneGame.AssertPlays(p2, game.References.Player2Deck);
            Assert.AreEqual(MatchSetup.DefaultOpponentName, p2.Name, "With no match chosen, the opponent keeps its default name");
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
        public IEnumerator AQueuedCreature_CanBeHovered_ToShowItEnlarged_WhetherItsSlotIsEmptyOrNot() {
            var occupant = game.Summon(p1, game.Cards.Creature("Test Ox", 0, 6), 3);
            var wolf = game.GiveCard(p1, game.Cards.Creature("Test Wolf", 2, 3));
            var hound = game.GiveCard(p1, game.Cards.Creature("Test Hound", 3, 1));
            game.Play(wolf, p1, p1.Battlefield[1]);  // An empty slot
            game.Play(hound, p1, p1.Battlefield[3]); // Replacing the ox
            yield return null;

            foreach (var (queued, slot) in new[] { (wolf, p1.Battlefield[1]), (hound, p1.Battlefield[3]) }) {
                var hits = SceneGame.Hover(game.GhostIn(slot));
                yield return null;

                var preview = SceneGame.HoverPreview();
                Assert.IsNotNull(preview, $"Hovering {queued.Name}'s ghost should show it enlarged");
                Assert.AreSame(queued, preview.GetLinkedCardInstance(), $"The preview over {slot} shows another card");
                Assert.Greater(preview.transform.localScale.x, 1f, "The preview should be enlarged");
                if (queued == wolf) { // The ghost replacing a creature is drawn raised, mostly outside its slot
                    Assert.IsTrue(hits.Any(h => h.gameObject.TryGetComponent(out BattlefieldSlot view) && view.Model == slot),
                        $"A card dropped on {queued.Name}'s ghost must still find {slot}");
                }
            }

            SceneGame.Hover(game.CardIn(p1.Battlefield[3]));
            yield return null;
            Assert.AreSame(occupant, SceneGame.HoverPreview()?.GetLinkedCardInstance(), "The creature about to be replaced can still be hovered");
        }

        [UnityTest]
        public IEnumerator AQueuedSpell_CanBeHovered_ToShowItEnlarged() {
            var bolt = game.GiveCard(p1, game.Cards.Spell("Test Bolt", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures)));
            game.Play(bolt, p1, p2.Battlefield[0]);
            yield return null;

            var shown = game.HandCards(p1).Single(c => c.GetLinkedCardInstance() == bolt);
            Assert.IsTrue(shown.IsQueued);
            SceneGame.Hover(shown);
            yield return null;

            var preview = SceneGame.HoverPreview();
            Assert.IsNotNull(preview, "Hovering a queued spell should show it enlarged");
            Assert.AreSame(bolt, preview.GetLinkedCardInstance());
            Assert.Greater(preview.transform.localScale.x, 1f);
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
                    game.PlayCreatures(player);
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
    }
}
