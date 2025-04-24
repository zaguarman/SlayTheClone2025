// File: Scripts/Deck/DeckViewController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static DebugLogger;

public class DeckViewController : UIComponent {
    // Remove these serialized fields and use GameReferences instead
    private Button deckViewButton;
    private Button discardViewButton;

    private TextMeshProUGUI deckButtonText;
    private TextMeshProUGUI discardButtonText;
    private DeckViewUI deckViewUI;
    // GameManager, gameMediator, and gameReferences are inherited from UIComponent

    private bool isDeckViewOpen = false;
    private bool isDiscardViewOpen = false;
    // isInitialized is inherited from UIComponent base class

    // Override Initialize from UIComponent
    public override void Initialize(IGameMediator mediator, IGameReferences references) {
        if (IsInitialized) return; // Prevent double initialization

        // Call base Initialize FIRST to set mediator/references/manager fields
        base.Initialize(mediator, references); // Pass null for player

        if (gameManager != null && gameReferences.AreReferencesValid()) {
            GetUIReferences(); // Get references using the now-set gameReferences
            SetupButtons();    // Setup using the obtained references
            // Base class sets IsInitialized = true
            Log("DeckViewController initialized with dependencies", LogTag.Initialization);
        } else {
            LogError("Cannot initialize DeckViewController - GameManager not ready or references invalid", LogTag.Initialization);
            enabled = false; // Disable component if init fails
        }
    }

    private void GetUIReferences() {
        // Always get references from GameReferences
        deckViewButton = gameReferences.GetDeckViewButton();
        discardViewButton = gameReferences.GetDiscardViewButton();
        deckViewUI = gameReferences.GetDeckViewUI();

        if (deckViewButton == null) {
            LogError("DeckViewButton reference is null", LogTag.UI | LogTag.Initialization);
            return;
        }

        if (discardViewButton == null) {
            LogError("DiscardViewButton reference is null", LogTag.UI | LogTag.Initialization);
            return;
        }

        if (deckViewUI == null) {
            LogError("DeckViewUI reference is null", LogTag.UI | LogTag.Initialization);
            return;
        }

        // Get the button text components
        deckButtonText = deckViewButton.GetComponentInChildren<TextMeshProUGUI>();
        discardButtonText = discardViewButton.GetComponentInChildren<TextMeshProUGUI>();

        if (deckButtonText == null) {
            LogWarning("Deck button text component not found", LogTag.UI);
        } else {
            deckButtonText.text = "View Deck";
        }

        if (discardButtonText == null) {
            LogWarning("Discard button text component not found", LogTag.UI);
        } else {
            discardButtonText.text = "View Discard";
        }

        // Ensure UI elements are active
        deckViewButton.gameObject.SetActive(true);
        discardViewButton.gameObject.SetActive(true);

        if (deckViewUI.deckViewPanel != null) {
            deckViewUI.deckViewPanel.SetActive(false);
            Log("Ensured deck view panel is hidden on initialization", LogTag.UI | LogTag.Initialization);
        } else {
            LogError("DeckViewPanel is null", LogTag.UI | LogTag.Initialization);
        }
    }

    private void SetupButtons() {
        Log("Setting up deck view buttons", LogTag.UI | LogTag.Initialization);

        if (deckViewButton == null || discardViewButton == null) {
            LogError("Cannot setup buttons - button references are null", LogTag.UI | LogTag.Initialization);
            return;
        }

        // First verify if the buttons have the Image component they need
        if (deckViewButton.GetComponent<Image>() == null) {
            LogError("DeckViewButton is missing Image component", LogTag.UI | LogTag.Initialization);
        }

        if (discardViewButton.GetComponent<Image>() == null) {
            LogError("DiscardViewButton is missing Image component", LogTag.UI | LogTag.Initialization);
        }

        // Remove any existing listeners first
        deckViewButton.onClick.RemoveAllListeners();
        discardViewButton.onClick.RemoveAllListeners();

        // Add our toggle listeners
        deckViewButton.onClick.AddListener(ToggleDeckView);
        discardViewButton.onClick.AddListener(ToggleDiscardView);

        Log("Button listeners added", LogTag.UI | LogTag.Initialization);

        // Configure the close button to update our state
        if (deckViewUI != null && deckViewUI.closeButton != null) {
            deckViewUI.closeButton.onClick.AddListener(OnCloseButtonClicked);
            Log("Added listener to close button", LogTag.UI | LogTag.Initialization);
        } else {
            LogWarning("Cannot setup close button - closeButton is null", LogTag.UI | LogTag.Initialization);
        }
    }

    public void ToggleDeckView() {
        if (!IsInitialized) { // Use base class property
            LogError("Cannot toggle deck view - not fully initialized yet", LogTag.UI);
            return;
        }

        // If discard view is open, close it first
        if (isDiscardViewOpen) {
            isDiscardViewOpen = false;
            UpdateDiscardButtonText();
        }

        isDeckViewOpen = !isDeckViewOpen;
        UpdateDeckButtonText();

        if (isDeckViewOpen) {
            ShowDeckView(false);
        } else {
            HideDeckView();
        }
    }

    public void ToggleDiscardView() {
        if (!IsInitialized) { // Use base class property
            LogError("Cannot toggle discard view - not fully initialized yet", LogTag.UI);
            return;
        }

        // If deck view is open, close it first
        if (isDeckViewOpen) {
            isDeckViewOpen = false;
            UpdateDeckButtonText();
        }

        isDiscardViewOpen = !isDiscardViewOpen;
        UpdateDiscardButtonText();

        if (isDiscardViewOpen) {
            ShowDeckView(true);
        } else {
            HideDeckView();
        }
    }

    private void OnCloseButtonClicked() {
        if (isDeckViewOpen) {
            isDeckViewOpen = false;
            UpdateDeckButtonText();
        }

        if (isDiscardViewOpen) {
            isDiscardViewOpen = false;
            UpdateDiscardButtonText();
        }

        Log("Close button clicked, updated button states", LogTag.UI);
    }

    private void ShowDeckView(bool showDiscard) {
        Log($"ShowDeckView called with showDiscard={showDiscard}", LogTag.UI);

        // Check essential references obtained during initialization
        if (deckViewUI == null || deckViewUI.deckViewPanel == null || gameMediator == null || gameReferences == null) {
            LogError("Cannot show deck view - references missing (DeckViewUI, Mediator, or References)", LogTag.UI);
            return;
        }

        var activePlayer = GetActivePlayer();
        if (activePlayer != null) {
            // Initialize DeckViewUI using the NEW signature
            if (!deckViewUI.IsInitialized) {
                // DeckViewUI doesn't need a specific player for initialization
                deckViewUI.Initialize(gameMediator, gameReferences);
                Log("Initialized DeckViewUI with dependencies", LogTag.UI);
            }

            try {
                // Set viewing state and update UI accordingly
                // Pass the active player explicitly to DeckViewUI methods
                if (showDiscard) {
                    deckViewUI.ShowDiscardPileCards(activePlayer); // Pass the active player
                } else {
                    deckViewUI.ShowDeckCards(activePlayer); // Pass the active player
                }

                deckViewUI.deckViewPanel.SetActive(true);
                Log($"Made {(showDiscard ? "discard" : "deck")} view panel visible for {(activePlayer.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.UI);
            } catch (System.Exception e) {
                 LogError($"Error showing view: {e.Message}\n{e.StackTrace}", LogTag.UI);
                 // Revert state
                 if (showDiscard) isDiscardViewOpen = false; else isDeckViewOpen = false;
                 UpdateDeckButtonText(); UpdateDiscardButtonText();
            }
        } else {
            LogError("Cannot show view - no active player found", LogTag.UI);
            // Revert our state since we couldn't show the view
            if (showDiscard) {
                isDiscardViewOpen = false;
                UpdateDiscardButtonText();
            } else {
                isDeckViewOpen = false;
                UpdateDeckButtonText();
            }
        }
    }

    private void HideDeckView() {
        Log("HideDeckView called", LogTag.UI);

        if (deckViewUI != null && deckViewUI.deckViewPanel != null) {
            deckViewUI.deckViewPanel.SetActive(false);
            Log("Hiding deck view panel", LogTag.UI);
        } else {
            LogError("Cannot hide deck view - references missing", LogTag.UI);
        }
    }

    private void UpdateDeckButtonText() {
        if (deckButtonText != null) {
            deckButtonText.text = isDeckViewOpen ? "Hide Deck" : "View Deck";
            Log($"Updated deck button text to '{deckButtonText.text}'", LogTag.UI);
        } else {
            LogWarning("Cannot update deck button text - text component is null", LogTag.UI);
        }
    }

    private void UpdateDiscardButtonText() {
        if (discardButtonText != null) {
            discardButtonText.text = isDiscardViewOpen ? "Hide Discard" : "View Discard";
            Log($"Updated discard button text to '{discardButtonText.text}'", LogTag.UI);
        } else {
            LogWarning("Cannot update discard button text - text component is null", LogTag.UI);
        }
    }

    private IPlayer GetActivePlayer() {
        // Use the inherited gameManager field
        if (gameManager != null) {
            // --- REFINED LOGIC ---
            // As per user clarification, Player 1 is always the context for deck/discard viewing.
            var player1 = gameManager.Player1;
            if (player1 == null) {
                LogError("Player1 is null in GameManager! Cannot determine active player for deck view.", LogTag.UI | LogTag.Initialization);
            }
            return player1; // Always return Player 1
            // --- END REFINED LOGIC ---
        }

        LogError("GameManager is null when trying to get active player for deck view", LogTag.UI | LogTag.Initialization);
        return null;
    }

    // Check if the panel state changed externally
    private void Update() {
        if (!IsInitialized) return; // Use base property

        if (deckViewUI != null && deckViewUI.deckViewPanel != null) {
            bool isPanelActive = deckViewUI.deckViewPanel.activeSelf;

            // If panel was closed externally
            if (!isPanelActive && (isDeckViewOpen || isDiscardViewOpen)) {
                if (isDeckViewOpen) {
                    isDeckViewOpen = false;
                    UpdateDeckButtonText();
                }

                if (isDiscardViewOpen) {
                    isDiscardViewOpen = false;
                    UpdateDiscardButtonText();
                }

                Log("Panel state changed externally, updated button states", LogTag.UI);
            }
        }
    }

    protected override void OnDestroy() { // Mark as override
        if (deckViewButton != null) {
            deckViewButton.onClick.RemoveListener(ToggleDeckView);
        }

        if (discardViewButton != null) {
            discardViewButton.onClick.RemoveListener(ToggleDiscardView);
        }

        if (deckViewUI != null && deckViewUI.closeButton != null) {
            deckViewUI.closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        Log("Removed click listeners from buttons", LogTag.UI);
        base.OnDestroy(); // Call base class OnDestroy
    }

    // Add required implementations for abstract methods from UIComponent
    protected override void RegisterEvents() { /* No specific events needed */ }
    protected override void UnregisterEvents() { /* No specific events needed */ }
    public override void UpdateUI(IPlayer player = null) { /* Update logic handled by Show/Hide */ }
}