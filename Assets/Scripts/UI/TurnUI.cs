using UnityEngine;
using TMPro;
using static DebugLogger;

public class TurnUI : UIComponent {
    [SerializeField] private TextMeshProUGUI turnText;
    private ITurnManager turnManager;

    protected override void Awake() {
        base.Awake();

        // Try to get reference if not assigned in inspector
        if (turnText == null) {
            turnText = GetComponentInChildren<TextMeshProUGUI>();
            if (turnText == null) {
                LogWarning("TurnText component not found on TurnUI", LogTag.UI | LogTag.Initialization);
            }
        }
    }

    public override void Initialize(IGameMediator mediator, IGameReferences references, IGameManager manager) {
        // Call base UIComponent Initialize FIRST
        base.Initialize(mediator, references, manager); // Pass dependencies

        // Get TurnManager from GameManager
        turnManager = manager?.TurnManager;
        if (turnManager == null) {
            LogError("TurnManager not available when initializing TurnUI", LogTag.UI | LogTag.Initialization);
        }

        UpdateUI(); // Initial UI update
        // IsInitialized set by base class
    }

    protected override void RegisterEvents() {
        if (gameMediator != null) {
            // Listen to TurnEnded event instead of GameStateChanged for turn updates
            gameMediator.AddTurnEndedListener(OnTurnEnded);
            // gameMediator.AddGameStateChangedListener(OnGameStateChanged); // Remove this if TurnEnded is sufficient
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            gameMediator.RemoveTurnEndedListener(OnTurnEnded);
            // gameMediator.RemoveGameStateChangedListener(OnGameStateChanged); // Remove this
        }
    }

    // Rename event handler
    private void OnTurnEnded(int newTurnNumber) {
        UpdateUI();
    }

    public override void UpdateUI(IPlayer player = null) {
        // Don't use player parameter
        if (turnText == null) return;

        // Get turn number from GameManager (temporary) or store from event
        int turnNumber = turnManager?.TurnNumber ?? 0;
        turnText.text = $"Turn: {turnNumber}";
    }
}
