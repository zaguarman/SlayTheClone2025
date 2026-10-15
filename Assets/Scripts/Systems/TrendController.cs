using TMPro;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The Trend banner in the middle of the table: what's trending, until when, and what it does. It's hidden while
    /// nothing is. Trends come from cards, so the old button that changed the weather by hand is hidden.
    /// </summary>
    public class TrendController : UIComponent {
        public const string BannerName = "WeatherPill"; // Its name in the scenes (the Trend used to be the weather)

        private TextMeshProUGUI trendText;

        private ITrendSystem Trends => GameManager.TrendSystem;

        public void Initialize(IGameMediator mediator, IGameManager manager, Button oldCycleButton, TextMeshProUGUI label) {
            if (IsInitialized) return;

            trendText = label;
            if (trendText == null) LogError("Trend text is not assigned in GameReferences", LogTag.UI | LogTag.Initialization);
            if (oldCycleButton != null) oldCycleButton.gameObject.SetActive(false);

            base.Initialize(mediator, manager); // Registers the listeners below
            UpdateUI();
            Log("TrendController initialized", LogTag.UI | LogTag.Initialization);
        }

        protected override void RegisterEvents() {
            Trends.OnTrendChanged.AddListener(ShowTrend);
            GameMediator.TurnEnded += OnTurnEnded;
        }

        protected override void UnregisterEvents() {
            Trends?.OnTrendChanged.RemoveListener(ShowTrend);
            if (GameMediator != null) GameMediator.TurnEnded -= OnTurnEnded;
        }

        public override void UpdateUI(IPlayer player = null) => ShowTrend(Trends.Current);

        private void OnTurnEnded(int endedTurn) => UpdateUI(); // Its Days left go down

        private void ShowTrend(Trend trend) {
            if (trendText == null) return;
            int daysLeft = Trends.LastDay != null ? Trends.LastDay.Value - GameManager.TurnManager.TurnNumber + 1 : 0;
            string days = daysLeft > 0 ? $" ({daysLeft} {(daysLeft == 1 ? "Day" : "Days")})" : "";
            string rule = trend == Trend.Casino && Trends.Roll != CasinoRoll.None // The Day's weather, not the whole wheel
                ? $"today <b>{TrendSystem.NameOf(Trends.Roll)}</b>, {TrendSystem.RuleOf(Trends.Roll)}"
                : TrendSystem.RuleOf(trend);
            trendText.text = trend == Trend.None ? "" : $"Trend: <b>{TrendSystem.NameOf(trend)}</b>{days}: {rule}";
            var banner = trendText.transform.parent;
            if (banner != null && banner.name == BannerName) banner.gameObject.SetActive(trend != Trend.None);
        }
    }
}
