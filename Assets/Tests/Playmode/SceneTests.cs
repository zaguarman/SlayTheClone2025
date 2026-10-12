using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The whole game in its real scene: turns end through the End Turn button and play back on screen, and the
    /// views must keep showing what the rules hold. Any error logged during a test fails it.
    /// The rules themselves are tested without the scene, in the other test classes.
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
        public IEnumerator TheGameStarts_WithTheScenesDecks_AndEndTurn_EndsOneTurn_EvenWhenClickedTwice() {
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

            var endTurn = game.References.GetResolveActionsButton();
            endTurn.onClick.Invoke();
            endTurn.onClick.Invoke(); // While the first click's turn is still resolving
            yield return Wait.For(() => !game.Turns.IsResolving, "the turn to resolve");

            Assert.AreEqual(1, game.Turns.TurnNumber, "Two clicks ended two turns");
            game.AssertViewsMatchRules(); // Including the new hands
        }

        /// <summary>Slot Effects show on their slot. Hidden ones only for their controller: player 1 is the one at the screen.</summary>
        [UnityTest]
        public IEnumerator SlotEffects_ShowOnTheirSlot_HiddenOnesOnlyToTheirController() {
            game.Queue.AddAction(new PlaceSlotEffectAction(p2.Battlefield[1], SlotEffectType.Landmine, 2, 0, p1));
            game.Queue.AddAction(new PlaceSlotEffectAction(p1.Battlefield[1], SlotEffectType.Landmine, 2, 0, p2));
            game.Queue.AddAction(new PlaceSlotEffectAction(p1.Battlefield[3], SlotEffectType.TemporalShield, 0, 2, p1));
            game.Queue.ResolveActions();
            yield return null;

            string Badge(BoardSlot slot) => game.BoardView(slot.Owner).FindSlotView(slot).SlotEffectText;
            StringAssert.Contains("Landmine", Badge(p2.Battlefield[1]), "Your own mine, on the enemy's side");
            StringAssert.Contains("(hidden)", Badge(p2.Battlefield[1]));
            Assert.AreEqual("", Badge(p1.Battlefield[1]), "The enemy's mine is secret");
            StringAssert.Contains("Temporal Shield", Badge(p1.Battlefield[3]), "Visible ones show to everyone");

            game.Summon(p2, game.Cards.Creature("Test Sapper", 1, 5), 1);
            yield return null;
            Assert.AreEqual("", Badge(p2.Battlefield[1]), "Used up: gone from the slot");
            game.AssertViewsMatchRules();
        }

        /// <summary>Active Watchdogs are listed by their row: in full for their controller, "???" for the other side (GDD 3.11).</summary>
        [UnityTest]
        public IEnumerator Watchdogs_AreListedByTheirSide_TheEnemysConditionHidden() {
            var nullify = new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Nullify, charges = 2 };
            game.Queue.AddAction(new CreateWatchdogAction(nullify, p1, null, 0));
            game.Queue.AddAction(new CreateWatchdogAction(nullify, p2, null, 0));
            game.Queue.ResolveActions();
            yield return null;

            StringAssert.Contains("attacks by the enemy -> Nullify (2 left, On Watch)", game.BoardView(p1).WatchdogText);
            StringAssert.Contains("???", game.BoardView(p2).WatchdogText, "The enemy's condition is hidden");
            StringAssert.DoesNotContain("Nullify", game.BoardView(p2).WatchdogText);
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

            yield return game.EndTurn();
            Assert.AreSame(wolf, p1.Battlefield[1].OccupyingCreature);
            Assert.AreSame(hound, p1.Battlefield[3].OccupyingCreature);
            game.AssertViewsMatchRules();
        }

        [UnityTest]
        public IEnumerator AQueuedSpell_StaysInTheHandMarkedQueued_CanBeHovered_ThenHitsItsTarget() {
            var ox = game.Summon(p2, game.Cards.Creature("Test Ox", 0, 6), 0);
            var bolt = game.GiveCard(p1, game.Cards.Spell("Test Bolt", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures)));

            game.Play(bolt, p1, p2.Battlefield[0]);
            yield return null;
            var shown = game.HandCards(p1).SingleOrDefault(c => c.GetLinkedCardInstance() == bolt);
            Assert.IsNotNull(shown, "A queued spell stays in the hand view");
            Assert.IsTrue(shown.IsQueued, "...marked as queued");

            SceneGame.Hover(shown);
            yield return null;
            var preview = SceneGame.HoverPreview();
            Assert.IsNotNull(preview, "Hovering a queued spell should show it enlarged");
            Assert.AreSame(bolt, preview.GetLinkedCardInstance());
            Assert.Greater(preview.transform.localScale.x, 1f);

            yield return game.EndTurn();
            Assert.AreEqual(3, ox.Health);
            var allOfP1sCards = p1.Deck.GetDeckPreview().Concat(p1.Deck.GetDiscardPilePreview()).Concat(p1.Hand);
            Assert.AreEqual(1, allOfP1sCards.Count(c => c.Name == "Test Bolt"), "The cast spell should be in player 1's cards exactly once");
            game.AssertViewsMatchRules();
        }

        [UnityTest]
        public IEnumerator AQueuedSpell_AimedAtASlot_ShowsAnArrowFromItsCardToThatSlot_UntilItIsTakenBack() {
            game.Summon(p2, game.Cards.Creature("Test Ox", 0, 6), 1);
            var bolt = game.GiveCard(p1, game.Cards.Spell("Test Bolt", TargetType.Enemy,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.Enemy)));
            var storm = game.GiveCard(p1, game.Cards.Spell("Test Storm", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.EnemyCreatures, TargetModifier.AllSameTypeTargets)));
            var target = p2.Battlefield[1];

            game.Play(storm, p1, target);
            yield return null;
            CollectionAssert.IsEmpty(game.QueuedArrows(p1), "A spell that hits every creature of a kind isn't aimed: no arrow");

            game.Play(bolt, p1, target);
            yield return null;
            var arrow = game.QueuedArrows(p1).SingleOrDefault();
            Assert.IsNotNull(arrow, "The aimed spell should show one arrow");
            var card = game.HandCards(p1).Single(c => c.GetLinkedCardInstance() == bolt);
            var slotView = game.BoardView(p2).FindSlotView(target);
            Assert.Less(Vector2.Distance(arrow.StartPosition, card.transform.position), 0.01f, "It starts at the spell's card in the hand");
            Assert.Less(Vector2.Distance(arrow.EndPosition, slotView.transform.position), 0.01f, "It points at the slot the spell is aimed at");

            SceneGame.Click(card);
            yield return null;
            CollectionAssert.IsEmpty(game.QueuedArrows(p1), "Taking the spell back removes its arrow");
        }

        [UnityTest]
        public IEnumerator DraggingACreature_ShowsWhatEachSlotOfItsRowWouldMean() {
            var mover = game.Summon(p1, game.Cards.Creature("Test Mover", 1, 5), 0);
            game.Summon(p1, game.Cards.Creature("Test Wall", 1, 5), 2);
            yield return null;

            var card = game.CardIn(p1.Battlefield[0]);
            var drag = SceneGame.BeginDrag(card);
            Assert.AreEqual((MoveKind.Walk, false), game.MoveHintIn(p1.Battlefield[1]));
            Assert.AreEqual((MoveKind.Push, false), game.MoveHintIn(p1.Battlefield[2]), "The wall can be pushed into slot 3");
            Assert.AreEqual((MoveKind.Walk, true), game.MoveHintIn(p1.Battlefield[4]), "The wall is in the way: blocked");
            Assert.IsNull(game.MoveHintIn(p1.Battlefield[0]), "Its own slot");

            SceneGame.EndDrag(card, drag);
            Assert.IsNull(game.MoveHintIn(p1.Battlefield[1]), "The hints go when the drag ends");
            Assert.AreSame(p1.Battlefield[0], mover.Slot);
        }

        [UnityTest]
        public IEnumerator MoveArrows_ShowTheFinalFormation_IncludingCreaturesThatGetPushed() {
            var pusher = game.Summon(p1, game.Cards.Creature("Test Pusher", 1, 5), 0);
            var pushed = game.Summon(p1, game.Cards.Creature("Test Pushed", 1, 5), 1);
            yield return null;

            game.Queue.AddAction(new MoveCreatureAction(pusher, p1.Battlefield[0], p1.Battlefield[1], p1));
            yield return null;

            var arrows = game.QueuedArrows(p1);
            Assert.AreEqual(2, arrows.Count, "One arrow for the mover, one for the creature it pushes");
            Vector2 Slot(int i) => game.BoardView(p1).FindSlotView(p1.Battlefield[i]).transform.position;
            Assert.IsTrue(arrows.Any(a => Vector2.Distance(a.StartPosition, Slot(0)) < 0.01f && Vector2.Distance(a.EndPosition, Slot(1)) < 0.01f));
            Assert.IsTrue(arrows.Any(a => Vector2.Distance(a.StartPosition, Slot(1)) < 0.01f && Vector2.Distance(a.EndPosition, Slot(2)) < 0.01f),
                "The pushed creature's arrow shows where it ends up");
            Assert.AreSame(p1.Battlefield[1], pushed.Slot, "Nothing has moved yet");
        }

        /// <summary>A queued spell is taken back by clicking it, right-clicking it, or dragging it where it can't be played.</summary>
        [UnityTest]
        public IEnumerator AQueuedSpell_CanBeTakenBack_ByClicking_RightClicking_OrDraggingItAway() {
            var ox = game.Summon(p2, game.Cards.Creature("Test Ox", 0, 6), 0);
            var bolt = game.GiveCard(p1, game.Cards.Spell("Test Bolt", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures)));
            CardController Shown() => game.HandCards(p1).SingleOrDefault(c => c.GetLinkedCardInstance() == bolt);

            var takeBacks = new (string how, System.Action<CardController> takeBack)[] {
                ("Clicking", card => SceneGame.Click(card)),
                ("Right-clicking", card => SceneGame.Click(card, PointerEventData.InputButton.Right)),
                ("Dragging it where it can't be played", card => Assert.IsTrue(SceneGame.DragAndDrop(card, null), "A queued card in the hand can be dragged")),
            };
            foreach (var (how, takeBack) in takeBacks) {
                game.Play(bolt, p1, p2.Battlefield[0]);
                yield return null;
                Assert.IsTrue(Shown().IsQueued);

                takeBack(Shown());
                yield return null;
                Assert.IsNotNull(Shown(), $"{how}: the spell stays in the hand");
                Assert.IsFalse(Shown().IsQueued, $"{how} the queued spell should take the play back");
                Assert.IsFalse(game.Queue.GetPendingActions().OfType<PlayCardAction>().Any(), $"{how}: its play is no longer queued");
            }

            yield return game.EndTurn();
            Assert.AreEqual(6, ox.Health, "The spell that was taken back isn't cast");
            game.AssertViewsMatchRules();
        }

        [UnityTest]
        public IEnumerator AQueuedCreature_CanBeTakenBack_ByClickingItsGhost_RightClickingIt_OrDraggingItAway() {
            var pup = game.GiveCard(p1, game.Cards.Creature("Test Pup", 1, 2)) as ICreature;
            var slot = p1.Battlefield[0];

            var takeBacks = new (string how, System.Action<CardController> takeBack)[] {
                ("Clicking", ghost => SceneGame.Click(ghost)),
                ("Right-clicking", ghost => SceneGame.Click(ghost, PointerEventData.InputButton.Right)),
                ("Dragging it where it can't be played", ghost => Assert.IsTrue(SceneGame.DragAndDrop(ghost, null), "A queued creature's ghost can be dragged")),
            };
            foreach (var (how, takeBack) in takeBacks) {
                game.Play(pup, p1, slot);
                yield return null;
                Assert.IsFalse(game.HandCards(p1).Any(c => c.GetLinkedCardInstance() == pup), "A queued creature shows as a ghost on the board, not in the hand");

                takeBack(game.GhostIn(slot));
                yield return null;
                Assert.IsFalse(game.HasGhost(slot), $"{how} its ghost should take the play back");
                Assert.IsTrue(game.HandCards(p1).Any(c => c.GetLinkedCardInstance() == pup && !c.IsQueued), $"{how}: the creature is back in the hand");
                Assert.IsFalse(game.Queue.GetPendingActions().OfType<PlayCardAction>().Any(), $"{how}: its play is no longer queued");
            }

            yield return game.EndTurn();
            Assert.IsNull(slot.OccupyingCreature, "The creature that was taken back isn't summoned");
            game.AssertViewsMatchRules();
        }

        [UnityTest]
        public IEnumerator Combat_KillsCreatures_HurtsPlayers_AndTheCardsAndHealthFollow() {
            var brute = game.Summon(p1, game.Cards.Creature("Test Brute", 4, 5), 0);
            var mouse = game.Summon(p2, game.Cards.Creature("Test Mouse", 1, 2), 0);
            var striker = game.Summon(p1, game.Cards.Creature("Test Striker", 3, 3), 1);

            game.Attack(brute, p2.Battlefield[0]);
            game.Attack(mouse, p1.Battlefield[0]);
            game.Attack(striker, p2.Battlefield[1]); // An empty slot: the hit goes to player 2
            yield return game.EndTurn();

            Assert.IsTrue(mouse.IsDead);
            Assert.IsNull(game.CardIn(p2.Battlefield[0]), "The dead creature's card is still on the board");
            Assert.AreEqual(4, brute.Health, "At equal Speed the mouse, with less Health, strikes first");
            Assert.AreEqual(Player.MaxHealth - 3, p2.Health);
            game.AssertViewsMatchRules(); // Including the survivor's damage and player 2's health
        }
    }
}
