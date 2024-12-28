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
    private IPlayer player;
    private ICreature linkedCreature;

    public CardUnityEvent OnBeginDragEvent = new CardUnityEvent();
    public CardUnityEvent OnEndDragEvent = new CardUnityEvent();
    public CardUnityEvent OnCardDropped = new CardUnityEvent();
    public Action OnPointerEnterHandler;
    public Action OnPointerExitHandler;

    public Transform OriginalParent => originalParent;
    public ICreature GetLinkedCreature() => linkedCreature;
    public bool IsPlayer1Card() => player?.IsPlayer1() ?? false;
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

    public void Setup(CardData data, IPlayer owner, string creatureTargetId = null) {
        cardData = data;
        player = owner;

        if (cardData is CreatureData) {
            linkedCreature = FindLinkedCreature();
        }

        UpdateUI();
    }

    private ICreature FindLinkedCreature() {
        // Early return if we can't find a creature
        if (cardData == null || player == null || !(cardData is CreatureData)) {
            return null;
        }

        // If we have a valid linked creature, return it
        if (linkedCreature != null) {
            return linkedCreature;
        }

        // Search in player's battlefield
        foreach (var slot in player.Battlefield) {
            if (slot.OccupyingCreature != null && slot.OccupyingCreature.Name == cardData.cardName) {
                return slot.OccupyingCreature;
            }
        }

        // Search in opponent's battlefield
        if (player.Opponent != null) {
            foreach (var slot in player.Opponent.Battlefield) {
                if (slot.OccupyingCreature != null && slot.OccupyingCreature.Name == cardData.cardName) {
                    return slot.OccupyingCreature;
                }
            }
        }

        return null;
    }

    protected override void RegisterEvents() {
        if (gameMediator != null) {
            gameMediator.AddGameStateChangedListener(UpdateUI);
            gameMediator.AddCreatureDamagedListener(OnCreatureDamaged);
            gameMediator.AddCreatureDiedListener(OnCreatureDied);
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            gameMediator.RemoveGameStateChangedListener(UpdateUI);
            gameMediator.RemoveCreatureDamagedListener(OnCreatureDamaged);
            gameMediator.RemoveCreatureDiedListener(OnCreatureDied);
        }
    }

    private void OnCreatureDamaged(ICreature creature, int damage) {
        if (linkedCreature != null && creature.TargetId == linkedCreature.TargetId) {
            Log($"Creature {creature.Name} took {damage} damage, updating UI", LogTag.Creatures | LogTag.UI);
            linkedCreature = creature;
            UpdateUI();
        }
    }

    private void OnCreatureDied(ICreature creature) {
        if (linkedCreature != null && creature.TargetId == linkedCreature.TargetId) {
            Log($"Creature {creature.Name} died, updating UI", LogTag.Creatures | LogTag.UI);
            linkedCreature = null;
            UpdateUI();
        }
    }

    public override void UpdateUI() {
        if (cardData == null) {
            LogWarning("Attempted to update UI with null card data", LogTag.UI | LogTag.Cards);
            return;
        }

        UpdateCardText();
        UpdateCardVisuals();
    }

    private void UpdateCardText() {
        nameText.text = cardData.cardName;
        descriptionText.text = cardData.description ?? string.Empty;

        if (cardData is CreatureData creatureData) {
            statsText.gameObject.SetActive(true);

            if (linkedCreature != null) {
                if (linkedCreature.Health <= 0) {
                    Log($"Creature {linkedCreature.Name} is dead, should be removed", LogTag.Creatures);
                    statsText.text = $"{creatureData.attack}/{creatureData.health}";
                } else {
                    statsText.text = $"{linkedCreature.Attack}/{linkedCreature.Health}";
                }
            } else {
                statsText.text = $"{creatureData.attack}/{creatureData.health}";
            }
        } else {
            statsText.gameObject.SetActive(false);
        }
    }

    private void UpdateCardVisuals() {
        if (cardImage != null && player != null) {
            cardImage.color = player.IsPlayer1()
                ? gameReferences.GetPlayer1CardColor()
                : gameReferences.GetPlayer2CardColor();
        }
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (canvasGroup == null) {
            LogWarning("CanvasGroup is missing, adding it now", LogTag.UI);
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        originalPosition = transform.position;
        originalParent = transform.parent;
        isDragging = true;
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();

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
            OnPointerEnterHandler?.Invoke();
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (!isDragging) {
            OnPointerExitHandler?.Invoke();
        }
    }

    protected override void OnDestroy() {
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