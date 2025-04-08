using TMPro;

public class HealthHandler {
    private readonly TextMeshProUGUI healthText;
    private readonly IPlayer player;
    private readonly GameMediator gameMediator;

    public HealthHandler(TextMeshProUGUI healthText, IPlayer player, GameMediator gameMediator) {
        this.healthText = healthText;
        this.player = player;
        this.gameMediator = gameMediator;

        // Register for events
        if (gameMediator != null) {
            gameMediator.AddGameStateChangedListener(UpdateUI);
            gameMediator.AddPlayerDamagedListener(HandlePlayerDamaged);
        }
    }

    public void UpdateUI() {
        if (healthText != null && player != null) {
            healthText.text = $"Health: {player.Health}";
        }
    }

    private void HandlePlayerDamaged(IPlayer damagedPlayer, int damage) {
        if (damagedPlayer == player) {
            UpdateUI();
        }
    }

    public void Cleanup() {
        if (gameMediator != null) {
            gameMediator.RemoveGameStateChangedListener(UpdateUI);
            gameMediator.RemovePlayerDamagedListener(HandlePlayerDamaged);
        }
    }
}
