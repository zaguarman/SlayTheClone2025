using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DrawCardsAction : IGameAction {
        private readonly IPlayer player;
        private readonly int amount;

        public IPlayer GetPlayer() => player;
        public int GetAmount() => amount;

        public DrawCardsAction(IPlayer player, int amount = 1) {
            this.player = player;
            this.amount = amount;
            Log($"Created {this}", LogTag.Actions | LogTag.Cards);
        }

        public override string ToString() {
            return $"DrawCardsAction: Player={player?.Name} (TargetID: {player?.TargetId.ToUpper()}), Amount={amount}";
        }
    }
}
