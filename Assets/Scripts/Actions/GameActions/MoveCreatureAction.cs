using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class MoveCreatureAction : IGameAction {
        private readonly ICreature creature;
        private readonly BoardSlot fromSlot;
        private readonly BoardSlot toSlot;
        private readonly IPlayer player;

        public ICreature GetCreature() => creature;
        public BoardSlot GetFromSlot() => fromSlot;
        public BoardSlot GetToSlot() => toSlot;
        public IPlayer GetPlayer() => player;

        public MoveCreatureAction(ICreature creature, BoardSlot fromSlot, BoardSlot toSlot, IPlayer player) {
            this.creature = creature;
            this.fromSlot = fromSlot;
            this.toSlot = toSlot;
            this.player = player;
            Log($"Created {this}", LogTag.Actions | LogTag.Creatures);
        }

        public override string ToString() {
            return $"MoveCreatureAction: Creature={creature?.Name} (TargetID: {creature?.TargetId.ToUpper()}), FromSlot={fromSlot?.TargetId.ToUpper()}, ToSlot={toSlot?.TargetId.ToUpper()}";
        }
    }
}
