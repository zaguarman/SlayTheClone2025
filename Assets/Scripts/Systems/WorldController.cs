using TMPro;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The World banner in the middle of the table: which World is in play, until when, and what it does. It's hidden while
    /// there is none. Worlds come from cards, so the old button that changed the weather by hand is hidden.
    /// </summary>
    public class WorldController : UIComponent {
        public const string BannerName = "WeatherPill"; // Its name in the scenes (the World used to be the weather)

        private TextMeshProUGUI worldText;

        private IWorldSystem World => GameManager.WorldSystem;

        public void Initialize(IGameMediator mediator, IGameManager manager, Button oldCycleButton, TextMeshProUGUI label) {
            if (IsInitialized) return;

            worldText = label;
            if (worldText == null) LogError("World text is not assigned in GameReferences", LogTag.UI | LogTag.Initialization);
            if (oldCycleButton != null) oldCycleButton.gameObject.SetActive(false);

            base.Initialize(mediator, manager); // Registers the listeners below
            UpdateUI();
            Log("WorldController initialized", LogTag.UI | LogTag.Initialization);
        }

        protected override void RegisterEvents() {
            World.OnWorldChanged.AddListener(ShowWorld);
            GameMediator.TurnEnded += OnTurnEnded;
        }

        protected override void UnregisterEvents() {
            World?.OnWorldChanged.RemoveListener(ShowWorld);
            if (GameMediator != null) GameMediator.TurnEnded -= OnTurnEnded;
        }

        public override void UpdateUI(IPlayer player = null) => ShowWorld(World.Current);

        private void OnTurnEnded(int endedTurn) => UpdateUI(); // Its Days left go down

        private void ShowWorld(World world) {
            if (worldText == null) return;
            int daysLeft = World.LastDay != null ? World.LastDay.Value - GameManager.TurnManager.TurnNumber + 1 : 0;
            string days = daysLeft > 0 ? $" ({daysLeft} {(daysLeft == 1 ? "Day" : "Days")})" : "";
            worldText.text = world == Enums.World.None ? "" : $"<b>{WorldSystem.NameOf(world)}</b>{days}: {WorldSystem.RuleOf(world)}";
            var banner = worldText.transform.parent;
            if (banner != null && banner.name == BannerName) banner.gameObject.SetActive(world != Enums.World.None);
        }
    }
}
