namespace SlayTheClone {
    public class SummonCreatureAction : IGameAction {
        private readonly ICreature creature;
        private readonly IPlayer owner;
        private readonly ITarget targetSlot; // Expected to be a BoardSlot
        private readonly bool fromDeck;

        public ICreature GetCreature() => creature;
        public IPlayer GetOwner() => owner;
        public ITarget GetTargetSlot() => targetSlot;
        public bool IsFromDeck() => fromDeck;
        /// <summary>Picked out of its owner's deck when queued (Summon from deck): it must still be there when it resolves.</summary>
        public bool PickedFromDeck { get; }

        public SummonCreatureAction(ICreature creature, IPlayer owner, ITarget target, bool fromDeck = false, bool pickedFromDeck = false) {
            PickedFromDeck = pickedFromDeck;
            this.creature = creature;
            this.owner = owner;
            targetSlot = target;
            this.fromDeck = fromDeck;
        }

        public override string ToString() {
            return $"SummonCreatureAction: Cr={creature?.Name}({creature?.TargetId.ShortId()}), Own={owner?.Name}, Slot={targetSlot?.TargetId.ShortId()}, Deck={fromDeck}";
        }
    }
}
