using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class BattlefieldSlot : MonoBehaviour, ITarget, IPointerEnterHandler, IPointerExitHandler {
        private RectTransform rectTransform;
        private Image backgroundImage;

        public string TargetId { get; private set; }
        public IPlayer Owner { get; set; } // Player whose battlefield this slot belongs to
        public CardController OccupyingCard { get; private set; }
        public ICreature OccupyingCreature { get; private set; }

        private Color defaultColor;
        private Color validDropColor;
        private Color invalidDropColor;
        private Color hoverColor;
        private Color combatMarkColor;

        // Reference to GameReferences for tooltip access - MUST be set via SetGameReferences
        private IGameReferences gameReferences;

        private void Awake() {
            rectTransform = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            backgroundImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();

            TargetId = System.Guid.NewGuid().ToString();
        }

        public void Initialize(Color defaultColor, Color validDropColor, Color invalidDropColor, Color hoverColor) {
            this.defaultColor = defaultColor;
            this.validDropColor = validDropColor;
            this.invalidDropColor = invalidDropColor;
            this.hoverColor = hoverColor;
            this.combatMarkColor = new Color(1f, 0.5f, 0.5f, 0.5f); // Light red with transparency

            backgroundImage.color = defaultColor;
            ClearSlot();
        }

        public void SetPosition(Vector2 position) {
            if (rectTransform != null) {
                rectTransform.anchoredPosition = position;
            }
        }

        public void AssignCreature(CardController controller) {
            ClearSlot();
            if (controller == null) return;

            var creature = controller.GetLinkedCreature();
            if (creature != null) {
                creature.Slot = this;
            }

            SnapToSlot(controller);
            OccupyingCard = controller;
            OccupyingCreature = creature;
        }

        // Parents the card to this slot, centered and upright
        public void SnapToSlot(CardController card) {
            card.transform.SetParent(transform, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Exchanges the cards of two slots; with one slot empty this moves the other's card.</summary>
        public static void SwapCards(BattlefieldSlot first, BattlefieldSlot second) {
            var firstCard = first.OccupyingCard;
            var secondCard = second.OccupyingCard;
            first.ClearSlot(false);
            second.ClearSlot(false);
            if (firstCard != null) second.AssignCreature(firstCard);
            if (secondCard != null) first.AssignCreature(secondCard);
        }

        public void ClearSlot(bool destroyCard = true) {
            if (OccupyingCard != null) {
                OccupyingCard.SetReplacementPreview(false);
                if (destroyCard) OccupyingCard.PlayLeavePlayAnimation(); // Fades out, then destroys itself
            }
            // Always clear the references
            OccupyingCard = null;
            OccupyingCreature = null;
        }

        public bool IsOccupied() {
            // Check if either the controller or creature reference is non-null
            return OccupyingCard != null || OccupyingCreature != null;
        }

        public void ResetVisuals() {
            if (backgroundImage != null) {
                backgroundImage.color = defaultColor;
            }
        }

        public bool IsValidTarget() => true;

        // Method to set the GameReferences dependency (called by BattlefieldUI)
        public void SetGameReferences(IGameReferences references) {
            gameReferences = references;
            if (gameReferences == null) {
                // Log an error if null references are passed during setup
                LogError($"SetGameReferences called with null references for slot {name} (TargetID: {TargetId})", LogTag.Initialization | LogTag.UI);
            }
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (IsCreatureFromHandDraggedOver(eventData)) {
                OccupyingCard.SetReplacementPreview(true);
            }

            // --- Dependency Check ---
            if (gameReferences == null) {
                LogError($"Cannot handle PointerEnter for slot {name} (TargetID: {TargetId}) - gameReferences is null. Check BattlefieldUI initialization.", LogTag.Initialization | LogTag.UI);
                return;
            }

            Tooltip tooltip = gameReferences.GetTooltip();
            // --- Tooltip System Check ---
            if (tooltip == null) {
                LogWarning($"Tooltip system (via gameReferences) not available when hovering over slot {name}", LogTag.UI);
                return; // Cannot proceed without a valid tooltip instance
            }
            // Ensure the tooltip GameObject itself hasn't been destroyed
            if (tooltip.gameObject == null) {
                LogWarning($"Tooltip GameObject was destroyed. Cannot show tooltip for slot {name}", LogTag.UI);
                return;
            }

            // --- Show Tooltip ---
            if (IsOccupied() && OccupyingCard != null) {
                tooltip.ShowTooltip(OccupyingCard); // Show card details
            }
            else {
                // Show basic slot info
                string tooltipText = $"<b>Slot: {name}</b>\nTargetID: {TargetId.ShortId()}";
                tooltip.ShowTooltip(tooltipText);
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (OccupyingCard != null) {
                OccupyingCard.SetReplacementPreview(false);
            }

            // --- Dependency Check ---
            if (gameReferences == null) {
                // No need to log again if logged in Enter, but check is crucial
                return;
            }

            Tooltip tooltip = gameReferences.GetTooltip();
            // --- Tooltip System Check ---
            // Check if tooltip instance and its GameObject exist before hiding
            if (tooltip != null && tooltip.gameObject != null) {
                tooltip.HideTooltip();
            }
        }

        // True while a creature card from a hand is dragged over this occupied slot (dropping it replaces the occupant)
        private bool IsCreatureFromHandDraggedOver(PointerEventData eventData) {
            if (OccupyingCard == null || eventData?.pointerDrag == null) return false;

            var draggedCard = eventData.pointerDrag.GetComponent<CardController>();
            return draggedCard != null
                && draggedCard != OccupyingCard
                && draggedCard.GetBaseCardData() is CreatureData
                && draggedCard.GetComponentInParent<HandUI>() != null;
        }

        public void MarkForCombat() {
            if (backgroundImage != null) {
                backgroundImage.color = combatMarkColor;
            }
        }
    }
}
