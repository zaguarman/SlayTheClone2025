namespace SlayTheClone {
    /// <summary>
    /// One position on a player's battlefield, as the game rules see it. Plain C#: the on-screen
    /// <see cref="BattlefieldSlot"/> is a view bound to one of these.
    /// </summary>
    public class BoardSlot : ITarget {
        public string TargetId { get; } = System.Guid.NewGuid().ToString();
        public IPlayer Owner { get; }
        public int Index { get; } // Position on the owner's battlefield, left to right
        public ICreature OccupyingCreature { get; private set; }
        /// <summary>The Slot Effect on this slot, if any (see <see cref="SlotEffectRules"/>). One per slot.</summary>
        public SlotEffect Effect { get; internal set; }

        public BoardSlot(IPlayer owner, int index) {
            Owner = owner;
            Index = index;
        }

        public bool IsOccupied() => OccupyingCreature != null;

        public bool IsValidTarget() => true;

        /// <summary>Puts the creature here, replacing any occupant, and points the creature at this slot.</summary>
        public void Place(ICreature creature) {
            Clear();
            OccupyingCreature = creature;
            if (creature != null) creature.Slot = this;
        }

        /// <summary>Empties the slot and returns the creature that was in it.</summary>
        public ICreature Clear() {
            var creature = OccupyingCreature;
            OccupyingCreature = null;
            if (creature != null && creature.Slot == this) creature.Slot = null;
            return creature;
        }

        /// <summary>Exchanges the creatures of two slots; with one slot empty this moves the other's creature.</summary>
        public static void Swap(BoardSlot first, BoardSlot second) {
            var firstCreature = first.Clear();
            var secondCreature = second.Clear();
            if (firstCreature != null) second.Place(firstCreature);
            if (secondCreature != null) first.Place(secondCreature);
        }

        public override string ToString() => $"Slot {Index} of {Owner?.Name ?? "nobody"}";
    }
}
