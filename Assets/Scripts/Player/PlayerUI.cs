using TMPro;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class PlayerUI : UIComponent {
        private TextMeshProUGUI healthText;

        public void Initialize(IPlayer player, IGameMediator mediator, IGameManager manager, TextMeshProUGUI health) {
            healthText = health;
            if (healthText == null) {
                LogWarning($"Health text reference missing for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.UI | LogTag.Initialization);
            }

            base.Initialize(player, mediator, manager);
            ShowHealth(player.Health);

            Log($"PlayerUI initialized for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.UI | LogTag.Initialization);
        }

        protected override void RegisterEvents() {
            if (Player == null) return;
            Player.HealthChanged -= ShowHealth; // Never subscribe twice
            Player.HealthChanged += ShowHealth;
        }

        protected override void UnregisterEvents() {
            if (Player != null) Player.HealthChanged -= ShowHealth;
        }

        private void ShowHealth(int health) {
            if (healthText != null) healthText.text = $"{health}"; // No maximum to show (the user)
        }

        public override void UpdateUI(IPlayer player) {
            if (!IsInitialized || player != Player) return;
            ShowHealth(player.Health);
        }
    }
}
