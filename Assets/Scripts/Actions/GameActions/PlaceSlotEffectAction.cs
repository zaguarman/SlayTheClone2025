using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>Puts a Slot Effect on a slot, replacing the one there. Static: it stays with the slot, not the creature in it.</summary>
    public class PlaceSlotEffectAction : IGameAction, ISourcedAction {
        /// <summary>The card that placed it (shown when a trap it laid goes off).</summary>
        public ICard Source { get; set; }
        private readonly BoardSlot slot;
        private readonly SlotEffectType type;
        private readonly int value;
        private readonly int days;
        private readonly IPlayer controller;

        public BoardSlot GetSlot() => slot;
        public SlotEffectType GetEffectType() => type;
        public int GetValue() => value;
        public int GetDays() => days;
        public IPlayer GetController() => controller;

        public PlaceSlotEffectAction(BoardSlot slot, SlotEffectType type, int value, int days, IPlayer controller) {
            this.slot = slot;
            this.type = type;
            this.value = value;
            this.days = days;
            this.controller = controller;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"PlaceSlotEffectAction: {type} on {slot}, Value={value}, Days={days}, Controller={controller?.Name}";
    }

    /// <summary>A Slot Effect going off (a Landmine, a Barbed Snare): its card shows on the stage, then what it does lands.</summary>
    public class TrapSprungAction : IGameAction {
        public BoardSlot Slot { get; }
        public SlotEffect Effect { get; }

        public TrapSprungAction(BoardSlot slot, SlotEffect effect) {
            Slot = slot;
            Effect = effect;
        }

        public override string ToString() => $"TrapSprungAction: {Effect?.Name} on {Slot}";
    }

    /// <summary>Cleanse (SlotEffect): removes the slot's Slot Effect, hidden or not, without triggering it.</summary>
    public class CleanseSlotAction : IGameAction {
        private readonly BoardSlot slot;

        public BoardSlot GetSlot() => slot;

        public CleanseSlotAction(BoardSlot slot) {
            this.slot = slot;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"CleanseSlotAction: {slot}";
    }
}
