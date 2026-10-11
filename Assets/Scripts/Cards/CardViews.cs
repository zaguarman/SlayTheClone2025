using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Makes card faces. Holds what every face needs: the card prefab, each player's card color and the running game.
    /// Built once at startup and handed to the views that show cards (hands, boards, the deck view, resolution playback).
    /// </summary>
    public class CardViews {
        private readonly CardController prefab;
        private readonly Color player1Color;
        private readonly Color player2Color;

        public IGameMediator Mediator { get; }
        public IGameManager GameManager { get; }

        public CardViews(CardController prefab, Color player1Color, Color player2Color, IGameMediator mediator, IGameManager gameManager) {
            this.prefab = prefab != null ? prefab : throw new System.ArgumentNullException(nameof(prefab));
            this.player1Color = player1Color;
            this.player2Color = player2Color;
            Mediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
            GameManager = gameManager ?? throw new System.ArgumentNullException(nameof(gameManager));
        }

        /// <summary>The border color that shows who owns a card.</summary>
        public Color GetColor(IPlayer owner) => owner.IsPlayer1 ? player1Color : player2Color;

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
