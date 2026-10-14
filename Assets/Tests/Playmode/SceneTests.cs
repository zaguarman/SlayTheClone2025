using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
            FogOfWar.PlaysAsPlayer1 = true;
            game = new SceneGame();
            yield return game.Load(Seed);
            p1 = game.Player1;
            p2 = game.Player2;
        }

        [TearDown]
        public void TearDown() {
            FogOfWar.Enabled = fogWasOn; // Keep the debug choice made in the editor (while the scene can still redraw)
            FogOfWar.PlaysAsPlayer1 = true;
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

            Object.FindFirstObjectByType<DeckViewController>().ToggleDeckView();
            var deckWindow = Object.FindFirstObjectByType<DeckViewUI>(FindObjectsInactive.Include);
            Assert.IsTrue(deckWindow.IsOpen, "View Deck opens the deck");
            Vector2 windowCenter = Camera.main.WorldToScreenPoint(deckWindow.DeckViewPanel.transform.position);
            Assert.IsFalse(deckWindow.ClickAt(windowCenter), "A click inside it keeps it open...");
            Assert.IsTrue(deckWindow.ClickAt(new Vector2(2f, Screen.height - 2f)), "...a click outside it closes it");
            yield return null;

            var brute = game.Summon(p1, game.Cards.Creature("Test Brute", 4, 5), 0);
            var mouse = game.Summon(p2, game.Cards.Creature("Test Mouse", 1, 2), 0);
            var striker = game.Summon(p1, game.Cards.Creature("Test Striker", 3, 3), 1);
            var flanker = game.Summon(p1, game.Cards.Creature("Test Flanker", 1, 3), 2);
            game.Attack(brute, p2.Battlefield[0]);
            game.Attack(mouse, p1.Battlefield[0]);
            game.Attack(striker, p2.Battlefield[1]); // An empty slot: the hit goes to player 2

            // Battle arrows: creatures attacking the same slot share one arrow, their tails merging into one head
            game.Attack(flanker, p2.Battlefield[0]);
            yield return null;
            Assert.AreEqual(2, game.QueuedArrows(p1).Count, "Three attacks, two targets: two arrows");
            Assert.AreEqual(2, game.QueuedArrows(p1).Max(a => a.Branches), "The two attacks on the same slot merge into one arrow");
            Assert.AreEqual(1, game.QueuedArrows(p2).Count, "The other side's attack has its own arrow");
            Assert.IsTrue(game.QueuedArrows(p1).All(a => a.Color.b > a.Color.r), "Your attack arrows are blue...");
            Assert.IsTrue(game.QueuedArrows(p2).All(a => a.Color.r > a.Color.b), "...the enemy's red");
            game.Queue.CancelDeclaredAction(flanker.TargetId);
            yield return null;
            Assert.IsTrue(game.QueuedArrows(p1).All(a => a.Branches == 1), "Called off: that arrow loses its branch");

            var endTurn = game.References.GetResolveActionsButton();
            endTurn.onClick.Invoke();
            endTurn.onClick.Invoke(); // While the first click's turn is still resolving
            yield return Wait.For(() => !game.Turns.IsResolving, "the turn to resolve");
            Assert.AreEqual(1, game.Turns.TurnNumber, "Two clicks ended two turns");
            var queuePanel = game.QueuePanel;
            CollectionAssert.AreEqual(new[] { 0 }, queuePanel.PastDays, "The queue keeps the Day that just ended");
            queuePanel.SetOpen(true);
            queuePanel.Page(-1);
            Assert.AreEqual(0, queuePanel.ShownDay);
            StringAssert.Contains("Test Brute", string.Join("\n", queuePanel.Lines), "Paging back shows what was planned that Day");
            queuePanel.Page(+1);
            Assert.IsNull(queuePanel.ShownDay, "...and forward, the queue as it is now");
            queuePanel.SetOpen(false);

            Assert.IsTrue(mouse.IsDead);
            Assert.IsNull(game.CardIn(p2.Battlefield[0]), "The dead creature's card is still on the board");
            Assert.AreEqual(4, brute.Health, "At equal Speed the mouse, with less Health, strikes first");
            Assert.AreEqual(Player.MaxHealth - 3, p2.Health);
            game.AssertViewsMatchRules(); // Including the new hands, the survivor's damage and player 2's health

            // At 0 Health the combat is over: a screen says who won, and no more turns are played
            game.Queue.AddAction(new DamagePlayerAction(p2, p2.Health));
            game.Queue.ResolveActions();
            yield return null;
            var over = Object.FindFirstObjectByType<GameUI>().GameOver;
            Assert.IsTrue(over.IsShown, "Player 2 at 0 Health: the game over screen covers the table");
            Assert.IsTrue(over.Title == "Victory" || over.Title == $"{p1.Name} wins", $"Player 1 won, the screen says {over.Title}");
            Assert.IsFalse(endTurn.interactable, "End Turn goes grey");
            endTurn.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, game.Turns.TurnNumber, "...and a turn ended anyway doesn't happen");
        }

        /// <summary>
        /// What the board shows besides the cards: Slot Effects (hidden ones only to their controller, player 1 at the screen),
        /// Watchdogs by their row (the enemy's condition hidden), "???" for the plans of a Concealed enemy (Fog of War, which
        /// can be switched off for debugging), the Queue panel under the same fog, move hints while dragging, and planned moves
        /// as ghosts and arrows.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBoardShows_SlotEffects_Watchdogs_TheQueue_MoveHints_AndPlannedMoves() {
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

            StringAssert.Contains("attacks by the enemy -> Nullify (2 left, Monitored)", game.BoardView(p1).WatchdogText);
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
            Assert.IsNotEmpty(game.HandCards(p2));
            Assert.IsTrue(game.HandCards(p2).All(c => c.IsFaceDown), "The enemy's hand shows only card backs");
            Assert.IsFalse(game.HandCards(p1).Any(c => c.IsFaceDown));
            var hex = game.GiveCard(p2, game.Cards.Spell("Test Hex", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.EnemyCreatures)));
            game.Play(hex, p2, p1.Battlefield[1]);
            yield return null;
            Assert.IsTrue(game.SpellGhost(hex).IsFaceDown, "An enemy spell shows where it's aimed, as a card back: not what it is");
            game.Queue.CancelPlay(hex);
            p2.RemoveFromHand(hex); // A test card left in a hand outlives its definition into the next test
            yield return null;
            Assert.IsFalse(FogOfWar.CanControl(p2), "...and its cards can't be used");
            var queuePanel = game.QueuePanel;
            string Listed() => string.Join("\n", queuePanel.Lines);
            Assert.IsFalse(queuePanel.IsOpen, "The queue panel starts closed");
            ExecuteEvents.Execute(queuePanel.Toggle.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            Assert.IsTrue(queuePanel.IsOpen, "The Queue button opens it");
            StringAssert.Contains("Test Sneak", Listed());
            StringAssert.Contains("plans ???", Listed(), "The queue shows a Concealed plan as ???...");
            StringAssert.DoesNotContain("Test Incoming", Listed(), "...and what an enemy summons");
            QueueItemHover Row(string text) => queuePanel.Panel.GetComponentsInChildren<QueueItemHover>().First(r => r.Details.Contains(text));
            void Hover(QueueItemHover row, bool on) {
                if (on) row.OnPointerEnter(new PointerEventData(EventSystem.current));
                else row.OnPointerExit(new PointerEventData(EventSystem.current));
            }
            Hover(Row("plans ???"), true);
            StringAssert.Contains("can't see what it plans", Tooltip.Existing.ShownText, "Each action is an item: hovering it tells more, as far as you can see");
            Hover(Row("plans ???"), false);
            Assert.IsNull(Tooltip.Existing.ShownText, "...until the pointer leaves it");
            float Height(Component view) => view.transform.position.y;
            FogOfWar.PlaysAsPlayer1 = false;
            yield return null;
            Assert.IsTrue(game.HandCards(p1).All(c => c.IsFaceDown), "Player 2's view: now player 1's hand is the hidden one");
            CollectionAssert.IsEmpty(game.BoardView(p2).AnonymousCards, "...and player 2 knows their own cards");
            Assert.Less(Height(game.BoardView(p2)), Height(game.BoardView(p1)), "...and their side of the table is at the bottom");
            Assert.Less(Height(game.HandView(p2)), Height(game.BoardView(p2)), "...hand below their mat");
            Assert.Greater(Height(game.HandView(p1)), Height(game.BoardView(p1)), "...and player 1's hand at the top");
            Assert.AreEqual(HandUI.YourHandScale, game.HandCards(p2)[0].transform.localScale.x, 0.01f, "Player 2's hand is the big one now...");
            Assert.AreEqual(1f, game.HandCards(p1)[0].transform.localScale.x, 0.01f, "...and player 1's is at board size");
            FogOfWar.PlaysAsPlayer1 = true;
            yield return null;
            Assert.Less(Height(game.BoardView(p1)), Height(game.BoardView(p2)), "Switching back puts player 1 at the bottom again");
            Assert.Less(Height(game.HandView(p1)), Height(game.BoardView(p1)));
            FogOfWar.Enabled = false;
            yield return null;
            CollectionAssert.IsEmpty(game.BoardView(p2).CreaturesWithHiddenPlans, "Debug: with the fog off the screen shows everything");
            var enemyDeckButton = GameObject.Find("EnemyDeckViewButton");
            Assert.IsNotNull(enemyDeckButton, "...the enemy's deck and discard pile get buttons");
            Object.FindFirstObjectByType<DeckViewController>().ToggleEnemyDeckView();
            var deckWindow = Object.FindFirstObjectByType<DeckViewUI>(FindObjectsInactive.Include);
            Assert.IsTrue(deckWindow.IsOpen, "...that open their deck");
            var texts = deckWindow.DeckViewPanel.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Select(t => t.text).ToList();
            CollectionAssert.Contains(texts, "Their Deck", "...with their deck's title");
            CollectionAssert.Contains(texts, $"Cards in deck: {p2.Deck.CardsRemaining}", "...and their cards");
            deckWindow.HideDeckView();
            CollectionAssert.IsEmpty(game.BoardView(p2).AnonymousCards);
            Assert.AreEqual(1, game.QueuedArrows(p2).Count);
            var enemyCard = game.HandCards(p2)[0];
            Assert.IsFalse(enemyCard.IsFaceDown, "...the enemy's hand face up");
            SceneGame.Hover(enemyCard);
            yield return SceneGame.PreviewShows();
            Assert.AreSame(enemyCard, SceneGame.HoverPreview(), "...whose cards come down out of their hand, enlarged, like yours rise out of yours");
            Assert.AreEqual(HandUI.RaisedScale, enemyCard.transform.localScale.x, 0.01f);
            Assert.Less(Height(enemyCard), Height(game.HandView(p2)));
            ExecuteEvents.Execute(enemyCard.gameObject, new PointerEventData(EventSystem.current) { position = Vector2.zero }, ExecuteEvents.pointerExitHandler);
            Tooltip.Existing.HideTooltip();
            StringAssert.Contains("Nullify", game.BoardView(p2).WatchdogText, "...the enemy's Watchdog conditions too");
            StringAssert.Contains("Test Sneak</color> attacks", Listed(), "...and the queue");
            StringAssert.Contains("Test Incoming", Listed());
            Hover(Row("Test Sneak</color> attacks"), true);
            StringAssert.Contains($"Speed {sneak.Speed}", Tooltip.Existing.ShownText, "...who does it, with the Speed that orders its layer");
            Hover(Row("Test Sneak</color> attacks"), false);
            ExecuteEvents.Execute(queuePanel.Toggle.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            Assert.IsFalse(queuePanel.IsOpen, "Clicking it again closes it");
            FogOfWar.Enabled = true;
            yield return null;
            Assert.IsFalse(enemyDeckButton.activeSelf, "With the fog back on, the enemy's piles are hidden again");
            game.Queue.CancelDeclaredAction(sneak.TargetId);

            // The icons above a creature's art: raised and lowered stats get one each, and hovering one says what changed
            game.Queue.AddAction(new ModifyAction(sneak, 2, true, false, false, 2));
            game.Queue.ResolveActions();
            yield return null;
            var sneakCard = game.CardIn(sneak.Slot);
            StringAssert.Contains("icon:stats up", sneakCard.StatusText.text, "A raised stat shows an arrow up");
            StringAssert.Contains("+2 Attack, 2 Days left", sneakCard.DescribeIcon("icon:stats up")?.Body, "...whose popup lists the change");

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
                // Going on from one enlarged card to the next is quicker
                yield return queued == wolf ? SceneGame.PreviewShows() : new WaitForSecondsRealtime(Tooltip.SwitchDelay + 0.05f);
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
            yield return SceneGame.PreviewShows();
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
        /// A dragged card lights up only where it can land: a slot for creatures and aimed spells, a whole battlefield for
        /// spells that hit a side, both for spells that play on either side (which can be dropped anywhere on the table),
        /// and nothing on a side it can't be played on; hovering without a card lights up nothing.
        /// A queued spell leaves the hand and shows as a ghost where it's aimed: in an empty slot, small on the edge of a
        /// taken slot or of a whole side. The ghost can be hovered, and the play is taken back by clicking it, right-clicking
        /// it, or dragging it where it can't be played. The keywords on an enlarged card open their explanations when
        /// hovered, and the keywords in those can be followed in turn.
        /// </summary>
        [UnityTest]
        public IEnumerator ACardLightsUpWhereItLands_AQueuedSpellShowsAsAGhostWhereItsAimed_CanBeTakenBack_ItsKeywordsExplainThemselves_ThenItHits() {
            var ox = game.Summon(p2, game.Cards.Creature("Test Ox", 0, 6), 0);
            game.Summon(p2, game.Cards.Creature("Test Calf", 0, 6), 1);
            var bolt = game.GiveCard(p1, game.Cards.Spell("Test Bolt", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures)));
            var zap = game.GiveCard(p1, game.Cards.Spell("Test Zap", TargetType.Enemy,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.Enemy)));
            var storm = game.GiveCard(p1, game.Cards.Spell("Test Storm", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.EnemyCreatures, TargetModifier.AllSameTypeTargets)));
            var quake = game.GiveCard(p1, game.Cards.Spell("Test Quake", TargetType.AllCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Stun, 1, TargetType.AllCreatures, TargetModifier.Random)));
            CardController Shown(ICard card) => game.HandCards(p1).SingleOrDefault(c => c.GetLinkedCardInstance() == card);
            var aimedAt = p2.Battlefield[1];
            float Height(Component view) => view.transform.position.y;
            float X(Component view) => view.transform.position.x;

            game.Play(storm, p1, aimedAt);
            yield return null;
            var sideGhost = game.SpellGhost(storm);
            Assert.IsNotNull(sideGhost, "A queued spell shows as a ghost on the board...");
            Assert.IsNull(Shown(storm), "...instead of in the hand");
            CollectionAssert.IsEmpty(game.QueuedArrows(p1), "It needs no arrow");
            Assert.IsTrue(sideGhost.IsQueued, "The ghost is translucent, marked queued");
            Assert.AreSame(game.BoardView(p2).transform, sideGhost.transform.parent, "One for a whole side sits on that battlefield...");
            var corners = new Vector3[4];
            ((RectTransform)game.BoardView(p2).transform).GetWorldCorners(corners);
            Assert.AreEqual(corners[0].y, Height(sideGhost), 0.01f, "...on its edge facing the other side, rather than in a slot");
            Assert.Less(sideGhost.transform.lossyScale.x, game.CardIn(ox.Slot).transform.lossyScale.x, "...small");
            bool Glows(IPlayer side) => game.BoardView(side).transform.Cast<Transform>().Any(t => t.name.StartsWith("SpellAim_"));
            Assert.IsTrue(Glows(p2), "The whole battlefield it plays on glows while it's queued");
            Assert.IsFalse(Glows(p1));
            SceneGame.Click(sideGhost);
            yield return null;
            Assert.IsFalse(Glows(p2), "Taken back: no more glow");
            Assert.IsNull(game.SpellGhost(storm), "...no more ghost");
            Assert.IsNotNull(Shown(storm), "...and the card is back in the hand");

            var pup = game.GiveCard(p1, game.Cards.Creature("Test Pup", 1, 1));
            yield return null;
            Color MatOf(IPlayer side) => game.BoardView(side).GetComponent<Image>().color;
            bool IsLit(BoardSlot slot) => game.BoardView(slot.Owner).FindSlotView(slot).GetComponent<Image>().color.a > 0f;
            GameObject SlotObject(BoardSlot slot) => game.BoardView(slot.Owner).FindSlotView(slot).gameObject;
            void Enter(PointerEventData pointer, GameObject over) => ExecuteEvents.Execute(over, pointer, ExecuteEvents.pointerEnterHandler);
            void Exit(PointerEventData pointer, GameObject over) => ExecuteEvents.Execute(over, pointer, ExecuteEvents.pointerExitHandler);
            var cases = new (ICard card, bool litMat1, bool litMat2, bool litSlot1, bool litSlot2)[] {
                (storm, false, true, false, false), // Hits every enemy creature: the enemy battlefield, no slot
                (zap, false, false, true, true),    // Aimed: any slot, never a whole battlefield
                (pup, false, false, true, false),   // Summoned: a slot of its own side only
            };
            foreach (var (card, litMat1, litMat2, litSlot1, litSlot2) in cases) {
                var drag = SceneGame.BeginDrag(Shown(card));
                foreach (var (side, litMat, slot, litSlot) in new[] { (p1, litMat1, p1.Battlefield[2], litSlot1), (p2, litMat2, p2.Battlefield[2], litSlot2) }) {
                    var mat = game.BoardView(side).gameObject;
                    Enter(drag, mat);
                    Assert.AreEqual(litMat ? UITheme.MatValid : UITheme.Mat, MatOf(side), $"{card.Name} over {side.Name}'s battlefield");
                    Enter(drag, SlotObject(slot));
                    Assert.AreEqual(litSlot, IsLit(slot), $"{card.Name} over {slot}");
                    Exit(drag, SlotObject(slot));
                    Exit(drag, mat);
                }
                SceneGame.EndDrag(Shown(card), drag);
                yield return null;
            }
            var quakeDrag = SceneGame.BeginDrag(Shown(quake));
            Enter(quakeDrag, game.PlayArea);
            Assert.IsTrue(MatOf(p1) == UITheme.MatValid && MatOf(p2) == UITheme.MatValid, "A spell for either side lights both battlefields up over the table");
            Exit(quakeDrag, game.PlayArea);
            Assert.IsTrue(MatOf(p1) == UITheme.Mat && MatOf(p2) == UITheme.Mat);
            SceneGame.EndDrag(Shown(quake), quakeDrag);
            yield return null;
            var idle = new PointerEventData(EventSystem.current);
            Enter(idle, game.BoardView(p1).gameObject);
            Enter(idle, SlotObject(p1.Battlefield[2]));
            Assert.AreEqual(UITheme.Mat, MatOf(p1), "Hovering a battlefield without a card doesn't light it up");
            Assert.IsFalse(IsLit(p1.Battlefield[2]), "...nor an empty slot");
            Exit(idle, SlotObject(p1.Battlefield[2]));
            Exit(idle, game.BoardView(p1).gameObject);

            Assert.IsTrue(SceneGame.DragAndDrop(Shown(storm), game.PlayArea));
            yield return null;
            Assert.IsNull(game.SpellGhost(storm), "Between the battlefields: not for a spell that hits one side only");
            Assert.IsTrue(SceneGame.DragAndDrop(Shown(storm), game.BoardView(p1).gameObject));
            yield return null;
            Assert.IsNull(game.SpellGhost(storm), "A spell that only hits enemy creatures can't be played on your own battlefield");
            Assert.IsTrue(SceneGame.DragAndDrop(Shown(storm), game.BoardView(p2).gameObject));
            yield return null;
            Assert.IsNotNull(game.SpellGhost(storm), "...it's played by dropping it anywhere on the enemy's");
            SceneGame.Click(game.SpellGhost(storm));
            yield return null;
            Assert.IsTrue(SceneGame.DragAndDrop(Shown(quake), game.PlayArea));
            yield return null;
            Assert.IsNotNull(game.SpellGhost(quake), "A spell for either side is played by dropping it anywhere on the table, even between the battlefields");
            SceneGame.Click(game.SpellGhost(quake));
            yield return null;

            game.Play(zap, p1, aimedAt);
            yield return null;
            var slotView = game.BoardView(p2).FindSlotView(aimedAt);
            var zapGhost = game.SpellGhost(zap);
            Assert.IsNotNull(zapGhost, "An aimed spell's ghost...");
            Assert.AreEqual(X(slotView), X(zapGhost), 0.01f, "...is at the slot it's aimed at");
            Assert.AreEqual(corners[0].y, Height(zapGhost), 0.01f, "...small on its edge, the creature there staying in sight");
            game.Play(bolt, p1, aimedAt);
            yield return null;
            var boltGhost = game.SpellGhost(bolt);
            Assert.AreEqual(X(slotView), X(boltGhost), 0.01f, "A second spell there: the newest takes the middle...");
            Assert.Less(X(zapGhost), X(slotView) - 0.01f, "...and the older one steps left");
            Assert.Greater(boltGhost.transform.GetSiblingIndex(), zapGhost.transform.GetSiblingIndex(), "...under the newest");
            SceneGame.Click(boltGhost);
            yield return null;
            Assert.AreEqual(X(slotView), X(zapGhost), 0.01f, "Taken back: the other one is in the middle again");
            SceneGame.Click(zapGhost);
            yield return null;
            game.Play(zap, p1, p2.Battlefield[3]);
            yield return null;
            zapGhost = game.SpellGhost(zap);
            Assert.AreSame(game.BoardView(p2).FindSlotView(p2.Battlefield[3]).transform, zapGhost.transform.parent, "In an empty slot it's in the slot...");
            Assert.AreEqual(1f, zapGhost.transform.localScale.x, 0.01f, "...at full size");
            SceneGame.Click(zapGhost);
            yield return null;
            Assert.IsNull(game.SpellGhost(zap), "Taking the spell back removes its ghost");

            var takeBacks = new (string how, System.Action<CardController> takeBack)[] {
                ("Clicking", card => SceneGame.Click(card)),
                ("Right-clicking", card => SceneGame.Click(card, PointerEventData.InputButton.Right)),
                ("Dragging it where it can't be played", card => Assert.IsTrue(SceneGame.DragAndDrop(card, null), "A spell's ghost can be dragged")),
            };
            foreach (var (how, takeBack) in takeBacks) {
                game.Play(bolt, p1, p2.Battlefield[0]);
                yield return null;
                Assert.IsNull(Shown(bolt));
                takeBack(game.SpellGhost(bolt));
                yield return null;
                Assert.IsNotNull(Shown(bolt), $"{how} the ghost: the spell is back in the hand");
                Assert.IsFalse(Shown(bolt).IsQueued, $"{how}: not queued");
                Assert.IsNull(game.SpellGhost(bolt), $"{how}: no ghost");
                Assert.IsFalse(game.Queue.GetPendingActions().OfType<PlayCardAction>().Any(), $"{how}: its play is no longer queued");
            }

            SceneGame.Hover(Shown(bolt));
            yield return null;
            Assert.IsNull(SceneGame.HoverPreview(), "Not at once: the pointer may only be passing over the card");
            yield return SceneGame.PreviewShows();
            var preview = SceneGame.HoverPreview();
            Assert.IsNotNull(preview, "Hovering a card in the hand shows it enlarged, once the pointer rests on it");
            Assert.AreSame(Shown(bolt), preview, "In your hand the card itself rises out of the fan, instead of a copy");
            Assert.AreEqual(HandUI.RaisedScale, preview.transform.localScale.x, 0.01f, "...enlarged...");
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, preview.transform.localEulerAngles.z), 0.01f, "...and straight");
            Assert.Greater(game.HandCards(p1).First(c => c != preview).transform.localScale.x, 1.5f, "The others are big too, Slay the Spire size");
            Assert.AreSame(bolt, preview.GetLinkedCardInstance());
            Assert.IsTrue(preview.IsSpellShaped, "A spell is shaped as one: rounded, with an oval art window");
            Assert.IsFalse(game.HandCards(p1).First(c => c.GetBaseCardData() is CreatureData).IsSpellShaped, "...a creature isn't");
            Tooltip.Existing.HideTooltip();
            game.Play(bolt, p1, p2.Battlefield[0]);
            yield return null;
            SceneGame.Hover(game.SpellGhost(bolt));
            yield return SceneGame.PreviewShows();
            Assert.AreSame(bolt, SceneGame.HoverPreview()?.GetLinkedCardInstance(), "Its ghost on the board shows it enlarged too");

            // Keywords: hovering one on the enlarged card explains it, and its explanation's keywords can be followed in turn
            Tooltip.Existing.HideTooltip();
            SceneGame.Hover(Shown(quake));
            yield return SceneGame.PreviewShows();
            var tooltip = Tooltip.Existing;
            Assert.IsTrue(tooltip.TrackKeywords(SceneGame.KeywordPoint(tooltip.Preview.RulesText, "Exhausted")));
            CollectionAssert.AreEqual(new[] { "Exhausted" }, tooltip.OpenKeywords, "Hovering Exhausted opens what it means");
            tooltip.TrackKeywords(SceneGame.KeywordPoint(tooltip.KeywordText("Exhausted"), "Exposed"));
            CollectionAssert.AreEqual(new[] { "Exhausted", "Exposed" }, tooltip.OpenKeywords, "...and Exposed, from inside that explanation");
            tooltip.TrackKeywords(SceneGame.KeywordPoint(tooltip.KeywordText("Exhausted"), "Day"));
            CollectionAssert.AreEqual(new[] { "Exhausted", "Day" }, tooltip.OpenKeywords, "Another keyword of the same explanation replaces it");
            tooltip.TrackKeywords(SceneGame.KeywordPoint(tooltip.Preview.RulesText, "Exhausted"));
            CollectionAssert.AreEqual(new[] { "Exhausted", "Day" }, tooltip.OpenKeywords, "Passing back over the same keyword keeps everything open");
            var onCard = RectTransformUtility.WorldToScreenPoint(CanvasUtility.GetEventCamera(tooltip.Preview), tooltip.Preview.transform.position);
            Assert.IsTrue(tooltip.PointerInArea(onCard), "The area includes the enlarged card...");
            tooltip.TrackKeywords(SceneGame.KeywordPoint(tooltip.Preview.RulesText, "Random"));
            CollectionAssert.AreEqual(new[] { "Random" }, tooltip.OpenKeywords, "...and another keyword on the card starts a new chain");
            Assert.IsFalse(tooltip.ClickAt(SceneGame.KeywordPoint(tooltip.KeywordText("Random"), "Heat Signature")), "A click on an explanation keeps it open");
            Assert.IsTrue(tooltip.ClickAt(new Vector2(2f, 2f)), "A click anywhere else closes it all");
            CollectionAssert.IsEmpty(tooltip.OpenKeywords);

            yield return game.EndTurn();
            Assert.AreEqual(3, ox.Health, "Only the spell left queued is cast");
            Assert.IsNull(game.SpellGhost(bolt), "Cast: its ghost is gone");
            var allOfP1sCards = p1.Deck.GetDeckPreview().Concat(p1.Deck.GetDiscardPilePreview()).Concat(p1.Hand);
            Assert.AreEqual(1, allOfP1sCards.Count(c => c.Name == "Test Bolt"), "The cast spell is in player 1's cards exactly once");
            game.AssertViewsMatchRules();
        }
    }
}
