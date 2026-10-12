using TMPro;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    // Wires the deck and discard buttons to the DeckViewUI panel; at most one of the two views is open
    public class DeckViewController : UIComponent {
        private enum OpenView { None, Deck, Discard }

        private Button deckViewButton;
        private Button discardViewButton;
        private TextMeshProUGUI deckButtonText;
        private TextMeshProUGUI discardButtonText;
        private DeckViewUI deckViewUI;

        private OpenView openView = OpenView.None;

        // The deck view panel must already be initialized (GameUI does it)
        public void Initialize(IGameMediator mediator, IGameManager manager, Button deckButton, Button discardButton, DeckViewUI view) {
            if (IsInitialized) return;

            deckViewButton = deckButton;
            discardViewButton = discardButton;
            deckViewUI = view;
            base.Initialize(mediator, manager);

            if (SetupUIReferences()) SetupButtons();
            Log("DeckViewController initialized with dependencies", LogTag.Initialization);
        }

        private bool SetupUIReferences() {
            if (deckViewButton == null || discardViewButton == null || deckViewUI == null) {
                LogError("Deck view button or DeckViewUI reference is null", LogTag.UI | LogTag.Initialization);
                return false;
            }

            deckButtonText = GetButtonText(deckViewButton, "Deck");
            discardButtonText = GetButtonText(discardViewButton, "Discard");
            UpdateButtonTexts();

            deckViewButton.gameObject.SetActive(true);
            discardViewButton.gameObject.SetActive(true);

            if (deckViewUI.DeckViewPanel != null) {
                deckViewUI.DeckViewPanel.SetActive(false);
            }
            else {
                LogError("DeckViewPanel is null", LogTag.UI | LogTag.Initialization);
            }
            return true;
        }

        private static TextMeshProUGUI GetButtonText(Button button, string label) {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text == null) LogWarning($"{label} button text component not found", LogTag.UI);
            if (button.GetComponent<Image>() == null) LogError($"{label} view button is missing Image component", LogTag.UI | LogTag.Initialization);
            return text;
        }

        private void SetupButtons() {
            deckViewButton.onClick.RemoveAllListeners();
            discardViewButton.onClick.RemoveAllListeners();
            deckViewButton.onClick.AddListener(ToggleDeckView);
            discardViewButton.onClick.AddListener(ToggleDiscardView);

            if (deckViewUI.CloseButton != null) {
                deckViewUI.CloseButton.onClick.AddListener(OnCloseButtonClicked);
            }
            else {
                LogWarning("Cannot setup close button - closeButton is null", LogTag.UI | LogTag.Initialization);
            }
        }

        public void ToggleDeckView() => ToggleView(OpenView.Deck);

        public void ToggleDiscardView() => ToggleView(OpenView.Discard);

        // Opening one view replaces the other; toggling the open view closes the panel
        private void ToggleView(OpenView view) {
            if (!IsInitialized) {
                LogError($"Cannot toggle {view} view - not fully initialized yet", LogTag.UI);
                return;
            }

            if (openView == view) {
                SetOpenView(OpenView.None);
                HideDeckView();
                return;
            }

            SetOpenView(view);
            if (!ShowDeckView(view == OpenView.Discard)) {
                SetOpenView(OpenView.None); // Couldn't show it
            }
        }

        private void OnCloseButtonClicked() {
            SetOpenView(OpenView.None);
        }

        private void SetOpenView(OpenView view) {
            openView = view;
            UpdateButtonTexts();
        }

        private void UpdateButtonTexts() {
            if (deckButtonText != null) deckButtonText.text = openView == OpenView.Deck ? "Hide Deck" : "View Deck";
            if (discardButtonText != null) discardButtonText.text = openView == OpenView.Discard ? "Hide Discard" : "View Discard";
        }

        /// <returns>False if the view could not be shown.</returns>
        private bool ShowDeckView(bool showDiscard) {
            if (deckViewUI == null || deckViewUI.DeckViewPanel == null || !deckViewUI.IsInitialized) {
                LogError("Cannot show deck view - DeckViewUI or its panel is missing, or it was not initialized", LogTag.UI);
                return false;
            }

            // The screen's player: player 1 unless the sides were switched (F10)
            var player = FogOfWar.ViewerOf(GameManager?.Player1);
            if (player == null) {
                LogError("Cannot show view - no active player found", LogTag.UI);
                return false;
            }

            try {
                if (showDiscard) deckViewUI.ShowDiscardPileCards(player);
                else deckViewUI.ShowDeckCards(player);

                deckViewUI.DeckViewPanel.SetActive(true);
                Log($"Made {(showDiscard ? "discard" : "deck")} view panel visible for {player.Name}", LogTag.UI);
                return true;
            }
            catch (System.Exception e) {
                LogError($"Error showing view: {e.Message}\n{e.StackTrace}", LogTag.UI);
                return false;
            }
        }

        private void HideDeckView() {
            if (deckViewUI != null && deckViewUI.DeckViewPanel != null) {
                deckViewUI.DeckViewPanel.SetActive(false);
            }
            else {
                LogError("Cannot hide deck view - references missing", LogTag.UI);
            }
        }

        // The panel can also be closed from outside (e.g. its own close button)
        private void Update() {
            if (!IsInitialized || openView == OpenView.None) return;

            if (deckViewUI != null && deckViewUI.DeckViewPanel != null && !deckViewUI.DeckViewPanel.activeSelf) {
                SetOpenView(OpenView.None);
                Log("Panel state changed externally, updated button states", LogTag.UI);
            }
        }

        protected override void OnDestroy() {
            if (deckViewButton != null) deckViewButton.onClick.RemoveListener(ToggleDeckView);
            if (discardViewButton != null) discardViewButton.onClick.RemoveListener(ToggleDiscardView);
            if (deckViewUI != null && deckViewUI.CloseButton != null) {
                deckViewUI.CloseButton.onClick.RemoveListener(OnCloseButtonClicked);
            }

            base.OnDestroy();
        }

        protected override void RegisterEvents() { }
        protected override void UnregisterEvents() { }
        public override void UpdateUI(IPlayer player = null) { }
    }
}
