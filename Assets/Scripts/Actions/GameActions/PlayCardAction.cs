using System.Collections.Generic;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class PlayCardAction : IGameAction {
        private readonly ICard card;
        private readonly IPlayer owner;
        private readonly ITarget target;

        public ICard GetCard() => card;
        /// <summary>The cards set aside to pay its discard cost: discarded when it resolves, back to the hand if it's taken back.</summary>
        public List<ICard> Paid { get; } = new List<ICard>();
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
