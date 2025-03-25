using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static DebugLogger;

public class DeckViewController : MonoBehaviour {
    private Button deckViewButton;
    private TextMeshProUGUI buttonText;
    private DeckViewUI deckViewUI;
    private GameManager gameManager;
    private GameReferences gameReferences;

    private bool isViewingDeck = false;
    private bool isInitialized = false;

    private void Awake() {
        Log("DeckViewController Awake called", LogTag.UI | LogTag.Initialization);
        InitializeReferences();
    }

    private void InitializeReferences() {
        gameManager = GameManager.Instance;
        gameReferences = GameReferences.Instance;

        if (gameManager != null && gameReferences != null && gameReferences.AreReferencesValid()) {
            GetUIReferences();
            SetupButton();
            isInitialized = true;
            Log("DeckViewController initialized successfully", LogTag.Initialization);
        } else {
            Log("Starting delayed initialization for DeckViewController", LogTag.Initialization);
            StartCoroutine(WaitForInitialization());
        }
    }

    private System.Collections.IEnumerator WaitForInitialization() {
        float timeoutDuration = 5f;
        float elapsed = 0f;

        while (elapsed < timeoutDuration) {
            if (gameManager == null) {
                gameManager = GameManager.Instance;
            }
            if (gameReferences == null) {
                gameReferences = GameReferences.Instance;
            }

            if (gameManager != null && gameReferences != null && gameReferences.AreReferencesValid()) {
                GetUIReferences();
                SetupButton();
                isInitialized = true;
                Log("DeckViewController initialized after delay", LogTag.Initialization);
                yield break;
            }

            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        LogError("DeckViewController initialization timed out", LogTag.Initialization);
    }

    private void GetUIReferences() {
        deckViewButton = gameReferences.GetDeckViewButton();
        deckViewUI = gameReferences.GetDeckViewUI();

        if (deckViewButton == null) {
            LogError("DeckViewButton reference is null", LogTag.UI | LogTag.Initialization);
            return;
        }

        if (deckViewUI == null) {
            LogError("DeckViewUI reference is null", LogTag.UI | LogTag.Initialization);
            return;
        }

        // Get the button text component
        buttonText = deckViewButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText == null) {
            LogWarning("Button text component not found, toggling text will not work", LogTag.UI);
        } else {
            // Initialize button text
            buttonText.text = "View Deck";
            Log("Set initial button text to 'View Deck'", LogTag.UI | LogTag.Initialization);
        }

        // Ensure UI elements are active
        deckViewButton.gameObject.SetActive(true);

        if (deckViewUI.deckViewPanel != null) {
            deckViewUI.deckViewPanel.SetActive(false);
            Log("Ensured deck view panel is hidden on initialization", LogTag.UI | LogTag.Initialization);
        } else {
            LogError("DeckViewPanel is null", LogTag.UI | LogTag.Initialization);
        }
    }

    private void SetupButton() {
        Log("Setting up deck view button", LogTag.UI | LogTag.Initialization);

        if (deckViewButton == null) {
            LogError("Cannot setup button - deckViewButton is null", LogTag.UI | LogTag.Initialization);
            return;
        }

        // First verify if the button has the Image component it needs
        if (deckViewButton.GetComponent<Image>() == null) {
            LogError("DeckViewButton is missing Image component - button may not work correctly", LogTag.UI | LogTag.Initialization);
        }

        // Remove any existing listeners first
        deckViewButton.onClick.RemoveAllListeners();

        // Add our toggle listener
        deckViewButton.onClick.AddListener(ToggleDeckViewInternal);

        Log($"Deck view button listener added, current listener count: {deckViewButton.onClick.GetPersistentEventCount()}",
            LogTag.UI | LogTag.Initialization);

        // Configure the close button to update our state
        if (deckViewUI != null && deckViewUI.closeButton != null) {
            // Add our listener after the DeckViewUI adds its own
            deckViewUI.closeButton.onClick.AddListener(OnCloseButtonClicked);
            Log("Added listener to close button", LogTag.UI | LogTag.Initialization);
        } else {
            LogWarning("Cannot setup close button - closeButton is null", LogTag.UI | LogTag.Initialization);
        }
    }

    // Internal method to handle button click
    private void ToggleDeckViewInternal() {
        Log($"Toggle button clicked, current state: {isViewingDeck}", LogTag.UI);

        if (!isInitialized) {
            LogError("Cannot toggle - not fully initialized yet", LogTag.UI);
            return;
        }

        ToggleDeckView();
    }

    // Main toggle method
    public void ToggleDeckView() {
        if (deckViewUI == null) {
            LogError("Cannot toggle deck view - deckViewUI is null", LogTag.UI);
            return;
        }

        if (deckViewUI.deckViewPanel == null) {
            LogError("Cannot toggle deck view - deckViewPanel is null", LogTag.UI);
            return;
        }

        isViewingDeck = !isViewingDeck;

        Log($"Toggled deck view to {(isViewingDeck ? "visible" : "hidden")}", LogTag.UI);

        if (isViewingDeck) {
            ShowDeckView();
        } else {
            HideDeckView();
        }

        UpdateButtonText();
    }

    private void OnCloseButtonClicked() {
        // Update our internal state when the close button is clicked
        if (isViewingDeck) {
            isViewingDeck = false;
            UpdateButtonText();
            Log("Close button clicked, updated deck view button state", LogTag.UI);
        }
    }

    private void ShowDeckView() {
        Log("ShowDeckView called", LogTag.UI);

        if (deckViewUI == null || deckViewUI.deckViewPanel == null) {
            LogError("Cannot show deck view - references missing", LogTag.UI);
            return;
        }

        // Get the active player from game manager
        var activePlayer = GetActivePlayer();
        if (activePlayer != null) {
            // Initialize if not already
            if (!deckViewUI.IsInitialized) {
                deckViewUI.Initialize(activePlayer);
                Log("Initialized DeckViewUI with player", LogTag.UI);
            }

            try {
                // Update the deck display first
                deckViewUI.UpdateDeckDisplay(activePlayer);
                Log("Updated deck display", LogTag.UI);

                // Now make the panel visible
                deckViewUI.deckViewPanel.SetActive(true);
                Log($"Made deck view panel visible for {(activePlayer.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.UI);
            } catch (System.Exception e) {
                LogError($"Error showing deck view: {e.Message}\n{e.StackTrace}", LogTag.UI);
                // Revert our state since we couldn't show the deck view
                isViewingDeck = false;
                UpdateButtonText();
            }
        } else {
            LogError("Cannot show deck view - no active player found", LogTag.UI);
            // Revert our state since we couldn't show the deck view
            isViewingDeck = false;
            UpdateButtonText();
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

    private void UpdateButtonText() {
        if (buttonText != null) {
            buttonText.text = isViewingDeck ? "Hide Deck" : "View Deck";
            Log($"Updated button text to '{buttonText.text}'", LogTag.UI);
        } else {
            LogWarning("Cannot update button text - buttonText is null", LogTag.UI);
        }
    }

    private IPlayer GetActivePlayer() {
        // For now, we'll return Player1, but you could implement turn-based logic later
        if (gameManager != null) {
            var player = gameManager.Player1;
            if (player == null) {
                LogError("Player1 is null in GameManager", LogTag.UI);
            }
            return player;
        }

        LogError("GameManager is null", LogTag.UI);
        return null;
    }

    // Check if the panel state changed externally
    private void Update() {
        if (!isInitialized) return;

        if (deckViewUI != null && deckViewUI.deckViewPanel != null) {
            bool isPanelActive = deckViewUI.deckViewPanel.activeSelf;
            if (isViewingDeck != isPanelActive) {
                Log($"Panel state changed externally to {isPanelActive}, updating internal state", LogTag.UI);
                isViewingDeck = isPanelActive;
                UpdateButtonText();
            }
        }
    }

    private void OnDestroy() {
        if (deckViewButton != null) {
            deckViewButton.onClick.RemoveListener(ToggleDeckViewInternal);
            Log("Removed click listener from deck view button", LogTag.UI);
        }

        if (deckViewUI != null && deckViewUI.closeButton != null) {
            deckViewUI.closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            Log("Removed click listener from close button", LogTag.UI);
        }
    }
}