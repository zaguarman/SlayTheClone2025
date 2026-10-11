using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The real game scene, started the way a player gets it, for tests that drive the whole game: rules, turn flow,
    /// resolution playback and the views. Time runs faster so the playback beats don't slow the tests down.
    /// Moves are made the way the views make them (queueing plays, declaring attacks) and turns end by clicking End Turn.
    /// </summary>
    public sealed class SceneGame : IDisposable {
        public const string SceneName = "InitialTestScene";
        private const float TimeScale = 20f;
        private static readonly Regex StatsLine = new Regex(@"^(\d+)/(\d+)/(\d+)(?:<color=[^>]*>\+(\d+)</color>)?$");

        public TestCards Cards { get; } = new TestCards();
        public GameManager Manager { get; private set; }
        public GameReferences References { get; private set; }
        public Player Player1 => Manager.Player1;
        public Player Player2 => Manager.Player2;
        public IEnumerable<Player> Players => new[] { Player1, Player2 };
        public ITurnManager Turns => Manager.TurnManager;
        public IActionsQueue Queue => Manager.ActionsQueue;

        /// <summary>Loads the scene and waits for the game to start; the seed makes the shuffles the same every run.</summary>
        public IEnumerator Load(int seed) {
            // Set after the scene's objects wake up and before GameBootstrap starts the game
            void SetSeed(Scene scene, LoadSceneMode mode) {
                var manager = Object.FindFirstObjectByType<GameManager>();
                if (manager != null) SetField(manager, "randomSeed", seed);
            }

            SceneManager.sceneLoaded += SetSeed;
            try {
                yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            }
            finally {
                SceneManager.sceneLoaded -= SetSeed;
            }

            yield return Wait.For(HasStarted, "the game to start");
            Manager = Object.FindFirstObjectByType<GameManager>();
            References = Object.FindFirstObjectByType<GameReferences>();
            Time.timeScale = TimeScale; // After the scene's pause menu has reset it
            yield return null;
        }

        private static bool HasStarted() {
            var ui = Object.FindFirstObjectByType<GameUI>();
            return ui != null && ui.IsInitialized && Object.FindFirstObjectByType<ResolutionPresenter>() != null;
        }

        public void Dispose() {
            Time.timeScale = 1f;
            Cards.Dispose();
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
        public void Play(ICard card, Player caster, BoardSlot slot) => Queue.AddAction(new PlayCardAction(card, caster, slot));

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

        private GameReferences.PlayerUIReferences ViewsOf(IPlayer player) =>
            player.IsPlayer1 ? References.Player1References : References.Player2References;

        /// <summary>The cards the hand shows, left to right.</summary>
        public List<CardController> HandCards(IPlayer player) =>
            HandView(player).transform.Cast<Transform>().Select(t => t.GetComponent<CardController>()).Where(c => c != null).ToList();

        /// <summary>The card shown in a board slot, if any.</summary>
        public CardController CardIn(BoardSlot slot) => BoardView(slot.Owner).FindSlotView(slot).OccupyingCard;

        /// <summary>Whether a slot shows the translucent preview of a creature queued to be summoned there.</summary>
        public bool HasGhost(BoardSlot slot) =>
            BoardView(slot.Owner).FindSlotView(slot).transform.Cast<Transform>().Any(t => t.name.StartsWith("QueuedGhost_"));

        /// <summary>
        /// Everything on screen shows the game as the rules have it: health, hands (minus creatures queued to be played),
        /// each creature's card with its live stats, queued summons as ghosts and nothing else, and the turn number.
        /// </summary>
        public void AssertViewsMatchRules() {
            var pending = Queue.GetPendingActions();
            foreach (var player in Players) {
                Assert.AreEqual($"Health: {player.Health}", ViewsOf(player).HealthText.text, $"{player.Name}'s health text");

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
            Assert.AreEqual($"Turn: {Turns.TurnNumber}", turnText.text);
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
