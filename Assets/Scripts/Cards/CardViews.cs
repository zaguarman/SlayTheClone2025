using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Makes card faces. Holds what every face needs: the card prefab and the running game.
    /// Built once at startup and handed to the views that show cards (hands, boards, the deck view, resolution playback).
    /// </summary>
    public class CardViews {
        private readonly CardController prefab;

        public IGameMediator Mediator { get; }
        public IGameManager GameManager { get; }

        public CardViews(CardController prefab, IGameMediator mediator, IGameManager gameManager) {
            this.prefab = prefab != null ? prefab : throw new System.ArgumentNullException(nameof(prefab));
            Mediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
            GameManager = gameManager ?? throw new System.ArgumentNullException(nameof(gameManager));
        }

        /// <summary>The face for a card instance: its definition and, for a creature on the board, its live stats.</summary>
        public CardController Create(ICard card, IPlayer owner, Transform parent) {
            if (card == null || parent == null) return null;
            if (card.SourceData == null) {
                LogError($"{card.Name} has no card definition, so it can't be shown. Cards must be made by CardFactory.CreateCard.", LogTag.Cards | LogTag.UI);
                return null;
            }

            var controller = Object.Instantiate(prefab, parent);
            controller.Setup(card, owner, this);
            Log($"Created card controller for {card.Name}", LogTag.Cards | LogTag.Initialization);
            return controller;
        }
    }
}
