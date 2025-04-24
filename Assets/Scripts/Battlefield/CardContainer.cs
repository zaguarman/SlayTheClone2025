using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using static DebugLogger;

public abstract class CardContainer : UIComponent, IDropHandler, IPointerEnterHandler, IPointerExitHandler {
    [SerializeField] protected ContainerSettings settings = new ContainerSettings();
    [SerializeField] protected bool acceptPlayer1Cards = true;
    [SerializeField] protected bool acceptPlayer2Cards = true;
    [SerializeField] protected Color defaultColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
    [SerializeField] protected Color validDropColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] protected Color invalidDropColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] protected Color hoverColor = new Color(1f, 1f, 0f, 0.5f);

    protected List<CardController> cards = new List<CardController>();

    protected RectTransform containerRect;
    protected CardDropZone dropZoneHandler;

    protected virtual void SetupDropZone() {
        var dropZoneImage = GetComponent<Image>();
        if (dropZoneImage == null) {
            dropZoneImage = gameObject.AddComponent<Image>();
        }

        dropZoneHandler = new CardDropZone(
            dropZoneImage,
            defaultColor,
            validDropColor,
            invalidDropColor,
            hoverColor,
            acceptPlayer1Cards,
            acceptPlayer2Cards
        );

        dropZoneHandler.OnCardDropped.AddListener(HandleCardDropped);
        dropZoneHandler.OnPointerEnterEvent.AddListener(HandlePointerEnter);
        dropZoneHandler.OnPointerExitEvent.AddListener(HandlePointerExit);
    }

    public virtual bool CanAcceptCard(CardController card) {
        return dropZoneHandler?.CanAcceptCard(card) ?? false;
    }

    // Consolidated drop handling
    public void OnDrop(PointerEventData eventData) {
        var card = eventData.pointerDrag?.GetComponent<CardController>();
        if (card != null && CanAcceptCard(card)) {
            HandleCardDropped(card);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) {
        dropZoneHandler?.OnPointerEnter(eventData);
    }

    public void OnPointerExit(PointerEventData eventData) {
        dropZoneHandler?.OnPointerExit(eventData);
    }

    protected virtual void HandleCardDropped(CardController card) {
        // Override in derived classes to handle specific drop behavior
    }

    protected virtual void HandlePointerEnter(PointerEventData eventData) {
        // Override in derived classes if needed
    }

    protected virtual void HandlePointerExit(PointerEventData eventData) {
        // Override in derived classes if needed
    }

    public override void Initialize(IPlayer player, IGameMediator mediator, IGameReferences references) {
        // Call base UIComponent Initialize FIRST
        base.Initialize(player, mediator, references);

        // Now do CardContainer specific setup
        containerRect = GetComponent<RectTransform>();
        if (containerRect == null) {
            containerRect = gameObject.AddComponent<RectTransform>();
        }

        SetupDropZone();
        UpdateLayout();
        // IsInitialized is set by base class
        UpdateUI(Player); // Use Player property from base class
    }

    protected virtual void UpdateLayout() {
        // Original layout code remains here
        Vector2[] positions = CalculateCardPositions();
        RepositionCards(positions);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
    }

    protected Vector2[] CalculateCardPositions() {
        Vector2[] positions = new Vector2[cards.Count];
        if (cards.Count == 0) return positions;

        CalculatePositions(positions);

        return positions;
    }

    private void CalculatePositions(Vector2[] positions) {
        if (cards.Count == 0) return;

        // Calculate total width of all cards including spacing
        float totalWidth = (cards.Count - 1) * settings.spacing;
        // Calculate the starting X position to center the group
        float startX = -totalWidth / 2;

        for (int i = 0; i < cards.Count; i++) {
            float position = startX + (settings.spacing * i);
            positions[i] = new Vector2(position, 0);
        }
    }

    protected virtual void SetupCardTransform(CardController card, Vector2 position) {
        if (card == null) return;

        var cardRect = card.GetComponent<RectTransform>();
        if (cardRect != null) {
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = position;
        }
    }

    protected void RepositionCards(Vector2[] positions) {
        for (int i = 0; i < cards.Count; i++) {
            if (cards[i] != null) {
                SetupCardTransform(cards[i], positions[i]);
            }
        }
    }

    protected virtual void CalculateHorizontalPositions(Vector2[] positions) {
        for (int i = 0; i < cards.Count; i++) {
            positions[i] = new Vector2(
                settings.offset + (settings.spacing * i),
                0
            );
        }
    }

    protected virtual void CalculateVerticalPositions(Vector2[] positions) {
        for (int i = 0; i < cards.Count; i++) {
            positions[i] = new Vector2(
                0,
                -(settings.offset + (settings.spacing * i))
            );
        }
    }

    protected virtual void CalculateGridPositions(Vector2[] positions) {
        for (int i = 0; i < cards.Count; i++) {
            int row = i / settings.gridColumns;
            int col = i % settings.gridColumns;
            positions[i] = new Vector2(
                settings.offset + (settings.gridCellSize.x * col),
                -(settings.offset + (settings.gridCellSize.y * row))
            );
        }
    }

    protected virtual Vector2 CalculateGridSize() {
        int rows = Mathf.CeilToInt((float)cards.Count / settings.gridColumns);
        return new Vector2(
            settings.offset + (settings.gridCellSize.x * settings.gridColumns),
            settings.offset + (settings.gridCellSize.y * rows)
        );
    }

    protected virtual void SetupCardEventHandlers(CardController card) {
        if (card == null) return;

        card.OnBeginDragEvent.AddListener(OnCardBeginDrag);
        card.OnEndDragEvent.AddListener(OnCardEndDrag);
        card.OnCardDropped.AddListener(OnCardDropped);
        card.OnPointerEnterHandler += () => OnCardHoverEnter(card);
        card.OnPointerExitHandler += () => OnCardHoverExit(card);
    }

    protected virtual void CleanupCardEventHandlers(CardController card) {
        if (card == null) return;

        card.OnBeginDragEvent.RemoveListener(OnCardBeginDrag);
        card.OnEndDragEvent.RemoveListener(OnCardEndDrag);
        card.OnCardDropped.RemoveListener(OnCardDropped);
        card.OnPointerEnterHandler = null;
        card.OnPointerExitHandler = null;
    }

    protected virtual void OnCardBeginDrag(CardController card) {
        card.transform.SetAsLastSibling();
        card.transform.localScale = Vector3.one;
    }

    protected virtual void OnCardEndDrag(CardController card) {
        card.transform.localScale = Vector3.one;
        UpdateLayout();
    }

    protected virtual void OnCardDropped(CardController card) {
        // Override in derived classes
    }

    protected virtual void OnCardHoverEnter(CardController card) {
        var rectTransform = card.GetComponent<RectTransform>();
        if (rectTransform != null) {
            var currentPos = rectTransform.anchoredPosition;
            rectTransform.anchoredPosition = new Vector2(currentPos.x, currentPos.y + settings.cardHoverOffset);
        }
    }

    protected virtual void OnCardHoverExit(CardController card) {
        UpdateLayout();
    }

    protected virtual CardController CreateCard(ICard card) {
        // Use the gameMediator and gameReferences fields inherited from UIComponent

        // Get the original CardData for this card
        CardData originalData = null;

        // Try to find the original card data in the deck
        if (Player != null && card.CardId != null) {
            // Since we don't have direct access to the deck's original data,
            // we'll create a temporary CardData based on the card's base properties
            if (card is ICreature creature) {
                var tempData = ScriptableObject.CreateInstance<CreatureData>();
                tempData.cardId = card.CardId;
                tempData.cardName = card.Name;
                tempData.description = card.Description;
                tempData.attack = creature.BaseAttack;
                tempData.health = creature.BaseHealth;
                tempData.speed = creature.BaseSpeed;
                originalData = tempData;
            } else if (card is Spell spell) {
                var tempData = ScriptableObject.CreateInstance<SpellData>();
                tempData.cardId = card.CardId;
                tempData.cardName = card.Name;
                tempData.description = card.Description;
                tempData.defaultTargetType = spell.DefaultTargetType;
                originalData = tempData;
            }
        }

        // If we couldn't create original data, log a warning
        if (originalData == null) {
            LogWarning($"Could not create CardData for {card.Name}, display may not be accurate", LogTag.Cards);
        }

        return CardFactory.CreateCardController(card, originalData, Player, transform, gameMediator, gameReferences);
    }

    public virtual void AddCard(CardController card) {
        if (card == null) return;
        cards.Add(card);
        SetupCardEventHandlers(card);
        UpdateLayout();
    }

    public virtual void RemoveCard(CardController card) {
        if (card == null) return;
        CleanupCardEventHandlers(card);
        cards.Remove(card);
        UpdateLayout();
    }

    protected override void OnDestroy() {
        dropZoneHandler?.Cleanup();
        dropZoneHandler = null;
        foreach (var card in cards.ToList()) {
            if (card != null) {
                CleanupCardEventHandlers(card);
                Destroy(card.gameObject);
            }
        }
        cards.Clear();
        base.OnDestroy();
    }
}