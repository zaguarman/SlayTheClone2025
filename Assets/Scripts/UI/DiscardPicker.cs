using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zeitgeist {
    /// <summary>
    /// A chosen discard cost on screen: when the player at the screen plays a card that's paid with cards picked by hand
    /// (Headhunter, NDA, Dead Drop), the cards it could take are shown and picked one at a time; once all are picked they pay
    /// for it instead of the ones set aside when it was played (see <see cref="DiscardCost.Repay"/>). Closed early, the cards
    /// already set aside stay.
    /// </summary>
    public class DiscardPicker : MonoBehaviour {
        private IGameManager gameManager;
        private IGameMediator gameMediator;
        private DeckViewUI deckView;
        private readonly HashSet<PlayCardAction> asked = new HashSet<PlayCardAction>();

        /// <summary>The play whose cost is being picked, while the window is open for it.</summary>
        public PlayCardAction Asking { get; private set; }

        public void Initialize(IGameManager manager, IGameMediator mediator, DeckViewUI view) {
            gameManager = manager;
            gameMediator = mediator;
            deckView = view;
            mediator.ActionsQueueChanged += OnQueueChanged;
        }

        // A new play with a chosen discard cost by a seat the screen plays: ask which cards pay for it
        private void OnQueueChanged() {
            if (deckView == null || gameManager?.ActionsQueue == null || Asking != null) return;
            var play = gameManager.ActionsQueue.GetPendingActions().OfType<PlayCardAction>()
                .FirstOrDefault(p => !asked.Contains(p) && DiscardCost.IsChosen(p.GetCard()) && p.Paid.Count > 0
                                     && FogOfWar.CanControl(p.GetOwner()) && !MatchSetup.PlayedByAI(p.GetOwner()));
            if (play == null) return;
            asked.Add(play);
            Asking = play;
            AskNext(play, new List<ICard>());
        }

        private void AskNext(PlayCardAction play, List<ICard> picked) {
            var owner = play.GetOwner();
            int cost = DiscardCost.Of(play.GetCard());
            if (picked.Count >= cost) {
                DiscardCost.Repay(play, picked, gameManager.ActionsQueue);
                Asking = null;
                gameMediator.NotifyHandStateChanged(owner);
                return;
            }
            var choices = DiscardCost.Payable(play.GetCard(), owner, gameManager.ActionsQueue).Concat(play.Paid).Where(c => !picked.Contains(c)).ToList();
            deckView.ShowPick(owner, $"{play.GetCard().Name}: give up a card ({picked.Count + 1} of {cost})", choices,
                card => AskNext(play, picked.Concat(new[] { card }).ToList()), () => Asking = null, "Click one to give it up");
        }

        private void OnDestroy() {
            if (gameMediator != null) gameMediator.ActionsQueueChanged -= OnQueueChanged;
        }
    }
}
