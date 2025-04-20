using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using DG.Tweening;
using static DebugLogger;

[Serializable]
public class CardUnityEvent : UnityEvent<CardController> { }

public class CardController : UIComponent, IPointerEnterHandler, IPointerExitHandler, IDragHandler, IBeginDragHandler, IEndDragHandler {
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image cardImage;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private CanvasGroup canvasGroup;
    private Vector3 originalPosition;
    private Transform originalParent;
    private bool isDragging;
    private CardData cardData;

    private ICreature linkedCreature;
    private Tooltip tooltip;

    public CardUnityEvent OnBeginDragEvent = new CardUnityEvent();
    public CardUnityEvent OnEndDragEvent = new CardUnityEvent();
    public CardUnityEvent OnCardDropped = new CardUnityEvent();
    public Action OnPointerEnterHandler;
    public Action OnPointerExitHandler;

    public Transform OriginalParent => originalParent;
    public ICreature GetLinkedCreature() => linkedCreature;
    public bool IsPlayer1Card() => Player?.IsPlayer1() ?? false;
    public CardData GetCardData() => cardData;

    protected override void Awake() {
        base.Awake();
        SetupComponents();
    }

    private void SetupComponents() {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        cardImage = GetComponent<Image>();
    }

    public void Setup(CardData data, IPlayer owner, ICreature creature = null) {
        base.Initialize(owner);
        cardData = data;
        linkedCreature = creature;
        UpdateUI();
    }

    protected override void RegisterEvents() {
        if (gameMediator != null) {
            // Listen for CreatureDamaged event to update Health/Armor display
            gameMediator.AddCreatureDamagedListener(OnCreatureDamaged);
            gameMediator.AddCreatureDiedListener(OnCreatureDied);
            gameMediator.AddCreatureArmorChangedListener(OnCreatureArmorChanged); // Listen for armor changes
            // Also listen for general game state changes which might affect modifiers
            gameMediator.AddGameStateChangedListener(OnGameStateChanged);
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            gameMediator.RemoveCreatureDamagedListener(OnCreatureDamaged);
            gameMediator.RemoveCreatureDiedListener(OnCreatureDied);
            gameMediator.RemoveCreatureArmorChangedListener(OnCreatureArmorChanged);
            gameMediator.RemoveGameStateChangedListener(OnGameStateChanged);
        }
    }

    private void OnGameStateChanged() {
        // If this card is linked to a creature, update its UI as modifiers might have changed
        if (linkedCreature != null) {
            UpdateUI();
        }
    }

    private void OnCreatureDamaged(ICreature creature, int damage) {
        if (linkedCreature != null && creature.TargetId == linkedCreature.TargetId) {
            Log($"Creature {creature.Name} took {damage} damage, updating UI (TargetID: {creature.TargetId.ToUpper()}) (Card TargetID: {GetInstanceID().ToString().ToUpper()})", LogTag.Creatures | LogTag.UI);
            linkedCreature = creature;
            UpdateUI();

            // If tooltip is showing this card, update it
            var tooltip = GetTooltip();
            if (tooltip != null && tooltip.gameObject.activeSelf) {
                tooltip.UpdateTooltipContent(this);
            }
        }
    }

    // NEW: Listener for Armor Pool changes
    private void OnCreatureArmorChanged(ICreature creature, int newArmor) {
        if (linkedCreature != null && creature.TargetId == linkedCreature.TargetId) {
            Log($"Creature {creature.Name} armor changed to {newArmor}, updating UI (TargetID: {creature.TargetId.ToUpper()}) (Card TargetID: {GetInstanceID().ToString().ToUpper()})", LogTag.Creatures | LogTag.UI | LogTag.Effects);
            // No need to reassign linkedCreature, just update UI
            UpdateUI();
        }
    }

    private void OnCreatureDied(ICreature creature) {
        if (linkedCreature != null && creature.TargetId == linkedCreature.TargetId) {
            Log($"Creature {creature.Name} died, updating UI (TargetID: {creature.TargetId.ToUpper()}) (Card TargetID: {GetInstanceID().ToString().ToUpper()})", LogTag.Creatures | LogTag.UI);
            linkedCreature = null;
            UpdateUI();

            // Hide tooltip if showing
            GetTooltip().HideTooltip();
        }
    }

    private Tooltip GetTooltip() {
        if (tooltip == null) {
            tooltip = gameReferences.GetTooltip();
        }
        return tooltip;
    }

    public override void UpdateUI(IPlayer player = null) {
        if (cardData == null) {
            LogWarning("Attempted to update UI with null card data (Card TargetID: " + GetInstanceID().ToString().ToUpper() + ")", LogTag.UI | LogTag.Cards);
            return;
        }

        UpdateCardText();
        UpdateCardVisuals();
    }

    // Updated UpdateCardText to include Armor and show only current health
    private void UpdateCardText() {
        if (nameText == null || statsText == null) {
             LogWarning("UI text components missing on CardController: " + gameObject.name, LogTag.UI);
             return;
        }
        nameText.text = cardData.cardName;

        if (descriptionText != null) {
            string shortDesc = cardData.description ?? "";
            if (shortDesc.Length > 30) {
                shortDesc = shortDesc.Substring(0, 27) + "...";
            }
            descriptionText.text = shortDesc;
        } else {
             // LogWarning("Description text component missing on CardController: " + gameObject.name, LogTag.UI);
        }


        if (cardData is CreatureData creatureData) {
            statsText.gameObject.SetActive(true);

            if (linkedCreature != null && linkedCreature.Health > 0) {
                // Creature is alive and linked, show current effective stats including armor
                string atk = linkedCreature.Attack.ToString();
                string health = linkedCreature.Health.ToString(); // Current Health
                string speed = linkedCreature.Speed.ToString();
                int armor = linkedCreature.CurrentArmorPool; // Use CurrentArmorPool

                // Format: A / H(+Arm) / S
                string healthArmorPart = $"{health}";
                if (armor > 0) {
                    healthArmorPart += $"<color=#ADD8E6>+{armor}</color>"; // Light blue color for armor
                }

                statsText.text = $"{atk} / {healthArmorPart} / {speed}";
            }
            else {
                // Creature is dead, not linked, or data is just for display (e.g., in hand)
                // Show base stats from CreatureData
                string atk = creatureData.attack.ToString();
                string health = creatureData.health.ToString(); // Base Health
                string speed = creatureData.speed.ToString();
                // Format: A / H / S (No armor shown for non-active creatures)
                statsText.text = $"{atk} / {health} / {speed}";
            }
        } else if (cardData is SpellData) {
            statsText.gameObject.SetActive(true);
            statsText.text = "Spell"; // Spells don't have A/H/S/Armor
        } else {
            statsText.gameObject.SetActive(false);
        }
    }

    private void UpdateCardVisuals() {
        if (cardImage != null && Player != null) {
            cardImage.color = Player.IsPlayer1()
                ? gameReferences.GetPlayer1CardColor()
                : gameReferences.GetPlayer2CardColor();
        }
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (canvasGroup == null) {
            LogWarning("CanvasGroup is missing, adding it now (Card TargetID: " + GetInstanceID().ToString().ToUpper() + ")", LogTag.UI);
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        originalPosition = transform.position;
        originalParent = transform.parent;
        isDragging = true;
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();

        // Hide tooltip when dragging starts
        GetTooltip()?.HideTooltip();

        OnBeginDragEvent.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData) {
        if (!isDragging) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            eventData.position,
            parentCanvas.worldCamera,
            out Vector2 localPoint)) {
            transform.position = parentCanvas.transform.TransformPoint(localPoint);
        }
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (!isDragging) return;

        isDragging = false;
        canvasGroup.blocksRaycasts = true;

        OnEndDragEvent.Invoke(this);
        OnCardDropped.Invoke(this);

        gameMediator?.NotifyGameStateChanged();
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (!isDragging) {
            // Always show tooltip on hover
            var tooltip = GetTooltip();
            if (tooltip != null) {
                tooltip.ShowTooltip(this);
            }

            OnPointerEnterHandler?.Invoke();
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (!isDragging) {
            GetTooltip()?.HideTooltip();
            OnPointerExitHandler?.Invoke();
        }
    }

    // Make sure to clean up when the card is destroyed
    protected override void OnDestroy() {
        // Hide tooltip if showing for this card
        if (gameReferences != null) {
            var tooltip = GetTooltip();
            if (tooltip != null) {
                tooltip.HideTooltip();
            }
        }

        if (transform != null) {
            DOTween.Kill(transform);
        }

        CleanupEvents();
        base.OnDestroy();
    }

    private void CleanupEvents() {
        OnBeginDragEvent.RemoveAllListeners();
        OnEndDragEvent.RemoveAllListeners();
        OnCardDropped.RemoveAllListeners();
        OnPointerEnterHandler = null;
        OnPointerExitHandler = null;
    }
}