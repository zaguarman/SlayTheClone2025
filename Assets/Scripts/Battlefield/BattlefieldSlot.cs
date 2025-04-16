using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Enums;
using static DebugLogger;

// Implement IModifiable
public class BattlefieldSlot : MonoBehaviour, ITarget, IPointerEnterHandler, IPointerExitHandler, IModifiable {
    private RectTransform rectTransform;
    private Image backgroundImage;

    public string TargetId { get; private set; }
    public CardController OccupyingCard { get; private set; }
    public ICreature OccupyingCreature { get; private set; }

    // Modifier Controller
    public ModifierController ModifierController { get; private set; }

    private Color defaultColor;
    private Color validDropColor;
    private Color invalidDropColor;
    private Color hoverColor;
    private Color combatMarkColor;

    private void Awake() {
        rectTransform = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        backgroundImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        TargetId = System.Guid.NewGuid().ToString();

        // Initialize Modifier Controller here, requires GameMediator instance
        if (GameMediator.Instance != null) {
             ModifierController = new ModifierController(this, GameMediator.Instance);
             Log($"BattlefieldSlot {TargetId} created. MC Initialized.", LogTag.Initialization | LogTag.Effects);
        } else {
             LogError($"BattlefieldSlot {TargetId} cannot initialize ModifierController - GameMediator not ready.", LogTag.Initialization);
        }
    }

    public void Initialize(Color defaultColor, Color validDropColor, Color invalidDropColor, Color hoverColor) {
        this.defaultColor = defaultColor;
        this.validDropColor = validDropColor;
        this.invalidDropColor = invalidDropColor;
        this.hoverColor = hoverColor;
        this.combatMarkColor = new Color(1f, 0.5f, 0.5f, 0.5f); // Light red with transparency

        if(backgroundImage != null) backgroundImage.color = defaultColor; // Null check
        ClearSlot();
    }

    public void SetPosition(Vector2 position) {
        if (rectTransform != null) {
            rectTransform.anchoredPosition = position;
        }
    }

    public void AssignCreature(CardController controller) {
        ClearSlot(); // Clear previous state first
        if (controller == null) return;

        OccupyingCard = controller;
        OccupyingCreature = controller.GetLinkedCreature();

        // Parent and position the card
        controller.transform.SetParent(transform, false); // Use false to maintain world scale if needed, adjust rect later
        controller.transform.localPosition = Vector3.zero;
        controller.transform.localRotation = Quaternion.identity;
        // Optional: Reset scale if parenting messes it up
        // controller.transform.localScale = Vector3.one;

        // Set slot reference on the creature AFTER assigning
        if (OccupyingCreature != null) {
            OccupyingCreature.Slot = this;
             Log($"Assigned creature {OccupyingCreature.Name} ({OccupyingCreature.TargetId}) to slot {TargetId}", LogTag.Creatures);
        } else {
            LogWarning($"Assigned CardController {controller.name} to slot {TargetId}, but it has no linked ICreature.", LogTag.Creatures);
        }
    }

    public void OccupySlot(CardController card) {
        AssignCreature(card);
    }

    public void ClearSlot(bool destroyCard = true) {
         // Clear link from creature first
         if (OccupyingCreature != null) {
             OccupyingCreature.Slot = null;
             Log($"Cleared slot link for creature {OccupyingCreature.Name} ({OccupyingCreature.TargetId}) from slot {TargetId}", LogTag.Creatures);
         }

        if (OccupyingCard != null && destroyCard) {
            Log($"Destroying CardController {OccupyingCard.name} in slot {TargetId}", LogTag.Creatures);
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

    public string GetModifiableId() => TargetId;

    public int GetBaseStat(StatType statType) {
        // Slots provide bonuses, they don't typically have base Attack/Health
        return 0;
    }

    public int GetCurrentStat(StatType statType) {
        // If slots could have their own health/attack, calculate here.
        // For now, it only contributes via modifiers applied TO it.
        return GetBaseStat(statType);
    }

    public bool IsValidTarget() => true;

    public void OnPointerEnter(PointerEventData eventData) {
        Tooltip tooltip = GameReferences.Instance?.GetTooltip(); // Null check GameReferences
        if (tooltip == null) return;

        if (IsOccupied() && OccupyingCard != null) {
            tooltip.ShowTooltip(OccupyingCard); // Shows creature/card info
        } else {
            // Show slot info only
            string tooltipText = $"<b>Slot: {name}</b>\nTargetID: {TargetId}\nEmpty";
            tooltip.ShowTooltip(tooltipText);
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        GameReferences.Instance?.GetTooltip()?.HideTooltip(); // Null checks
    }

    public void MarkForCombat() {
        if (backgroundImage != null) {
            backgroundImage.color = combatMarkColor;
        }
    }

    // Cleanup ModifierController
    private void OnDestroy() {
        ModifierController?.Cleanup();
        Log($"BattlefieldSlot {TargetId} destroyed.", LogTag.Effects);
    }
}
