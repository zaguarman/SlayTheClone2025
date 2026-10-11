using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DiscardHandAction : IGameAction {
        private readonly IPlayer player;

        public IPlayer GetPlayer() => player;

        public DiscardHandAction(IPlayer player) {
            this.player = player;
            Log($"Created {this}", LogTag.Actions | LogTag.Cards);
        }

        public override string ToString() {
            return $"DiscardHandAction: Player={player?.Name} (PlayerID: {player?.TargetId.ToUpper() ?? "UNKNOWN"}) with {player?.Hand.Count ?? 0} cards";
        }
    }
}
