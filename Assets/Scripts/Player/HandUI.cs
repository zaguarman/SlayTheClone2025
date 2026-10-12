using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class HandUI : CardContainer {
        private const float CastStagger = 0.2f;

        // The hand is held as a fan, like in Slay the Spire and Hearthstone: cards tilt and dip away from the middle,
        // and squeeze together (overlapping) rather than spread past MaxFanWidth
        private const float MaxFanWidth = 1300f;
        private const float FanReach = 700f;   // Distance from the middle at which a card gets the full tilt and dip
        private const float FanTilt = 8f;      // Degrees
        private const float FanDip = 28f;

        // The hand area itself is invisible; it only shows a faint glow while a card is dragged over it
        protected override DropZoneColors ZoneColors => new DropZoneColors(
            Color.clear, new Color(1f, 1f, 1f, 0.04f), new Color(1f, 0.3f, 0.3f, 0.05f), Color.clear);

        // Cards of this player with a pending PlayCardAction
        private readonly HashSet<ICard> queuedCards = new HashSet<ICard>();
        private PlayCardAction draggedPlay; // The play of the queued card being dragged, to tell whether the drop replaced it
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

        protected override void SetupCardEventHandlers(CardController controller) {
            base.SetupCardEventHandlers(controller);
            controller.OnCardClicked.AddListener(TakeBackPlay);
            controller.OnCardRightClicked.AddListener(TakeBackPlay);
        }

        // Clicking or right-clicking a queued card takes the play back, like clicking a creature withdraws its attack
        private void TakeBackPlay(CardController card) {
            if (!card.IsQueued || (GameManager?.TurnManager?.IsResolving ?? true)) return;
            GameManager.ActionsQueue.CancelPlay(card.GetLinkedCardInstance());
        }

        /// <summary>The card shown in the hand for this card, if any.</summary>
        public CardController GetCardController(ICard card) =>
            card == null ? null : cards.FirstOrDefault(c => c != null && c.GetLinkedCardInstance() == card);

        private PlayCardAction QueuedPlayOf(ICard card) =>
            GameManager?.ActionsQueue?.GetPendingActions().OfType<PlayCardAction>().FirstOrDefault(p => p.GetCard() == card);

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
                    if (isQueued) controller.SetQueued(true, canBeDragged: true);
                    controller.SetFaceDown(FogOfWar.HidesHandOf(Player)); // The enemy's hand shows only card backs
                }
            }

            UpdateLayout();
        }

        protected override void UpdateLayout() {
            int count = cards.Count;
            float spacing = count > 1 ? Mathf.Min(settings.spacing, MaxFanWidth / (count - 1)) : 0f;
            float facing = Player == null || Player.IsPlayer1 ? 1f : -1f; // The top hand fans the other way, toward the table

            for (int i = 0; i < count; i++) {
                var card = cards[i];
                if (card == null) continue;

                float x = (i - (count - 1) / 2f) * spacing;
                float reach = x / FanReach;
                SetupCardTransform(card, new Vector2(x, -facing * FanDip * reach * reach));
                card.transform.localRotation = Quaternion.Euler(0f, 0f, -facing * FanTilt * reach);
                card.transform.SetSiblingIndex(i); // Right-hand cards overlap left-hand ones
            }
        }

        protected override void OnCardBeginDrag(CardController card) {
            if (card == null) return;
            draggedPlay = card.IsQueued ? QueuedPlayOf(card.GetLinkedCardInstance()) : null;
            card.transform.localRotation = Quaternion.identity;
            card.transform.SetAsLastSibling();
            Log($"Begin dragging card from hand: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);
        }

        protected override void OnCardEndDrag(CardController card) {
            Log($"End dragging card from hand: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);

            // A queued card dropped where it can't be played keeps its old play; take it back. A valid drop already replaced it.
            if (draggedPlay != null && QueuedPlayOf(card.GetLinkedCardInstance()) == draggedPlay) {
                GameManager.ActionsQueue.CancelPlay(card.GetLinkedCardInstance());
            }
            draggedPlay = null;
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
