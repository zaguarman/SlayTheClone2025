using System.Collections.Generic;
using TMPro;

namespace SlayTheClone {
    /// <summary>
    /// Both sides of the table as the UI shows them, for views that work across sides:
    /// the resolution playback and arrows that point at enemy slots.
    /// </summary>
    public class TableViews {
        private readonly GameReferences.PlayerUIReferences player1Views;
        private readonly GameReferences.PlayerUIReferences player2Views;

        public TableViews(GameReferences.PlayerUIReferences player1Views, GameReferences.PlayerUIReferences player2Views) {
            this.player1Views = player1Views ?? throw new System.ArgumentNullException(nameof(player1Views));
            this.player2Views = player2Views ?? throw new System.ArgumentNullException(nameof(player2Views));
        }

        public IEnumerable<BattlefieldUI> Battlefields => new[] { player1Views.BattlefieldUI, player2Views.BattlefieldUI };
        public BattlefieldUI GetBattlefield(IPlayer player) => Get(player).BattlefieldUI;
        public TextMeshProUGUI GetHealthText(IPlayer player) => Get(player).HealthText;

        private GameReferences.PlayerUIReferences Get(IPlayer player) => player.IsPlayer1 ? player1Views : player2Views;
    }
}
