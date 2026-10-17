using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class HealPlayerAction : IGameAction, ISourcedAction {
        public ICard Source { get; set; }
        private readonly IPlayer target;
        private readonly int amount;

        public IPlayer GetTargetPlayer() => target;
        public int GetAmount() => amount;

        public HealPlayerAction(IPlayer target, int amount) {
            this.target = target;
            this.amount = amount;
            Log($"Created {this}", LogTag.Actions | LogTag.Players);
        }

        public override string ToString() {
            return $"HealPlayerAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Amount={amount}";
        }
    }
}
