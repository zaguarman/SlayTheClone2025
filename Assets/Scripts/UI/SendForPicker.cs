using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Send For on screen: when the player at the screen plays a card that sends for one (Multipass, Omen Reading...), the
    /// deck window opens on what it can fetch; clicking one picks it (see <see cref="IPlayer.SendForPicks"/>). Closed without
    /// a pick, the best one is sent for when it resolves.
    /// </summary>
    public class SendForPicker : MonoBehaviour {
        private IGameManager gameManager;
        private IGameMediator gameMediator;
        private DeckViewUI deckView;
        private readonly HashSet<PlayCardAction> asked = new HashSet<PlayCardAction>();

        /// <summary>The play whose pick is being made, while the window is open for it.</summary>
        public PlayCardAction Asking { get; private set; }

        public void Initialize(IGameManager manager, IGameMediator mediator, DeckViewUI view) {
            gameManager = manager;
            gameMediator = mediator;
            deckView = view;
            mediator.ActionsQueueChanged += OnQueueChanged;
        }

        // A new play of a Send For card by a seat the screen plays: ask which card
        private void OnQueueChanged() {
            if (deckView == null || gameManager?.ActionsQueue == null || Asking != null) return;
            var play = gameManager.ActionsQueue.GetPendingActions().OfType<PlayCardAction>()
                .FirstOrDefault(p => !asked.Contains(p) && SendFor.Of(p.GetCard()) != null && FogOfWar.CanControl(p.GetOwner()) && !MatchSetup.PlayedByAI(p.GetOwner()));
            if (play == null) return;
            asked.Add(play);
            var owner = play.GetOwner();
            var sending = SendFor.Of(play.GetCard());
            var choices = SendFor.Choices(owner, sending);
            if (choices.Count == 0) return;
            Asking = play;
            deckView.ShowPick(owner, $"{play.GetCard().Name}: send for a card", choices, picked => {
                owner.SendForPicks[play.GetCard()] = picked;
                Asking = null;
            }, () => Asking = null);
        }

        private void OnDestroy() {
            if (gameMediator != null) gameMediator.ActionsQueueChanged -= OnQueueChanged;
        }
    }
}
