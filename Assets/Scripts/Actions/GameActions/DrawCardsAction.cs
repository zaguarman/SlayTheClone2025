using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DrawCardsAction : IGameAction, ISourcedAction {
        public ICard Source { get; set; }
        private readonly IPlayer player;
        private readonly int amount;
        /// <summary>A card's effect, not the Day's hand: during resolution and Dusk it adds to the next Day's draw instead.</summary>
        public bool FromEffect { get; }

        public IPlayer GetPlayer() => player;
        public int GetAmount() => amount;

        public DrawCardsAction(IPlayer player, int amount = 1, bool fromEffect = false) {
            this.player = player;
            this.amount = amount;
            FromEffect = fromEffect;
            Log($"Created {this}", LogTag.Actions | LogTag.Cards);
        }

        public override string ToString() {
            return $"DrawCardsAction: Player={player?.Name} (TargetID: {player?.TargetId.ToUpper()}), Amount={amount}";
        }
    }
}
