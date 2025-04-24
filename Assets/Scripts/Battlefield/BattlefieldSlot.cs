using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using static DebugLogger;

public class BattlefieldSlot : MonoBehaviour, ITarget, IPointerEnterHandler, IPointerExitHandler {
    private RectTransform rectTransform;
    private Image backgroundImage;

    public string TargetId { get; private set; }
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

        // Set slot reference on the creature
        var creature = controller.GetLinkedCreature();
        if (creature != null) {
            creature.Slot = this;
        }

        // Parent and position the card
        controller.transform.SetParent(transform, false);
        controller.transform.localPosition = Vector3.zero;
        controller.transform.localRotation = Quaternion.identity;

        OccupyingCard = controller;
        OccupyingCreature = controller.GetLinkedCreature();
    }

    public void OccupySlot(CardController card) {
        AssignCreature(card);
    }

    public void ClearSlot(bool destroyCard = true) {
        if (OccupyingCard != null && destroyCard) {
            Destroy(OccupyingCard.gameObject);
        }
        OccupyingCard = null;
        OccupyingCreature = null;
    }

    public bool IsOccupied() {
        return OccupyingCreature != null;
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
        if (gameReferences == null)
        {
             LogError($"SetGameReferences called with null references for slot {name} (TargetID: {TargetId})", LogTag.Initialization | LogTag.UI);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) {
        // --- FIX: Check if gameReferences was correctly injected ---
        if (gameReferences == null) {
            // Log an error - this indicates a problem in BattlefieldUI's initialization
            LogError($"Cannot handle PointerEnter for slot {name} (TargetID: {TargetId}) - gameReferences is null. Check BattlefieldUI initialization.", LogTag.Initialization | LogTag.UI);
            return; // Cannot proceed without references
        }
        // --- END FIX ---

        Tooltip tooltip = gameReferences.GetTooltip();
        if (tooltip == null)
        {
            // Log a warning if the tooltip system isn't ready, but don't crash
            LogWarning($"Tooltip system not available when hovering over slot {name}", LogTag.UI);
            return;
        }

        if (IsOccupied() && OccupyingCard != null) {
            // Show the card tooltip which includes effects info
            tooltip.ShowTooltip(OccupyingCard);
        } else {
            // Show just slot info
            string tooltipText = $"<b>Slot: {name}</b>\nTargetID: {TargetId}";
            tooltip.ShowTooltip(tooltipText);
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
         // --- FIX: Check if gameReferences was correctly injected ---
         if (gameReferences == null) {
            // No need to log again if it was already logged in OnPointerEnter,
            // but check is needed to prevent NullReferenceException when accessing tooltip.
            return;
         }
         // --- END FIX ---

        Tooltip tooltip = gameReferences.GetTooltip();
        // Also check if tooltip object exists before calling HideTooltip
        if (tooltip != null && tooltip.gameObject != null) {
            tooltip.HideTooltip();
        }
    }

    public void MarkForCombat() {
        if (backgroundImage != null) {
            backgroundImage.color = combatMarkColor;
        }
    }
}
