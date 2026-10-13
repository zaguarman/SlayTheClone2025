using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DeckViewUI : UIComponent {
        [SerializeField] private GameObject deckViewPanel;
        [SerializeField] private Transform cardListContent;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI cardsCountText;
        [SerializeField] private int columnsCount = 4;
        [SerializeField] private float cardScale = 0.7f;

        private readonly List<CardController> cardEntries = new List<CardController>();
        private GridLayoutGroup gridLayout;
        private ScrollRect scrollRect;
        private bool viewingDiscardPile = false;

        public GameObject DeckViewPanel => deckViewPanel;
        public Button CloseButton => closeButton;

        // Set up when initialized rather than in Awake: the panel is saved hidden in the scene (so it doesn't cover
        // the table in the editor), and a hidden panel only wakes up the first time it is shown
        private void Setup() {
            if (deckViewPanel == null) {
                LogError("DeckViewPanel is not assigned in the inspector", LogTag.UI | LogTag.Initialization);
            }
            if (cardListContent == null) {
                LogError("CardListContent is not assigned in the inspector", LogTag.UI | LogTag.Initialization);
            }
            if (titleText == null) {
                LogWarning("TitleText is not assigned in the inspector", LogTag.UI | LogTag.Initialization);
            }

            if (closeButton == null) {
                LogError("CloseButton is not assigned in the inspector", LogTag.UI | LogTag.Initialization);
            }
            else {
                closeButton.onClick.AddListener(HideDeckView);
            }

            if (deckViewPanel != null) {
                deckViewPanel.SetActive(false);
            }

            SetupCardGrid();
        }

        private void SetupCardGrid() {
            if (cardListContent == null) {
                LogError("Cannot setup card grid - cardListContent is null", LogTag.UI | LogTag.Initialization);
                return;
            }

            foreach (var layout in cardListContent.GetComponents<LayoutGroup>()) {
                DestroyImmediate(layout);
            }

            gridLayout = cardListContent.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(150, 200);
            gridLayout.spacing = new Vector2(15, 15);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columnsCount;

            var fitter = cardListContent.GetComponent<ContentSizeFitter>();
            if (fitter == null) {
                fitter = cardListContent.gameObject.AddComponent<ContentSizeFitter>();
            }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Log("Card grid setup complete", LogTag.UI | LogTag.Initialization);
        }

        private CardViews cardViews;

        public void Initialize(IGameMediator mediator, IGameManager manager, CardViews views) {
            if (IsInitialized) return;
            cardViews = views ?? throw new System.ArgumentNullException(nameof(views));
            Setup();
            base.Initialize(mediator, manager);
            Log("DeckViewUI initialized", LogTag.UI | LogTag.Initialization);
        }

        protected override void RegisterEvents() { }
        protected override void UnregisterEvents() { }

        public override void UpdateUI(IPlayer player = null) {
            if (!IsInitialized) {
                LogWarning("Cannot update UI - DeckViewUI not initialized", LogTag.UI);
                return;
            }

            if (player == null) {
                LogError("Cannot update UI - Player parameter is null", LogTag.UI);
                ClearCardEntries();
                if (titleText != null) titleText.text = "Error";
                if (cardsCountText != null) cardsCountText.text = "No Player";
                return;
            }

            if (GameManager == null) {
                ClearCardEntries();
                LogError("Cannot update deck view - references missing", LogTag.UI);
                return;
            }

            var dealingService = GameManager.CardDealingService;
            if (viewingDiscardPile) {
                ShowCards(player, "Discard Pile", "Discarded cards", dealingService.GetDiscardPilePreview(player));
            }
            else {
                ShowCards(player, "Deck View", "Cards in deck", dealingService.GetDeckPreview(player));
            }
        }

        public void HideDeckView() {
            if (deckViewPanel == null) {
                LogError("Cannot hide deck view - deckViewPanel is null", LogTag.UI);
                return;
            }

            Tooltip.Existing?.HideTooltip();
            deckViewPanel.SetActive(false);
            Log("Hiding deck view", LogTag.UI);
        }

        public bool IsOpen => deckViewPanel != null && deckViewPanel.activeSelf;
        private readonly List<RectTransform> toggles = new List<RectTransform>();

        /// <summary>Buttons that open and close the window: clicking them isn't a click outside it.</summary>
        public void AddToggle(Button button) {
            if (button != null) toggles.Add((RectTransform)button.transform);
        }

        // A click anywhere outside the window closes it, like the close button
        private void Update() {
            if (!IsOpen) return;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) ClickAt(Input.mousePosition);
        }

        /// <summary>A click at this screen point: outside the window (and any card preview it shows), it closes. Returns whether it did.</summary>
        public bool ClickAt(Vector2 screenPoint) {
            if (!IsOpen) return false;
            var canvas = deckViewPanel.GetComponentInParent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)deckViewPanel.transform, screenPoint, eventCamera)) return false;
            if (Tooltip.Existing != null && Tooltip.Existing.PointerOnTooltip(screenPoint)) return false;
            // The buttons that open and close it do that themselves
            if (toggles.Exists(t => t != null && t.gameObject.activeInHierarchy
                                    && RectTransformUtility.RectangleContainsScreenPoint(t, screenPoint, eventCamera))) return false;
            HideDeckView();
            return true;
        }

        public void ShowDeckCards(IPlayer player) {
            viewingDiscardPile = false;
            UpdateUI(player);
        }

        public void ShowDiscardPileCards(IPlayer player) {
            viewingDiscardPile = true;
            UpdateUI(player);
        }

        private void ShowCards(IPlayer player, string title, string countLabel, List<ICard> cards) {
            ClearCardEntries();

            if (titleText != null) titleText.text = title;
            if (cardsCountText != null) cardsCountText.text = $"{countLabel}: {cards.Count}";

            foreach (var card in cards) {
                CreateCardEntry(card, player);
            }
            Log($"Updated {title} with {cards.Count} cards for player (TargetID: {player.TargetId.ToUpper()})", LogTag.UI | LogTag.Cards);

            if (scrollRect != null) {
                scrollRect.normalizedPosition = new Vector2(0, 1); // Back to the top
            }
        }

        private void CreateCardEntry(ICard card, IPlayer owner) {
            if (card == null || cardListContent == null) {
                LogError("Cannot create card entry - card or content is null", LogTag.Cards);
                return;
            }

            var cardController = cardViews.Create(card, owner, cardListContent);
            if (cardController == null) {
                LogError($"Failed to create card controller for {card.Name} (TargetID: {card.TargetId.ToUpper()})", LogTag.Cards);
                return;
            }

            DisableCardInteractions(cardController);
            cardController.transform.localScale = new Vector3(cardScale, cardScale, cardScale);
            cardEntries.Add(cardController);
        }

        // The cards only show tooltips: the CardController is disabled and an interceptor swallows drags
        private void DisableCardInteractions(CardController cardController) {
            var tooltip = Tooltip.Shared;
            tooltip.EnableTooltipOnly(cardController);

            var interceptor = cardController.gameObject.AddComponent<DeckViewCardInterceptor>();
            cardController.enabled = false;
            interceptor.Initialize(cardController, tooltip);

            var canvasGroup = cardController.GetComponent<CanvasGroup>();
            if (canvasGroup != null) {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
        }

        private void ClearCardEntries() {
            Tooltip.Existing?.HideTooltip();

            foreach (var entry in cardEntries) {
                if (entry != null) Destroy(entry.gameObject);
            }
            cardEntries.Clear();
        }

        protected override void OnDestroy() {
            if (closeButton != null) {
                closeButton.onClick.RemoveListener(HideDeckView);
            }

            ClearCardEntries();
            base.OnDestroy();
        }
    }

    // Lets deck view cards show their tooltip while preventing any drag
    public class DeckViewCardInterceptor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                           IBeginDragHandler, IDragHandler, IEndDragHandler {
        private CardController cardController;
        private Tooltip tooltip;

        public void Initialize(CardController controller, Tooltip tooltip) {
            cardController = controller;
            this.tooltip = tooltip;
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (tooltip != null && cardController != null) {
                tooltip.ShowTooltip(cardController);
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (tooltip != null) {
                tooltip.ReleaseCard(cardController);
            }
        }

        public void OnBeginDrag(PointerEventData eventData) {
            eventData.pointerDrag = null;
            eventData.Use();
            eventData.eligibleForClick = false;
        }

        public void OnDrag(PointerEventData eventData) {
            eventData.pointerDrag = null;
            eventData.Use();
        }

        public void OnEndDrag(PointerEventData eventData) {
            eventData.Use();
        }
    }
}
