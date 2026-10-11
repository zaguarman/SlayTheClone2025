using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class HandUI : CardContainer {
        private const float CastStagger = 0.2f;

        // Cards of this player with a pending PlayCardAction
        private readonly HashSet<ICard> queuedCards = new HashSet<ICard>();
        private float nextCastTime;

        protected override void RegisterEvents() {
            if (GameMediator != null) {
                GameMediator.HandStateChanged += UpdateUI;
                GameMediator.ActionsQueueChanged += OnActionsQueueChanged;
            }
        }

        protected override void UnregisterEvents() {
            if (GameMediator != null) {
                GameMediator.HandStateChanged -= UpdateUI;
                GameMediator.ActionsQueueChanged -= OnActionsQueueChanged;
            }
        }

        private void OnActionsQueueChanged() {
            if (!IsInitialized || Player == null) return;

            queuedCards.Clear();
            var pendingActions = GameManager?.ActionsQueue?.GetPendingActions();
            if (pendingActions != null) {
                foreach (var action in pendingActions) {
                    if (action is PlayCardAction playAction && playAction.GetOwner() == Player) {
                        queuedCards.Add(playAction.GetCard());
                    }
                }
            }

            UpdateUI(Player);
        }

        private void PlayCastAnimation(CardController card) {
            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas == null) {
                Destroy(card.gameObject);
                return;
            }

            float delay = Mathf.Max(0f, nextCastTime - Time.time);
            nextCastTime = Time.time + delay + CastStagger;
            card.PlayCastAnimation(rootCanvas.transform, rootCanvas.transform.position, delay);
        }

        public override void UpdateUI(IPlayer player) {
            if (!IsInitialized || Player == null) return;

            if (player != Player) return;

            // Clear all current cards
            foreach (var card in cards.ToList()) {
                RemoveCard(card);
                if (card == null) continue;

                // A queued spell that just left the hand was cast: animate it instead of popping it out
                var instance = card.GetLinkedCardInstance();
                bool wasCast = instance != null && !(instance is ICreature)
                    && queuedCards.Contains(instance) && !player.Hand.Contains(instance);
                if (wasCast) {
                    PlayCastAnimation(card);
                }
                else {
                    Destroy(card.gameObject);
                }
            }
            cards.Clear();

            // Create new card controllers for each card in hand
            foreach (var handCard in player.Hand) {
                // Queued creatures are shown as ghosts in their target slot (see BattlefieldUI)
                bool isQueued = queuedCards.Contains(handCard);
                if (isQueued && handCard is ICreature) continue;

                var controller = CardViews.Create(handCard, Player, transform);
                if (controller != null) {
                    AddCard(controller);
                    if (isQueued) controller.SetQueued(true);
                }
            }

            UpdateLayout();
        }

        protected override void OnCardBeginDrag(CardController card) {
            if (card == null) return;
            card.transform.SetAsLastSibling();
            Log($"Begin dragging card from hand: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);
        }

        protected override void OnCardEndDrag(CardController card) {
            Log($"End dragging card from hand: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);
            UpdateLayout();
        }

        protected override void OnCardDropped(CardController card) {
            Log($"Card dropped from hand: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);

            // If the card was successfully dropped onto a valid target,
            // it will be removed from the hand by the PlayCardAction
            // We'll check if it's still in the player's hand after a short delay
            StartCoroutine(CheckCardAfterDrop(card));

            UpdateLayout();
        }

        private System.Collections.IEnumerator CheckCardAfterDrop(CardController card) {
            // Wait a short time for other handlers to process
            yield return new WaitForSeconds(0.1f);

            // If the card controller is null or destroyed, we don't need to do anything
            if (card == null) yield break;

            // Find the matching card in the player's hand by name
            var cardData = card.GetBaseCardData();
            if (cardData == null) yield break;

            // If the card is still in the hand, it wasn't handled by another component
            // Just update layout again to ensure proper positioning
            UpdateLayout();
        }

        protected override void OnCardHoverEnter(CardController card) { }
        protected override void OnCardHoverExit(CardController card) { }
    }
}
