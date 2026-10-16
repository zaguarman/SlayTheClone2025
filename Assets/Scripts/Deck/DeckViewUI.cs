using DG.Tweening;
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
        private const int ColumnsCount = 4; // The cards fill the window's width, big enough to read at a glance (see CardColumns)

        private readonly List<CardController> cardEntries = new List<CardController>();
        private GridLayoutGroup gridLayout;
        private CardColumns columns;
        private ScrollRect scrollRect;
        private bool viewingDiscardPile = false;
        private bool viewingEnemy = false; // The other side's piles (only while Fog of War is off)

        public GameObject DeckViewPanel => deckViewPanel;
        public Button CloseButton => closeButton;

        /// <summary>
        /// A card must be picked (Send For) before anything else is played or the Day ends: the window can be hidden to
        /// look at the table (hovering it works), but not closed (the user, 2026-10-15).
        /// </summary>
        public static bool PickPending { get; private set; }
        /// <summary>The pick's window is tucked away to see the table; Show brings it back.</summary>
        public bool PickHidden { get; private set; }
        public Button HideButton => hideButton;
        public Button ShowButton => showButton;
        private Button hideButton, showButton;

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
            gridLayout.spacing = new Vector2(20, 22);
            gridLayout.padding = new RectOffset(16, 16, 16, 16);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            columns = CardColumns.AddTo(gridLayout, ColumnsCount);

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
                ShowCards(player, viewingEnemy ? "Their Discard Pile" : "Discard Pile", "Discarded cards", dealingService.GetDiscardPilePreview(player));
            }
            else {
                ShowCards(player, viewingEnemy ? "Their Deck" : "Deck View", "Cards in deck", dealingService.GetDeckPreview(player));
            }
        }

        public void HideDeckView() {
            if (deckViewPanel == null) {
                LogError("Cannot hide deck view - deckViewPanel is null", LogTag.UI);
                return;
            }
            if (IsPicking) return; // A pick isn't closed: it's made (or called off, see CancelPick)
            EndPick();

            Tooltip.Existing?.HideTooltip();
            deckViewPanel.SetActive(false);
            ArrowIndicator.Suppressed = false;
            Log("Hiding deck view", LogTag.UI);
        }

        public bool IsOpen => deckViewPanel != null && deckViewPanel.activeSelf;
        private readonly List<RectTransform> toggles = new List<RectTransform>();

        /// <summary>Buttons that open and close the window: clicking them isn't a click outside it.</summary>
        public void AddToggle(Button button) {
            if (button != null) toggles.Add((RectTransform)button.transform);
        }

        // A click anywhere outside the window closes it, like the close button. The battle arrows hide while it's open
        private void Update() {
            ArrowIndicator.Suppressed = IsOpen;
            if (!IsOpen) return;
            if (GameInput.ClickedThisFrame) ClickAt(GameInput.PointerPosition);
        }

        /// <summary>A click at this screen point: outside the window (and any card preview it shows), it closes. Returns whether it did.</summary>
        public bool ClickAt(Vector2 screenPoint) {
            if (!IsOpen || IsPicking) return false; // A pick can't be closed: only hidden
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

        // Send For: the window shows what can be fetched, and clicking a card picks it
        private System.Action<ICard> onPick;
        private System.Action onPickClosed;

        /// <summary>Whether the window is open to pick a card (Send For), rather than to look.</summary>
        public bool IsPicking => onPick != null;

        /// <summary>Opens the window on these cards; clicking one calls <paramref name="picked"/> and closes it. Closed without a pick: <paramref name="closed"/>.</summary>
        public void ShowPick(IPlayer player, string title, List<ICard> cards, System.Action<ICard> picked, System.Action closed, string hint = "Click one to send for it") {
            if (deckViewPanel == null || !IsInitialized) return;
            EndPick();
            ShowCards(player, title, hint, cards);
            onPick = picked;
            onPickClosed = closed;
            PickPending = true;
            foreach (var entry in cardEntries) {
                var interceptor = entry.GetComponent<DeckViewCardInterceptor>();
                if (interceptor != null) interceptor.Clicked = Pick;
            }
            deckViewPanel.SetActive(true);
            UITheme.Lift(deckViewPanel, UITheme.WindowsLayer); // Over the hands too
            SetPickHidden(false);
        }

        /// <summary>Tucks the pick's window away to look at the table, or brings it back. Only while picking.</summary>
        public void SetPickHidden(bool hidden) {
            EnsurePickButtons();
            PickHidden = hidden && IsPicking;
            if (IsPicking) deckViewPanel.SetActive(!PickHidden);
            ArrowIndicator.Suppressed = deckViewPanel.activeSelf; // Its Update stops while it's hidden
            if (closeButton != null) closeButton.gameObject.SetActive(!IsPicking);
            if (hideButton != null) hideButton.gameObject.SetActive(IsPicking && !PickHidden);
            if (showButton != null) {
                bool waiting = IsPicking && PickHidden;
                showButton.gameObject.SetActive(waiting);
                Breathe(showButton.transform, waiting); // A pick is waiting: it breathes slowly, like a queued card, so it's noticed
            }
            if (PickHidden) Tooltip.Existing?.HideTooltip();
        }

        // Hide sits where the close button is (a pick has none); Show floats over the table while it's hidden
        private void EnsurePickButtons() {
            if (hideButton != null || closeButton == null) return;
            hideButton = MakeLike(closeButton, closeButton.transform.parent, "PickHideButton", "Hide", () => SetPickHidden(true));
            var hideRect = (RectTransform)hideButton.transform;
            hideRect.sizeDelta = new Vector2(130f, ((RectTransform)closeButton.transform).sizeDelta.y);
            UITheme.StyleButton(hideButton);
            // Show takes Hide's place, so the toggle stays under the pointer; it lives beside the window, as that hides
            showButton = MakeLike(closeButton, deckViewPanel.transform.parent, "PickShowButton", "Show", () => SetPickHidden(false));
            var showRect = (RectTransform)showButton.transform;
            showRect.pivot = hideRect.pivot;
            showRect.sizeDelta = new Vector2(150f, hideRect.sizeDelta.y);
            var parentScale = showRect.parent.lossyScale;
            showRect.localScale = new Vector3(hideRect.lossyScale.x / parentScale.x, hideRect.lossyScale.y / parentScale.y, 1f);
            showRect.rotation = hideRect.rotation;
            showRect.position = hideRect.position;
            UITheme.StyleButton(showButton, UITheme.ButtonKind.Primary);
            showButton.gameObject.SetActive(false);
            hideButton.gameObject.SetActive(false);
        }

        private Vector3 showScale;

        private void Breathe(Transform target, bool on) {
            DOTween.Kill(target);
            if (showScale == Vector3.zero) showScale = target.localScale;
            target.localScale = showScale;
            if (on) target.DOScale(showScale * 1.08f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetTarget(target).SetLink(target.gameObject);
        }

        private static Button MakeLike(Button model, Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick) {
            var copy = Instantiate(model.gameObject, parent, false);
            copy.name = name;
            var button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);
            var text = copy.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) {
                text.text = label;
                text.enableAutoSizing = true;
                text.fontSizeMin = 14;
                text.fontSizeMax = 26;
            }
            copy.transform.SetAsLastSibling();
            return button;
        }

        /// <summary>Calls the pick off (the card that asked for it was taken back): the window closes.</summary>
        public void CancelPick() {
            if (!IsPicking) return;
            EndPick();
            HideDeckView();
        }

        /// <summary>Picks this card, as clicking it does.</summary>
        public void Pick(ICard card) {
            var picked = onPick;
            onPick = null;
            onPickClosed = null;
            PickPending = false;
            HideDeckView();
            picked?.Invoke(card); // It may open the window again, for the next pick
        }

        // Closed without a pick: whoever asked is told
        private void EndPick() {
            var closed = onPickClosed;
            onPick = null;
            onPickClosed = null;
            PickPending = false;
            if (hideButton != null) SetPickHidden(false);
            closed?.Invoke();
        }

        public void ShowDeckCards(IPlayer player, bool enemy = false) {
            if (IsPicking) return; // The pick comes first
            viewingDiscardPile = false;
            viewingEnemy = enemy;
            UpdateUI(player);
        }

        public void ShowDiscardPileCards(IPlayer player, bool enemy = false) {
            if (IsPicking) return;
            viewingDiscardPile = true;
            viewingEnemy = enemy;
            UpdateUI(player);
        }

        private void ShowCards(IPlayer player, string title, string countLabel, List<ICard> cards) {
            ClearCardEntries();

            if (titleText != null) titleText.text = title;
            if (cardsCountText != null) cardsCountText.text = $"{countLabel}: {cards.Count}";

            foreach (var card in cards) {
                CreateCardEntry(card, player);
            }
            if (columns != null) columns.Fit();
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

            // Each card in a cell of the grid, centered, scaled to fill it (the grid sizes the cells, not the cards)
            var cell = new GameObject(card.Name, typeof(RectTransform));
            cell.transform.SetParent(cardListContent, false);
            var cardController = cardViews.Create(card, owner, cell.transform);
            if (cardController == null) {
                LogError($"Failed to create card controller for {card.Name} (TargetID: {card.TargetId.ToUpper()})", LogTag.Cards);
                Destroy(cell);
                return;
            }
            var rect = (RectTransform)cardController.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            DisableCardInteractions(cardController);
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
                if (entry == null) continue;
                var cell = entry.transform.parent;
                Destroy(cell != null && cell != cardListContent ? cell.gameObject : entry.gameObject);
            }
            cardEntries.Clear();
        }

        protected override void OnDestroy() {
            PickPending = false;
            ArrowIndicator.Suppressed = false;
            if (closeButton != null) {
                closeButton.onClick.RemoveListener(HideDeckView);
            }

            ClearCardEntries();
            base.OnDestroy();
        }
    }

    // Lets deck view cards show their tooltip while preventing any drag
    public class DeckViewCardInterceptor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
                                           IBeginDragHandler, IDragHandler, IEndDragHandler {
        private CardController cardController;
        private Tooltip tooltip;
        /// <summary>Picking (Send For): what a click on the card does.</summary>
        public System.Action<ICard> Clicked;

        public void OnPointerClick(PointerEventData eventData) {
            if (Clicked != null && cardController != null) Clicked(cardController.GetLinkedCardInstance());
        }

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
