using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// On-screen view of one <see cref="BoardSlot"/>: shows the card of the creature the rules placed there
    /// and takes pointer input. Rules never touch it; <see cref="BattlefieldUI"/> keeps it in sync with the model.
    /// </summary>
    public class BattlefieldSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        private RectTransform rectTransform;
        private Image backgroundImage;

        public BoardSlot Model { get; private set; }
        public string TargetId => Model?.TargetId;
        public IPlayer Owner => Model?.Owner; // Player whose battlefield this slot belongs to
        public ICreature OccupyingCreature => Model?.OccupyingCreature;
        public CardController OccupyingCard { get; private set; } // The card currently shown here

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
        }

        public void Initialize(BoardSlot model, Color defaultColor, Color validDropColor, Color invalidDropColor, Color hoverColor) {
            Model = model;
            this.defaultColor = defaultColor;
            this.validDropColor = validDropColor;
            this.invalidDropColor = invalidDropColor;
            this.hoverColor = hoverColor;
            this.combatMarkColor = new Color(1f, 0.5f, 0.5f, 0.5f); // Light red with transparency

            backgroundImage.color = defaultColor;
        }

        public void SetPosition(Vector2 position) {
            if (rectTransform != null) {
                rectTransform.anchoredPosition = position;
            }
        }

        /// <summary>Shows the card here, centered and upright.</summary>
        public void ShowCard(CardController card) {
            OccupyingCard = card;
            if (card == null) return;

            card.transform.SetParent(transform, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Stops showing the current card and returns it; the caller decides what happens to it.</summary>
        public CardController ReleaseCard() {
            var card = OccupyingCard;
            OccupyingCard = null;
            if (card != null) card.SetReplacementPreview(false);
            return card;
        }

        public void ResetVisuals() {
            if (backgroundImage != null) {
                backgroundImage.color = defaultColor;
            }
        }

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
            if (OccupyingCard != null) {
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
