using TMPro;
using UnityEngine;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
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

        public override void Initialize(IGameMediator mediator, IGameManager manager) {
            base.Initialize(mediator, manager);

            // Get TurnManager from GameManager
            turnManager = manager?.TurnManager;
            if (turnManager == null) {
                LogError("TurnManager not available when initializing TurnUI", LogTag.UI | LogTag.Initialization);
            }

            UpdateUI(); // Initial UI update
            // IsInitialized set by base class
        }

        protected override void RegisterEvents() {
            if (GameMediator != null) {
                // Listen to TurnEnded event instead of GameStateChanged for turn updates
                GameMediator.TurnEnded += OnTurnEnded;
            }
        }

        protected override void UnregisterEvents() {
            if (GameMediator != null) {
                GameMediator.TurnEnded -= OnTurnEnded;
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
            turnText.text = $"Turn {turnNumber}";
        }
    }
}
