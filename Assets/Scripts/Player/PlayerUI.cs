using TMPro;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class PlayerUI : UIComponent {
        private HandUI handUI;
        private TextMeshProUGUI healthText;

        public override void Initialize(IPlayer player, IGameMediator mediator, IGameReferences references, IGameManager manager) {
            base.Initialize(player, mediator, references, manager);

            var playerReferences = references.GetPlayerReferences(player);
            healthText = playerReferences.PlayerUI.GetComponentInChildren<TextMeshProUGUI>();
            handUI = playerReferences.HandUI;

            if (healthText == null) {
                LogWarning($"Health text reference missing for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.UI | LogTag.Initialization);
            }

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
            if (healthText != null) healthText.text = $"Health: {health}";
        }

        public override void UpdateUI(IPlayer player) {
            if (!IsInitialized || player != Player) return;

            // The HandUI reference may have been missing when this was initialized
            if (handUI == null) {
                handUI = GameReferences?.GetPlayerReferences(player).HandUI;
            }
            handUI?.UpdateUI(player);

            ShowHealth(player.Health);
        }
    }
}
