using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class HandUI : CardContainer {
        private const float CastStagger = 0.2f;

        // The hand is held as a fan, like in Slay the Spire: the cards sit on a wide arc, overlapping, each tilted along it
        // and dipping away from the middle, and squeeze together rather than spread past MaxFanWidth. Your hand (at the
        // bottom) shows them big, about Slay the Spire's size next to the board, sunk a little below the edge so the
        // board's bottom row stays clear; the card under the pointer rises out of the fan, straight, and its neighbours
        // make room. The other hand, at the top, keeps the cards at board size.
        public const float YourHandScale = 1.5f;
        private const float CardWidth = 160f;       // A card's width at scale 1, in hand units
        private const float Overlap = 0.8f;         // Spacing between card centers, as a share of a card's width
        private const float MaxFanWidth = 1250f;    // From the first card's center to the last's
        private const float FanRadius = 2600f;      // The arc the cards sit on: smaller tilts and dips them more
        private const float Sink = 32f;             // How far your hand sits below its area's middle
        private const float HoverRise = 34f;        // Where the hovered card stands: fully in view
        private const float HoverRoom = 0.35f;      // How far its neighbours move aside, as a share of a card's width
        private const float Settle = 0.12f;         // Seconds for the other cards to slide to their places

        private CardController hovered;
        private readonly Dictionary<CardController, Sequence> slides = new Dictionary<CardController, Sequence>();

        // The player at the bottom of the table: the hand you play from
        private bool IsYourHand => Player == null || Player.IsPlayer1;
        private float CardScale => IsYourHand ? YourHandScale : 1f;

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

        protected override void UpdateLayout() => Arrange(animate: false);

        // Places every card on the fan: at once, or sliding there (when the hovered card changes)
        private void Arrange(bool animate) {
            int count = cards.Count;
            float scale = CardScale;
            float spacing = count > 1 ? Mathf.Min(CardWidth * scale * Overlap, MaxFanWidth / (count - 1)) : 0f;
            float facing = IsYourHand ? 1f : -1f; // The top hand fans the other way, toward the table
            int hoveredIndex = hovered != null ? cards.IndexOf(hovered) : -1;

            for (int i = 0; i < count; i++) {
                var card = cards[i];
                if (card == null || card == CardController.Dragged) continue;

                float x = (i - (count - 1) / 2f) * spacing;
                float angle = Mathf.Asin(Mathf.Clamp(x / FanRadius, -1f, 1f)); // Along the arc
                float dip = FanRadius - Mathf.Sqrt(FanRadius * FanRadius - x * x);
                float y = -dip - (IsYourHand ? Sink : 0f);
                float tilt = -angle * Mathf.Rad2Deg;
                if (hoveredIndex >= 0 && i != hoveredIndex) x += Mathf.Sign(i - hoveredIndex) * CardWidth * scale * HoverRoom;
                if (i == hoveredIndex) {
                    y = IsYourHand ? HoverRise : 0f;
                    tilt = 0f;
                }
                card.transform.SetSiblingIndex(i); // Right-hand cards overlap left-hand ones
                Place(card, new Vector2(x, facing * y), facing * tilt, scale, animate && i != hoveredIndex);
            }
            if (hovered != null && hoveredIndex >= 0) hovered.transform.SetAsLastSibling(); // On top of its neighbours
        }

        // The hovered card jumps straight to its place, so the enlarged view next to it sees where it ends up
        private void Place(CardController card, Vector2 position, float tilt, float scale, bool animate) {
            if (slides.TryGetValue(card, out var running)) running.Kill();
            slides.Remove(card);
            var rect = (RectTransform)card.transform;
            SetupCardTransform(card, rect.anchoredPosition);
            if (!animate) {
                rect.anchoredPosition = position;
                rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
                rect.localScale = Vector3.one * scale;
                return;
            }
            rect.localScale = Vector3.one * scale;
            slides[card] = DOTween.Sequence()
                .Join(DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, position, Settle).SetEase(Ease.OutQuad))
                .Join(rect.DOLocalRotate(new Vector3(0f, 0f, tilt), Settle).SetEase(Ease.OutQuad))
                .OnKill(() => slides.Remove(card));
        }

        protected override void OnCardBeginDrag(CardController card) {
            if (card == null) return;
            draggedPlay = card.IsQueued ? QueuedPlayOf(card.GetLinkedCardInstance()) : null;
            if (slides.TryGetValue(card, out var running)) running.Kill();
            hovered = null;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one; // Dragged at board size, so it doesn't cover the slots it's aimed at
            card.transform.SetAsLastSibling();
            Arrange(animate: true); // The others close the gap it leaves
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

        protected override void OnCardHoverEnter(CardController card) {
            if (card == null || CardController.Dragged != null || !cards.Contains(card)) return;
            hovered = card;
            Arrange(animate: true);
        }

        protected override void OnCardHoverExit(CardController card) {
            if (card != hovered) return;
            // Leaving through its bottom edge (toward the screen's edge) keeps it up: dropping back under the pointer
            // would only raise it again
            var rect = (RectTransform)card.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, CanvasUtility.GetEventCamera(card), out var local)
                && local.y < rect.rect.yMin && local.x > rect.rect.xMin && local.x < rect.rect.xMax) return;
            hovered = null;
            Arrange(animate: true);
        }

        public override void RemoveCard(CardController card) {
            if (card == hovered) hovered = null;
            if (card != null && slides.TryGetValue(card, out var running)) running.Kill();
            base.RemoveCard(card);
        }
    }
}
