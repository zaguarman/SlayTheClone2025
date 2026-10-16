using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class HandUI : CardContainer {
        private const float CastStagger = 0.2f;

        // The hand is held as a fan, like in Slay the Spire: the cards sit on a wide arc, overlapping, each tilted along it
        // and dipping away from the middle, and squeeze together rather than spread past MaxFanWidth. Your hand (at the
        // bottom) shows them big, sunk below the screen's edge so only their name and picture show (their Deployment Time
        // is in their top-left corner, always in sight); the card under the pointer rises out of the fan, enlarged, straight
        // and whole, its explanations beside it, and its neighbours make room. The other hand, at the top, keeps the cards at
        // board size; while its cards are face up (Fog of War off) the one under the pointer comes down out of it the same way.
        public const float YourHandScale = 1.7f;
        public const float RaisedScale = 2.1f;      // The card under the pointer, risen out of your hand
        private const float CardWidth = 160f;       // A card's size at scale 1, in hand units
        private const float CardHeight = 200f;
        private const float ShownShare = 0.62f;     // How much of a card in your hand shows above the screen's edge: name and picture (lower for the bigger board)
        private const float RaisedMargin = 10f;     // Between the risen card and the screen's edge
        private const float Overlap = 0.78f;        // Spacing between card centers, as a share of a card's width
        private const float MaxFanWidth = 1250f;    // From the first card's center to the last's
        private const float FanRadius = 3200f;      // The arc the cards sit on: smaller tilts and dips them more
        private const float HoverRoom = 0.4f;       // How far its neighbours move aside, as a share of a card's width...
        private const float YourHoverRoom = 0.22f;  // ...less in your hand, whose cards are bigger: the risen card just overlaps them
        private const float Settle = 0.12f;         // Seconds for the cards to slide to their places
        private const float Rise = 0.08f;           // Seconds for the card under the pointer to rise

        private CardController hovered;
        private bool releasePending; // The pointer left the risen card for its explanations: it stays up while they're open
        private CardController waiting; // A neighbour the pointer is on meanwhile: it rises once those explanations close
        private CardController picked; // The card being dragged out of this hand

        /// <summary>The card risen out of the fan, if any: the only one of this hand whose explanations may show.</summary>
        public CardController Raised => hovered;
        private const float PickedScale = 1.15f;  // Picked up, a card grows a little (of its size in the hand)...
        private const float OverBoardReach = 260f; // ...and shrinks to board size as it's carried this far above the hand
        private const float DiscardStagger = 0.06f;

        /// <summary>Where the bottom hand's discards fly to (the discard pile's button); the top hand's leave off the screen's edge.</summary>
        public Transform DiscardPile { get; set; }
        private readonly Dictionary<CardController, Sequence> slides = new Dictionary<CardController, Sequence>();

        // The hand at the bottom of the screen: the screen's player's, the one you play from (player 2's after switching sides, F8)
        public bool IsYourHand => Player == null || Player.IsPlayer1 == FogOfWar.PlaysAsPlayer1;
        private float CardScale => IsYourHand ? YourHandScale : 1f;

        // The hand area itself is invisible; it only shows a faint glow while a card is dragged over it
        protected override DropZoneColors ZoneColors => new DropZoneColors(
            Color.clear, new Color(1f, 1f, 1f, 0.04f), new Color(1f, 0.3f, 0.3f, 0.05f), Color.clear);

        // Cards of this player with a pending PlayCardAction, and those of them shown as ghosts on the board instead
        // (creatures, and the spells the screen can see; see BattlefieldUI)
        private readonly HashSet<ICard> queuedCards = new HashSet<ICard>();
        private readonly HashSet<ICard> onBoard = new HashSet<ICard>();
        private PlayCardAction draggedPlay; // The play of the queued card being dragged, to tell whether the drop replaced it
        private float nextCastTime;

        protected override void RegisterEvents() {
            if (GameMediator != null) {
                GameMediator.HandStateChanged += UpdateUI;
                GameMediator.ActionsQueueChanged += OnActionsQueueChanged;
                GameMediator.PlayRefused += OnPlayRefused;
            }
        }

        protected override void UnregisterEvents() {
            if (GameMediator != null) {
                GameMediator.HandStateChanged -= UpdateUI;
                GameMediator.ActionsQueueChanged -= OnActionsQueueChanged;
                GameMediator.PlayRefused -= OnPlayRefused;
            }
        }

        /// <summary>The last reason shown for a card that couldn't be played (for tests).</summary>
        public string RefusedReason { get; private set; }

        // A card that couldn't be played (its cost can't be paid) stays in the hand: it shakes, and why floats up from it
        private void OnPlayRefused(ICard card, string reason) {
            var view = cards.FirstOrDefault(c => c != null && c.GetLinkedCardInstance() == card);
            if (view == null || !FogOfWar.CanControl(Player)) return;
            RefusedReason = reason;
            view.transform.DOPunchPosition(new Vector3(12f, 0f, 0f), 0.35f, 14, 0.8f);
            var note = UITheme.NewText("Refused", transform, reason, 24f, UITheme.Hex(0xFF8A7A));
            note.fontStyle = TMPro.FontStyles.Bold;
            note.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            note.raycastTarget = false;
            UITheme.AddDropShadow(note);
            var rect = note.rectTransform;
            rect.sizeDelta = new Vector2(520f, 40f);
            rect.position = view.transform.position;
            rect.anchoredPosition += new Vector2(0f, ((RectTransform)view.transform).rect.height * 0.75f);
            rect.SetAsLastSibling();
            float from = rect.anchoredPosition.y;
            DOTween.To(() => rect.anchoredPosition.y, y => rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y), from + 60f, 1.8f)
                .SetEase(Ease.OutCubic).SetTarget(note);
            DOTween.To(() => note.alpha, a => note.alpha = a, 0f, 0.6f).SetDelay(1.3f).SetTarget(note).OnComplete(() => { if (note != null) Destroy(note.gameObject); });
        }

        private void OnActionsQueueChanged() {
            if (!IsInitialized || Player == null) return;

            queuedCards.Clear();
            onBoard.Clear();
            var pendingActions = GameManager?.ActionsQueue?.GetPendingActions();
            if (pendingActions != null) {
                var viewer = FogOfWar.ViewerOf(Player);
                foreach (var action in pendingActions) {
                    if (action is PlayCardAction playAction && playAction.GetOwner() == Player) {
                        queuedCards.Add(playAction.GetCard());
                        if (playAction.GetCard() is ICreature || !FogOfWar.Hides(viewer, action, GameManager.ActionsQueue, GameManager.ModifierManager)) {
                            onBoard.Add(playAction.GetCard());
                        }
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

        // Fades into a dart that streaks into the discard pile (off the screen's edge for a hand without a pile button)
        private void PlayDiscardAnimation(CardController card) {
            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas == null) {
                Destroy(card.gameObject);
                return;
            }
            var pile = IsYourHand ? DiscardPile : null;
            var target = pile != null ? pile.position
                : transform.TransformPoint(new Vector3(((RectTransform)transform).rect.xMin - 200f, 0f, 0f));
            float delay = Mathf.Max(0f, nextCastTime - Time.time);
            nextCastTime = Time.time + delay + DiscardStagger;
            CardFx.Discard(card, rootCanvas.transform, target, pile, delay);
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
                bool wasDiscarded = !wasCast && instance != null && !player.Hand.Contains(instance)
                    && player.Deck.GetDiscardPilePreview().Contains(instance);
                // The presenter flies a cast spell it can show from here to its stage (it hid this card); one it can't, flies from here
                bool takenByPresenter = card.TryGetComponent(out CanvasGroup group) && group.alpha <= 0.01f;
                if (wasCast && takenByPresenter) {
                    Destroy(card.gameObject);
                }
                else if (wasCast) {
                    PlayCastAnimation(card);
                }
                else if (wasDiscarded) {
                    PlayDiscardAnimation(card);
                }
                else {
                    Destroy(card.gameObject);
                }
            }
            cards.Clear();

            // Create new card controllers for each card in hand
            foreach (var handCard in player.Hand) {
                // Queued cards are shown as ghosts where they're aimed (see BattlefieldUI)
                bool isQueued = queuedCards.Contains(handCard);
                if (onBoard.Contains(handCard)) continue;

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
            float edge = ScreenEdgeY();

            for (int i = 0; i < count; i++) {
                var card = cards[i];
                if (card == null || card == CardController.Dragged) continue;

                float x = (i - (count - 1) / 2f) * spacing;
                float angle = Mathf.Asin(Mathf.Clamp(x / FanRadius, -1f, 1f)); // Along the arc
                float dip = FanRadius - Mathf.Sqrt(FanRadius * FanRadius - x * x);
                float height = CardHeight * scale;
                // Your hand: the top ShownShare of each card above the screen's edge. The other hand: at its area's middle
                float y = IsYourHand ? edge + height * (ShownShare - 0.5f) - dip : -facing * dip;
                float tilt = -facing * angle * Mathf.Rad2Deg;
                float cardScale = scale;
                if (hoveredIndex >= 0 && i != hoveredIndex) x += Mathf.Sign(i - hoveredIndex) * CardWidth * scale * (IsYourHand ? YourHoverRoom : HoverRoom);
                if (i == hoveredIndex && !card.IsFaceDown) {
                    // Whole, just clear of the screen's edge: above the bottom one, below the top one
                    cardScale = RaisedScale;
                    y = edge + facing * (CardHeight * RaisedScale * 0.5f + RaisedMargin);
                    tilt = 0f;
                }
                else if (i == hoveredIndex) tilt = 0f;
                card.transform.SetSiblingIndex(i); // Right-hand cards overlap left-hand ones
                Place(card, new Vector2(x, y), tilt, cardScale, animate, i == hoveredIndex ? Rise : Settle);
            }
            if (hovered != null && hoveredIndex >= 0) hovered.transform.SetAsLastSibling(); // On top of its neighbours
        }

        // The screen's edge your hand sits on (its bottom; the top for the other hand), in this hand's own units
        private float ScreenEdgeY() {
            var screenPoint = new Vector2(Screen.width * 0.5f, IsYourHand ? 0f : Screen.height);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPoint,
                CanvasUtility.GetEventCamera(this), out var local) ? local.y : 0f;
        }

        private void Place(CardController card, Vector2 position, float tilt, float scale, bool animate, float seconds) {
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
            slides[card] = DOTween.Sequence()
                .Join(DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, position, seconds).SetEase(Ease.OutQuad))
                .Join(rect.DOLocalRotate(new Vector3(0f, 0f, tilt), seconds).SetEase(Ease.OutQuad))
                .Join(rect.DOScale(scale, seconds).SetEase(Ease.OutBack))
                .OnKill(() => slides.Remove(card));
        }

        private Image zoneImage;

        private void Update() {
            // The hand's area only takes the pointer while a card is carried (a drop back into the hand): at rest it would
            // cover the bottom of the board, the cards in the back row (the hand's own cards take the pointer themselves)
            if (zoneImage != null || TryGetComponent(out zoneImage)) zoneImage.raycastTarget = CardController.Dragged != null;
            Carry();
            if (!releasePending) return;
            if (hovered != null && Tooltip.Existing != null && Tooltip.Existing.ActiveCard == hovered && Tooltip.Existing.Preview == hovered) return;
            releasePending = false;
            if (hovered == null) return;
            // Its explanations closed: the neighbour the pointer went on to rises instead, with its own
            hovered = waiting != null && cards.Contains(waiting) ? waiting : null;
            waiting = null;
            Arrange(animate: true);
            if (hovered != null) Tooltip.Shared.ShowTooltip(hovered);
        }

        protected override void OnCardBeginDrag(CardController card) {
            if (card == null) return;
            draggedPlay = card.IsQueued ? QueuedPlayOf(card.GetLinkedCardInstance()) : null;
            if (slides.TryGetValue(card, out var running)) running.Kill();
            hovered = null;
            waiting = null;
            releasePending = false;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one * CardScale * PickedScale; // Picked up: it grows
            picked = card;
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
            picked = null;
            Arrange(animate: true); // Back into the fan, if it's still in the hand
        }

        // While a card is carried it keeps the picked-up size over the hand, and eases down to board size as it rises
        // over the board, so the slot it's aimed at stays in view; over a slot it'll land in, it shrinks to a slotted card's size
        private void Carry() {
            if (picked == null || CardController.Dragged != picked) return;
            float big = CardScale * PickedScale;
            float scale = big;
            var rect = (RectTransform)transform;
            // The board's size by the sliders (UI > Battlefield Tuning): the slot's, and the card's in it
            if (picked.SnapsTo != null) scale = picked.SnapsTo.lossyScale.x * BattlefieldUI.CardInSlotScale / Mathf.Max(1e-6f, picked.transform.parent.lossyScale.x);
            else if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, GameInput.PointerPosition, CanvasUtility.GetEventCamera(this), out var local)) {
                float away = IsYourHand ? local.y - rect.rect.yMax : rect.rect.yMin - local.y; // Toward the board
                scale = Mathf.Lerp(big, BattlefieldUI.SlottedScaleUnder(picked.transform.parent), Mathf.InverseLerp(0f, OverBoardReach, away));
            }
            float now = picked.transform.localScale.x;
            picked.transform.localScale = Vector3.one * Mathf.Lerp(now, scale, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
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
            if (card == null || CardController.Dragged != null || !cards.Contains(card) || card == hovered) return;
            // Crossing a neighbour on the way to the risen card's explanations doesn't drop it from under them
            if (releasePending) {
                waiting = card;
                return;
            }
            hovered = card;
            releasePending = false;
            Arrange(animate: true);
        }

        protected override void OnCardHoverExit(CardController card) {
            if (card == waiting) waiting = null;
            if (card != hovered) return;
            // Leaving through its edge toward the screen's edge keeps it up: dropping back under the pointer would only
            // raise it again
            var rect = (RectTransform)card.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, GameInput.PointerPosition, CanvasUtility.GetEventCamera(card), out var local)
                && (IsYourHand ? local.y < rect.rect.yMin : local.y > rect.rect.yMax) && local.x > rect.rect.xMin && local.x < rect.rect.xMax) return;
            // On its way to its explanations: it stays up while they're open (see Update)
            if (Tooltip.Existing != null && Tooltip.Existing.ActiveCard == card && Tooltip.Existing.OpenKeywords.Count > 0) {
                releasePending = true;
                return;
            }
            hovered = null;
            Arrange(animate: true);
        }

        public override void RemoveCard(CardController card) {
            if (card == waiting) waiting = null;
            if (card == hovered) {
                hovered = null;
                releasePending = false;
            }
            if (card != null && slides.TryGetValue(card, out var running)) running.Kill();
            base.RemoveCard(card);
        }
    }
}
