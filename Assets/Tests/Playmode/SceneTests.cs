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
        private bool fogWasOn;

        [UnitySetUp]
        public IEnumerator SetUp() {
            fogWasOn = FogOfWar.Enabled;
            FogOfWar.Enabled = true; // What the screen hides is part of what's tested
            game = new SceneGame();
            yield return game.Load(Seed);
            p1 = game.Player1;
            p2 = game.Player2;
        }

        [TearDown]
        public void TearDown() {
            FogOfWar.Enabled = fogWasOn; // Keep the debug choice made in the editor (while the scene can still redraw)
            game.Dispose();
        }

        [UnityTest]
        public IEnumerator TheGameStarts_EndTurnEndsOneTurn_EvenClickedTwice_AndCombatShowsOnScreen() {
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

            var brute = game.Summon(p1, game.Cards.Creature("Test Brute", 4, 5), 0);
            var mouse = game.Summon(p2, game.Cards.Creature("Test Mouse", 1, 2), 0);
            var striker = game.Summon(p1, game.Cards.Creature("Test Striker", 3, 3), 1);
            game.Attack(brute, p2.Battlefield[0]);
            game.Attack(mouse, p1.Battlefield[0]);
            game.Attack(striker, p2.Battlefield[1]); // An empty slot: the hit goes to player 2

            var endTurn = game.References.GetResolveActionsButton();
            endTurn.onClick.Invoke();
            endTurn.onClick.Invoke(); // While the first click's turn is still resolving
            yield return Wait.For(() => !game.Turns.IsResolving, "the turn to resolve");
            Assert.AreEqual(1, game.Turns.TurnNumber, "Two clicks ended two turns");

            Assert.IsTrue(mouse.IsDead);
            Assert.IsNull(game.CardIn(p2.Battlefield[0]), "The dead creature's card is still on the board");
            Assert.AreEqual(4, brute.Health, "At equal Speed the mouse, with less Health, strikes first");
            Assert.AreEqual(Player.MaxHealth - 3, p2.Health);
            game.AssertViewsMatchRules(); // Including the new hands, the survivor's damage and player 2's health
        }

        /// <summary>
        /// What the board shows besides the cards: Slot Effects (hidden ones only to their controller, player 1 at the screen),
        /// Watchdogs by their row (the enemy's condition hidden), "???" for the plans of a Concealed enemy (Fog of War, which
        /// can be switched off for debugging), move hints while dragging, and planned moves as ghosts and arrows.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBoardShows_SlotEffects_Watchdogs_MoveHints_AndPlannedMoves() {
            game.Queue.AddAction(new PlaceSlotEffectAction(p2.Battlefield[1], SlotEffectType.Landmine, 2, 0, p1));
            game.Queue.AddAction(new PlaceSlotEffectAction(p1.Battlefield[1], SlotEffectType.Landmine, 2, 0, p2));
            game.Queue.AddAction(new PlaceSlotEffectAction(p1.Battlefield[3], SlotEffectType.TemporalShield, 0, 2, p1));
            var nullify = new WatchdogSpec { watches = WatchedAction.Attack, side = WatchdogSide.Enemy, effect = WatchdogEffect.Nullify, charges = 2 };
            game.Queue.AddAction(new CreateWatchdogAction(nullify, p1, null, 0));
            game.Queue.AddAction(new CreateWatchdogAction(nullify, p2, null, 0));
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

            StringAssert.Contains("attacks by the enemy -> Nullify (2 left, On Watch)", game.BoardView(p1).WatchdogText);
            StringAssert.Contains("???", game.BoardView(p2).WatchdogText, "The enemy's Watchdog condition is hidden");
            StringAssert.DoesNotContain("Nullify", game.BoardView(p2).WatchdogText);

            var sneak = game.Summon(p2, game.Cards.Creature("Test Sneak", 2, 5), 4);
            var stealthy = game.Cards.Creature("Test Sleeper", 1, 5);
            stealthy.ability = CreatureAbility.Undercover;
            var sleeper = game.Summon(p2, stealthy, 2);
            game.Queue.AddAction(new ApplyStatusEffectAction(sneak, StatusEffectType.Concealed, 2, 0));
            game.Queue.ResolveActions();
            game.Attack(sneak, p1.Battlefield[4]);
            var incoming = game.GiveCard(p2, game.Cards.Creature("Test Incoming", 1, 5));
            game.Play(incoming, p2, p2.Battlefield[3]);
            yield return null;
            CollectionAssert.AreEqual(new[] { sneak }, game.BoardView(p2).CreaturesWithHiddenPlans, "A Concealed enemy's card shows ???...");
            CollectionAssert.IsEmpty(game.QueuedArrows(p2), "...instead of its attack arrow");
            CollectionAssert.AreEquivalent(new ICard[] { sleeper, incoming }, game.BoardView(p2).AnonymousCards,
                "An enemy summon shows as ??? until it finishes, and a stealthy creature stays ??? after");
            FogOfWar.Enabled = false;
            yield return null;
            CollectionAssert.IsEmpty(game.BoardView(p2).CreaturesWithHiddenPlans, "Debug: with the fog off the screen shows everything");
            CollectionAssert.IsEmpty(game.BoardView(p2).AnonymousCards);
            Assert.AreEqual(1, game.QueuedArrows(p2).Count);
            StringAssert.Contains("Nullify", game.BoardView(p2).WatchdogText, "...the enemy's Watchdog conditions too");
            FogOfWar.Enabled = true;
            yield return null;
            game.Queue.CancelDeclaredAction(sneak.TargetId);

            var mover = game.Summon(p1, game.Cards.Creature("Test Mover", 1, 5), 0);
            var wall = game.Summon(p1, game.Cards.Creature("Test Wall", 1, 5), 2);
            yield return null;
            var card = game.CardIn(p1.Battlefield[0]);
            var drag = SceneGame.BeginDrag(card);
            Assert.AreEqual((MoveKind.Walk, false), game.MoveHintIn(p1.Battlefield[1]));
            Assert.AreEqual((MoveKind.Push, false), game.MoveHintIn(p1.Battlefield[2]), "The wall can be pushed into slot 3");
            Assert.AreEqual((MoveKind.Walk, true), game.MoveHintIn(p1.Battlefield[4]), "The wall is in the way: blocked");
            Assert.IsNull(game.MoveHintIn(p1.Battlefield[0]), "Its own slot");
            SceneGame.EndDrag(card, drag);
            Assert.IsNull(game.MoveHintIn(p1.Battlefield[1]), "The hints go when the drag ends");

            game.Queue.AddAction(new MoveCreatureAction(mover, p1.Battlefield[0], p1.Battlefield[2], p1));
            yield return null;
            CollectionAssert.AreEquivalent(new[] { (mover, p1.Battlefield[2]), (wall, p1.Battlefield[3]) }, game.BoardView(p1).MoveGhosts.ToList(),
                "A ghost of each creature where it will end up, the pushed one included");
            var arrows = game.QueuedArrows(p1);
            Vector2 Slot(int i) => game.BoardView(p1).FindSlotView(p1.Battlefield[i]).transform.position;
            Assert.AreEqual(2, arrows.Count, "One arrow for the mover, one for the creature it pushes");
            Assert.IsTrue(arrows.Any(a => Vector2.Distance(a.StartPosition, Slot(2)) < 0.01f && Vector2.Distance(a.EndPosition, Slot(3)) < 0.01f),
                "The pushed creature's arrow shows where it ends up");
            Assert.AreSame(p1.Battlefield[0], mover.Slot, "Nothing has moved yet");
            game.AssertViewsMatchRules();

            game.Queue.ResolveActions();
            yield return null;
            Assert.IsEmpty(game.BoardView(p1).MoveGhosts, "The ghosts go once the moves are done");
            game.AssertViewsMatchRules();
        }

        /// <summary>A queued creature shows as a ghost on its slot: it can be hovered, and taken back by clicking, right-clicking or dragging it away.</summary>
        [UnityTest]
        public IEnumerator AQueuedCreature_CanBeHovered_OrTakenBack() {
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

            var pup = game.GiveCard(p1, game.Cards.Creature("Test Pup", 1, 2)) as ICreature;
            var pupSlot = p1.Battlefield[0];
            var takeBacks = new (string how, System.Action<CardController> takeBack)[] {
                ("Clicking", ghost => SceneGame.Click(ghost)),
                ("Right-clicking", ghost => SceneGame.Click(ghost, PointerEventData.InputButton.Right)),
                ("Dragging it where it can't be played", ghost => Assert.IsTrue(SceneGame.DragAndDrop(ghost, null), "A queued creature's ghost can be dragged")),
            };
            foreach (var (how, takeBack) in takeBacks) {
                game.Play(pup, p1, pupSlot);
                yield return null;
                Assert.IsFalse(game.HandCards(p1).Any(c => c.GetLinkedCardInstance() == pup), "A queued creature shows as a ghost on the board, not in the hand");
                takeBack(game.GhostIn(pupSlot));
                yield return null;
                Assert.IsFalse(game.HasGhost(pupSlot), $"{how} its ghost should take the play back");
                Assert.IsTrue(game.HandCards(p1).Any(c => c.GetLinkedCardInstance() == pup && !c.IsQueued), $"{how}: the creature is back in the hand");
            }

            yield return game.EndTurn();
            Assert.AreSame(wolf, p1.Battlefield[1].OccupyingCreature);
            Assert.AreSame(hound, p1.Battlefield[3].OccupyingCreature);
            Assert.IsNull(pupSlot.OccupyingCreature, "The creature that was taken back isn't summoned");
            game.AssertViewsMatchRules();
        }

        /// <summary>
        /// A queued spell stays in the hand marked queued: it can be hovered, shows an arrow to the slot it's aimed at, and is
        /// taken back by clicking it, right-clicking it, or dragging it where it can't be played.
        /// </summary>
        [UnityTest]
        public IEnumerator AQueuedSpell_StaysInTheHand_ShowsItsAim_CanBeTakenBack_ThenHitsItsTarget() {
            var ox = game.Summon(p2, game.Cards.Creature("Test Ox", 0, 6), 0);
            game.Summon(p2, game.Cards.Creature("Test Calf", 0, 6), 1);
            var bolt = game.GiveCard(p1, game.Cards.Spell("Test Bolt", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures)));
            var zap = game.GiveCard(p1, game.Cards.Spell("Test Zap", TargetType.Enemy,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.Enemy)));
            var storm = game.GiveCard(p1, game.Cards.Spell("Test Storm", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.EnemyCreatures, TargetModifier.AllSameTypeTargets)));
            CardController Shown(ICard card) => game.HandCards(p1).SingleOrDefault(c => c.GetLinkedCardInstance() == card);
            var aimedAt = p2.Battlefield[1];

            game.Play(storm, p1, aimedAt);
            yield return null;
            CollectionAssert.IsEmpty(game.QueuedArrows(p1), "A spell that hits every creature of a kind isn't aimed: no arrow");
            SceneGame.Click(Shown(storm));
            game.Play(zap, p1, aimedAt);
            yield return null;
            var arrow = game.QueuedArrows(p1).SingleOrDefault();
            Assert.IsNotNull(arrow, "The aimed spell shows one arrow");
            var slotView = game.BoardView(p2).FindSlotView(aimedAt);
            Assert.Less(Vector2.Distance(arrow.StartPosition, Shown(zap).transform.position), 0.01f, "It starts at the spell's card in the hand");
            Assert.Less(Vector2.Distance(arrow.EndPosition, slotView.transform.position), 0.01f, "It points at the slot the spell is aimed at");
            SceneGame.Click(Shown(zap));
            yield return null;
            CollectionAssert.IsEmpty(game.QueuedArrows(p1), "Taking the spell back removes its arrow");

            var takeBacks = new (string how, System.Action<CardController> takeBack)[] {
                ("Clicking", card => SceneGame.Click(card)),
                ("Right-clicking", card => SceneGame.Click(card, PointerEventData.InputButton.Right)),
                ("Dragging it where it can't be played", card => Assert.IsTrue(SceneGame.DragAndDrop(card, null), "A queued card in the hand can be dragged")),
            };
            foreach (var (how, takeBack) in takeBacks) {
                game.Play(bolt, p1, p2.Battlefield[0]);
                yield return null;
                Assert.IsTrue(Shown(bolt).IsQueued, "A queued spell stays in the hand view, marked as queued");
                takeBack(Shown(bolt));
                yield return null;
                Assert.IsNotNull(Shown(bolt), $"{how}: the spell stays in the hand");
                Assert.IsFalse(Shown(bolt).IsQueued, $"{how} the queued spell should take the play back");
                Assert.IsFalse(game.Queue.GetPendingActions().OfType<PlayCardAction>().Any(), $"{how}: its play is no longer queued");
            }

            game.Play(bolt, p1, p2.Battlefield[0]);
            yield return null;
            SceneGame.Hover(Shown(bolt));
            yield return null;
            var preview = SceneGame.HoverPreview();
            Assert.IsNotNull(preview, "Hovering a queued spell shows it enlarged");
            Assert.AreSame(bolt, preview.GetLinkedCardInstance());
            Assert.Greater(preview.transform.localScale.x, 1f);

            yield return game.EndTurn();
            Assert.AreEqual(3, ox.Health, "Only the spell left queued is cast");
            var allOfP1sCards = p1.Deck.GetDeckPreview().Concat(p1.Deck.GetDiscardPilePreview()).Concat(p1.Hand);
            Assert.AreEqual(1, allOfP1sCards.Count(c => c.Name == "Test Bolt"), "The cast spell is in player 1's cards exactly once");
            game.AssertViewsMatchRules();
        }
    }
}
