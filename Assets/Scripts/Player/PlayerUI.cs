using TMPro;
using static DebugLogger;

public class PlayerUI : UIComponent {
    private HandUI handUI;
    private TextMeshProUGUI healthText;

    public override void Initialize(IPlayer player, IGameMediator mediator, IGameReferences references) {
        // Call base UIComponent Initialize FIRST
        base.Initialize(player, mediator, references);

        // Get the player's health text based on whether it's player 1 or 2
        healthText = player.IsPlayer1 ?
            references.player1References.HealthText :
            references.player2References.HealthText;

        if (healthText == null) {
            LogWarning($"Health text reference missing for {(player.IsPlayer1 ? "Player 1" : "Player 2")} (TargetID: {player.TargetId.ToUpper()})", LogTag.UI | LogTag.Initialization);
        } else {
            // Give the player direct access to its health text
            player.SetHealthText(healthText);
            Log($"Health text set for {(player.IsPlayer1 ? "Player 1" : "Player 2")} (TargetID: {player.TargetId.ToUpper()})", LogTag.UI | LogTag.Initialization);
        }

        // Don't initialize HandUI here, let GameUI handle its children's initialization
        // InitializeHandUI(player); // REMOVE THIS CALL

        // IsInitialized is set by base class
        Log($"PlayerUI initialized for {(player.IsPlayer1 ? "Player 1" : "Player 2")} (TargetID: {player.TargetId.ToUpper()})", LogTag.UI | LogTag.Initialization);
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
        UpdateUI(Player);
    }

    public override void UpdateUI(IPlayer player) {
        if (!IsInitialized || player != Player) return;

        // Update the hand UI
        handUI?.UpdateUI(player);

        // Force an update of the player's health UI
        player.UpdateHealthUI();
    }

    // Remove InitializeHandUI - this should be managed by GameUI
    // private void InitializeHandUI(IPlayer player) { ... } // REMOVED

    protected override void CleanupComponent() {
        // No need to clean up the health handler as it's now managed by the Player
    }

    protected override void OnDestroy() {
        CleanupComponent();
        base.OnDestroy();
    }
}