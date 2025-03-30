using UnityEngine;
using TMPro;
using static DebugLogger;

public class TurnUI : UIComponent {
    [SerializeField] private TextMeshProUGUI turnText;
    private TurnManager turnManager;

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

    public override void Initialize(IPlayer player = null) {
        base.Initialize(player);
        
        turnManager = TurnManager.Instance;
        if (turnManager == null) {
            LogError("TurnManager not found when initializing TurnUI", LogTag.UI | LogTag.Initialization);
        }
        
        UpdateUI();
    }

    protected override void RegisterEvents() {
        if (gameMediator != null) {
            gameMediator.AddGameStateChangedListener(OnGameStateChanged);
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            gameMediator.RemoveGameStateChangedListener(OnGameStateChanged);
        }
    }

    private void OnGameStateChanged() {
        UpdateUI();
    }

    public override void UpdateUI(IPlayer player = null) {
        if (turnText == null || turnManager == null) return;
        
        turnText.text = $"Turn: {turnManager.TurnNumber}";
    }
}
