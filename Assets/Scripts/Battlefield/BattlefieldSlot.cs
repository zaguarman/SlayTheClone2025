using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    public void OnPointerEnter(PointerEventData eventData) {
        Tooltip tooltip = GameReferences.Instance.GetCardTooltip();
        if (tooltip == null) return;

        if (IsOccupied()) {
            // Show creature info first, then slot info
            string tooltipText = $"<b>{OccupyingCreature.Name}</b>\n";
            tooltipText += $"TargetID: {OccupyingCreature.TargetId}\n\n";
            tooltipText += $"<color=#888888>Slot: {name}\nTargetID: {TargetId}</color>";
            tooltip.ShowTooltip(tooltipText);
        } else {
            // Show just slot info
            string tooltipText = $"<b>Slot: {name}</b>\nTargetID: {TargetId}";
            tooltip.ShowTooltip(tooltipText);
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        Tooltip tooltip = GameReferences.Instance.GetCardTooltip();
        if (tooltip != null) {
            tooltip.HideTooltip();
        }
    }
}
