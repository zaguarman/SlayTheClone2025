using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class DrawCardsAction : IGameAction, ISourcedAction {
        public ICard Source { get; set; }
        private readonly IPlayer player;
        private readonly int amount;
        /// <summary>A card's effect, not the Day's hand: during resolution and Dusk it adds to the next Day's draw instead.</summary>
        public bool FromEffect { get; }

        public IPlayer GetPlayer() => player;
        public int GetAmount() => amount;

        /// <summary>The Day's hand (creatures up to 3, 2 spells), its amount the extra cards (fewer, negative).</summary>
        public bool DealsDay { get; }

        public DrawCardsAction(IPlayer player, int amount = 1, bool fromEffect = false, bool dealsDay = false) {
            this.player = player;
            this.amount = amount;
            FromEffect = fromEffect;
            DealsDay = dealsDay;
            Log($"Created {this}", LogTag.Actions | LogTag.Cards);
        }

        public override string ToString() {
            return $"DrawCardsAction: Player={player?.Name} (TargetID: {player?.TargetId.ToUpper()}), Amount={amount}";
        }
    }
}
