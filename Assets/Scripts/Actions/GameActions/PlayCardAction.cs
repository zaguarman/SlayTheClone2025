using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class PlayCardAction : IGameAction {
        private readonly ICard card;
        private readonly IPlayer owner;
        private readonly ITarget target;

        public ICard GetCard() => card;
        public IPlayer GetOwner() => owner;
        public ITarget GetTarget() => target;

        public PlayCardAction(ICard card, IPlayer owner, ITarget target) {
            this.card = card;
            this.owner = owner;
            this.target = target;
            Log($"Created {this}", LogTag.Actions | LogTag.Cards);
        }

        public override string ToString() {
            return $"PlayCardAction: Card={card?.Name} (TargetID: {card?.TargetId.ToUpper()}), Owner={owner?.Name} (TargetID: {owner?.TargetId.ToUpper()}), Target={target?.TargetId.ToUpper()}";
        }
    }
}
