using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using static SlayTheClone.Enums;
using Object = UnityEngine.Object;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The real game scene, started the way a player gets it, for tests that drive the whole game: rules, turn flow,
    /// resolution playback and the views. Time runs faster, frames aren't capped and plain logs are muted, so the playback
    /// beats (which last at least a frame each) don't slow the tests down.
    /// Moves are made the way the views make them (queueing plays, declaring attacks) and turns end by clicking End Turn.
    /// </summary>
    public sealed class SceneGame : IDisposable {
        public const string SceneName = "InitialTestScene";
        private const float TimeScale = 100f; // Unity's maximum
        private int savedVSyncCount;
        private int savedTargetFrameRate;
        private bool framesUncapped;
        private static readonly Regex StatsLine = new Regex(@"^(\d+)/(\d+)/(\d+)(?:<color=[^>]*>\+(\d+)</color>)?$");

        public TestCards Cards { get; } = new TestCards();
        public GameManager Manager { get; private set; }
        public GameReferences References { get; private set; }
        public Player Player1 => Manager.Player1;
        public Player Player2 => Manager.Player2;
        public IEnumerable<Player> Players => new[] { Player1, Player2 };
        public ITurnManager Turns => Manager.TurnManager;
        public IActionsQueue Queue => Manager.ActionsQueue;

        /// <summary>
        /// Loads the scene on its own, with the decks set on it, and waits for the game to start;
        /// the seed makes the shuffles the same every run.
        /// </summary>
        public IEnumerator Load(int seed) {
            MatchSetup.Clear();
            yield return Start(seed, () => SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single));
        }

        /// <summary>Loads the scene for a match as the main menu would have chosen it.</summary>
        public IEnumerator Load(int seed, DeckScriptableObject playerDeck, string opponentName, DeckScriptableObject opponentDeck) {
            MatchSetup.Choose(playerDeck, opponentName, opponentDeck);
            yield return Start(seed, () => SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single));
        }

        /// <summary>Runs whatever loads the game scene (the main menu's Start button, say) and waits for the game to start.</summary>
        public IEnumerator Start(int seed, Action loadGameScene) {
            DebugLogger.MuteInfo = true; // The game logs thousands of lines a turn; writing them is what slows playback down most
            // Set after the scene's objects wake up and before GameBootstrap starts the game
            bool loaded = false; // The game scene being replaced may still look started
            void SetSeed(Scene scene, LoadSceneMode mode) {
                if (scene.name != SceneName) return;
                var manager = Object.FindFirstObjectByType<GameManager>();
                if (manager != null) SetField(manager, "randomSeed", seed);
                loaded = true;
            }

            SceneManager.sceneLoaded += SetSeed;
            try {
                loadGameScene();
                yield return Wait.For(() => loaded && HasStarted(), "the game to start");
            }
            finally {
                SceneManager.sceneLoaded -= SetSeed;
            }
            Manager = Object.FindFirstObjectByType<GameManager>();
            References = Object.FindFirstObjectByType<GameReferences>();
            Time.timeScale = TimeScale; // After the scene's pause menu has reset it
            UncapFrames();
            yield return null;
        }

        private static bool HasStarted() {
            var ui = Object.FindFirstObjectByType<GameUI>();
            return ui != null && ui.IsInitialized && Object.FindFirstObjectByType<ResolutionPresenter>() != null;
        }

        private void UncapFrames() {
            if (framesUncapped) return;
            savedVSyncCount = QualitySettings.vSyncCount;
            savedTargetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            framesUncapped = true;
        }

        public void Dispose() {
            Time.timeScale = 1f;
            DebugLogger.MuteInfo = false;
            if (framesUncapped) {
                QualitySettings.vSyncCount = savedVSyncCount;
                Application.targetFrameRate = savedTargetFrameRate;
                framesUncapped = false;
            }
            MatchSetup.Clear(); // A chosen match would otherwise carry over to the next test
            Cards.Dispose();
        }

        /// <summary>Every card the player owns (deck, hand, board and discard pile) are the deck's cards, copies included.</summary>
        public static void AssertPlays(Player player, DeckScriptableObject deck) {
            var owned = player.Deck.GetDeckPreview().Concat(player.Hand).Concat(player.Deck.GetDiscardPilePreview())
                .Concat(player.Battlefield.Where(s => s.IsOccupied()).Select(s => (ICard)s.OccupyingCreature));
            CollectionAssert.AreEquivalent(deck.Cards.Select(c => c.cardId), owned.Select(c => c.CardId),
                $"{player.Name} isn't playing {deck.GetDeckName()}");
        }

        #region Playing
        /// <summary>Puts a new card in a player's hand.</summary>
        public ICard GiveCard(Player player, CardData data) {
            var card = CardFactory.CreateCard(data);
            player.AddToHand(card);
            CollectionAssert.Contains(player.Hand, card, $"{player.Name}'s hand is full");
            return card;
        }

        /// <summary>Puts a new creature straight onto a board, as a summon effect would.</summary>
        public ICreature Summon(Player owner, CreatureData data, int slot) {
            var creature = (ICreature)CardFactory.CreateCard(data);
            Queue.AddAction(new SummonCreatureAction(creature, owner, owner.Battlefield[slot], fromDeck: true));
            Queue.ResolveActions();
            return creature;
        }

        /// <summary>What dropping a card from the hand on a slot does: the play is queued until the turn ends.</summary>
        public void Play(ICard card, Player caster, BoardSlot slot) =>
            Queue.AddAction(new PlayCardAction(card, caster, EffectResolver.AimAt(card, caster, slot, Queue)));

        /// <summary>Every creature in the hand goes to an empty slot of its player's board.</summary>
        public void PlayCreatures(Player player) {
            var freeSlots = new Queue<BoardSlot>(player.Battlefield.Where(s => !s.IsOccupied()));
            foreach (var creature in player.Hand.OfType<ICreature>().ToList()) {
                if (freeSlots.Count == 0) break;
                Play(creature, player, freeSlots.Dequeue());
            }
        }

        /// <summary>What dragging a creature onto an enemy slot does.</summary>
        public void Attack(ICreature attacker, BoardSlot target) => Manager.CombatHandler.HandleCreatureCombat(attacker, target);

        /// <summary>Clicks End Turn and waits for the turn, and its playback, to finish.</summary>
        public IEnumerator EndTurn() {
            int turn = Turns.TurnNumber;
            References.GetResolveActionsButton().onClick.Invoke();
            yield return Wait.For(() => !Turns.IsResolving, "the turn to resolve");
            Assert.AreEqual(turn + 1, Turns.TurnNumber, "Clicking End Turn didn't end the turn");
            yield return null; // Cards the views replaced are destroyed at the end of the frame
        }
        #endregion

        #region Views
        public HandUI HandView(IPlayer player) => ViewsOf(player).HandUI;

        public BattlefieldUI BoardView(IPlayer player) => ViewsOf(player).BattlefieldUI;

        /// <summary>The Queue button and the panel it opens.</summary>
        public QueuePanel QueuePanel => Object.FindFirstObjectByType<GameUI>().QueuePanel;

        private GameReferences.PlayerUIReferences ViewsOf(IPlayer player) =>
            player.IsPlayer1 ? References.Player1References : References.Player2References;

        /// <summary>The cards the hand shows, left to right.</summary>
        public List<CardController> HandCards(IPlayer player) =>
            HandView(player).transform.Cast<Transform>().Select(t => t.GetComponent<CardController>()).Where(c => c != null).ToList();

        /// <summary>The card shown in a board slot, if any.</summary>
        public CardController CardIn(BoardSlot slot) => BoardView(slot.Owner).FindSlotView(slot).OccupyingCard;

        /// <summary>The translucent card of the creature queued to be summoned in a slot.</summary>
        public CardController GhostIn(BoardSlot slot) {
            var ghost = BoardView(slot.Owner).FindSlotView(slot).transform.Cast<Transform>()
                .FirstOrDefault(t => t.name.StartsWith("QueuedGhost_"))?.GetComponent<CardController>();
            Assert.IsNotNull(ghost, $"{slot} shows no queued creature");
            return ghost;
        }

        /// <summary>The arrows a board shows for its player's queued actions, not counting ones fading out.</summary>
        public List<ArrowIndicator> QueuedArrows(IPlayer player) =>
            BoardView(player).GetComponentsInChildren<ArrowIndicator>()
                .Where(a => a.IsVisible() && !a.IsFadingOut).ToList();

        /// <summary>Whether a slot shows the translucent preview of a creature queued to be summoned there.</summary>
        public bool HasGhost(BoardSlot slot) =>
            BoardView(slot.Owner).FindSlotView(slot).transform.Cast<Transform>().Any(t => t.name.StartsWith("QueuedGhost_"));

        /// <summary>
        /// Moves the pointer onto a visible part of the card, the way the event system does: what's on top there
        /// gets the enter event, then each of its parents. Returns everything under the pointer, top first.
        /// </summary>
        public static List<RaycastResult> Hover(CardController card) {
            var corners = new Vector3[4];
            ((RectTransform)card.transform).GetWorldCorners(corners);
            var eventCamera = CanvasUtility.GetEventCamera(card);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);

            // The center first; cards in a hand overlap, so other points of the card may be the visible ones
            var points = new[] { 0.5f, 0.2f, 0.8f }.SelectMany(x => new[] { 0.5f, 0.2f, 0.8f }.Select(y => min + Vector2.Scale(max - min, new Vector2(x, y))));
            foreach (var point in points) {
                var eventData = new PointerEventData(EventSystem.current) { position = point };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, hits);
                if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<CardController>() != card) continue;

                eventData.pointerEnter = hits[0].gameObject;
                for (var t = hits[0].gameObject.transform; t != null; t = t.parent) {
                    ExecuteEvents.Execute(t.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
                }
                return hits;
            }
            Assert.Fail($"{card.name} can't be pointed at: it's covered, or doesn't take the pointer");
            return null;
        }

        /// <summary>Clicks a visible part of the card: the click goes to whatever handles it under the pointer.</summary>
        public static void Click(CardController card, PointerEventData.InputButton button = PointerEventData.InputButton.Left) {
            var hits = Hover(card);
            var eventData = new PointerEventData(EventSystem.current) { position = hits[0].screenPosition, button = button };
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>
        /// Drags the card the way the event system does: begin drag, a drop on <paramref name="dropOn"/> (null: released
        /// where nothing takes it), then end drag. Returns false if the card refused to be dragged.
        /// </summary>
        public static bool DragAndDrop(CardController card, GameObject dropOn) {
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerDrag = card.gameObject };
            ExecuteEvents.Execute(card.gameObject, eventData, ExecuteEvents.beginDragHandler);
            if (eventData.pointerDrag == null) return false;

            if (dropOn != null) ExecuteEvents.ExecuteHierarchy(dropOn, eventData, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(card.gameObject, eventData, ExecuteEvents.endDragHandler);
            return true;
        }

        /// <summary>Starts dragging the card the way the event system does; finish with <see cref="EndDrag"/>.</summary>
        public static PointerEventData BeginDrag(CardController card) {
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerDrag = card.gameObject };
            ExecuteEvents.Execute(card.gameObject, eventData, ExecuteEvents.beginDragHandler);
            return eventData;
        }

        public static void EndDrag(CardController card, PointerEventData eventData) =>
            ExecuteEvents.Execute(card.gameObject, eventData, ExecuteEvents.endDragHandler);

        /// <summary>The move hint a slot shows while one of its side's creatures is dragged.</summary>
        public (MoveKind kind, bool blocked)? MoveHintIn(BoardSlot slot) => BoardView(slot.Owner).FindSlotView(slot).MoveHint;

        /// <summary>Where a keyword is on screen in a text (the enlarged card's, or a keyword explanation's).</summary>
        public static Vector2 KeywordPoint(TMP_Text text, string key) {
            text.ForceMeshUpdate();
            var link = text.textInfo.linkInfo.Take(text.textInfo.linkCount).FirstOrDefault(l => l.GetLinkID() == key);
            Assert.AreEqual(key, link.GetLinkID(), $"'{text.text}' has no keyword {key}");
            var character = text.textInfo.characterInfo[link.linkTextfirstCharacterIndex];
            return text.transform.TransformPoint((character.bottomLeft + character.topRight) / 2f);
        }

        /// <summary>Long enough for a hovered card to show enlarged (it waits for the pointer to rest on it).</summary>
        public static WaitForSecondsRealtime PreviewShows() => new WaitForSecondsRealtime(Tooltip.HoverDelay + 0.05f);

        /// <summary>The enlarged copy shown while a card is hovered, if any.</summary>
        public static CardController HoverPreview() {
            var tooltip = Tooltip.Existing;
            return tooltip != null && tooltip.gameObject.activeSelf ? tooltip.GetComponentInChildren<CardController>() : null;
        }

        /// <summary>
        /// Everything on screen shows the game as the rules have it: health, hands (minus creatures queued to be played),
        /// each creature's card with its live stats, queued summons as ghosts and nothing else, and the turn number.
        /// </summary>
        public void AssertViewsMatchRules() {
            var pending = Queue.GetPendingActions();
            foreach (var player in Players) {
                Assert.AreEqual($"{player.Health}/{Player.MaxHealth}", ViewsOf(player).HealthText.text, $"{player.Name}'s health text");

                var queuedCreatures = pending.OfType<PlayCardAction>().Where(a => a.GetOwner() == player && a.GetCard() is ICreature).Select(a => a.GetCard());
                CollectionAssert.AreEquivalent(player.Hand.Except(queuedCreatures), HandCards(player).Select(c => c.GetLinkedCardInstance()),
                    $"{player.Name}'s hand view shows different cards than the hand");

                foreach (var slot in player.Battlefield) {
                    var card = CardIn(slot);
                    var creature = slot.OccupyingCreature;
                    if (creature == null) {
                        Assert.IsNull(card, $"{slot} is empty but shows {card?.GetBaseCardData()?.cardName}");
                    }
                    else {
                        Assert.IsNotNull(card, $"{creature.Name} in {slot} has no card on screen");
                        Assert.AreSame(creature, card.GetLinkedCreature(), $"{slot} shows another creature's card");
                        AssertCardShows(card, creature);
                    }

                    bool queuedHere = pending.Any(a => (a is PlayCardAction play && play.GetCard() is ICreature && play.GetTarget() == slot)
                        || (a is SummonCreatureAction summon && summon.GetTargetSlot() == slot));
                    Assert.AreEqual(queuedHere, HasGhost(slot), queuedHere ? $"{slot} should show a queued creature" : $"{slot} shows a ghost of nothing queued");
                }
            }

            var turnText = (TMP_Text)GetField(Object.FindFirstObjectByType<TurnUI>(), "turnText");
            Assert.AreEqual($"Turn {Turns.TurnNumber}", turnText.text);
        }

        /// <summary>The card's attack/speed/health line, armor and turns-until-operative pip match the creature.</summary>
        public static void AssertCardShows(CardController card, ICreature creature) {
            string stats = ((TMP_Text)GetField(card, "statsText")).text;
            var match = StatsLine.Match(stats);
            Assert.IsTrue(match.Success, $"{creature.Name}'s card shows an unreadable stats line '{stats}'");

            Assert.AreEqual(creature.Attack, int.Parse(match.Groups[1].Value), $"{creature.Name}'s card shows the wrong attack ({stats})");
            Assert.AreEqual(creature.Speed, int.Parse(match.Groups[2].Value), $"{creature.Name}'s card shows the wrong speed ({stats})");
            Assert.AreEqual(creature.Health, int.Parse(match.Groups[3].Value), $"{creature.Name}'s card shows the wrong health ({stats})");
            int armor = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
            Assert.AreEqual(creature.CurrentArmorPool, armor, $"{creature.Name}'s card shows the wrong armor ({stats})");

            string ready = ((TMP_Text)GetField(card, "readyText")).text;
            Assert.AreEqual(creature.TurnsUntilOperative.ToString(), ready, $"{creature.Name}'s card shows the wrong turns until operative");
        }
        #endregion

        // The views keep their text fields private; tests read them by name
        private static object GetField(object target, string name) => FindField(target, name).GetValue(target);

        private static void SetField(object target, string name, object value) => FindField(target, name).SetValue(target, value);

        private static FieldInfo FindField(object target, string name) {
            Assert.IsNotNull(target, $"Nothing to read {name} from");
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{target.GetType().Name} has no field {name}; update {nameof(SceneGame)}");
            return field;
        }
    }
}
