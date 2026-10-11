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
            else {
                // The player updates its own health text
                player.SetHealthText(healthText);
                Log($"Health text set for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.UI | LogTag.Initialization);
            }

            Log($"PlayerUI initialized for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.UI | LogTag.Initialization);
        }

        protected override void RegisterEvents() {
            GameMediator?.AddGameStateChangedListener(OnGameStateChanged);
        }

        protected override void UnregisterEvents() {
            GameMediator?.RemoveGameStateChangedListener(OnGameStateChanged);
        }

        // HandUI refreshes itself from hand/queue events; rebuilding it here on every state change destroyed and
        // recreated all hand cards many times per action
        private void OnGameStateChanged() {
            if (!IsInitialized || Player == null) return;
            Player.UpdateHealthUI();
        }

        public override void UpdateUI(IPlayer player) {
            if (!IsInitialized || player != Player) return;

            // The HandUI reference may have been missing when this was initialized
            if (handUI == null) {
                handUI = GameReferences?.GetPlayerReferences(player).HandUI;
            }
            handUI?.UpdateUI(player);

            player.UpdateHealthUI();
        }
    }
}
