using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class HealCreatureAction : IGameAction, ISourcedAction {
        public ICard Source { get; set; }
        private readonly ICreature target;
        private readonly int amount;

        public ICreature GetTarget() => target;
        public int GetAmount() => amount;

        public HealCreatureAction(ICreature target, int amount) {
            this.target = target;
            this.amount = amount;
            Log($"Created {this}", LogTag.Actions | LogTag.Creatures);
        }

        public override string ToString() {
            return $"HealCreatureAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Amount={amount}";
        }
    }
}
