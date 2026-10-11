using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class MoveCreatureAction : IGameAction {
        private readonly ICreature creature;
        private readonly ITarget fromSlot;
        private readonly ITarget toSlot;
        private readonly IPlayer player;

        public ICreature GetCreature() => creature;
        public ITarget GetFromSlot() => fromSlot;
        public ITarget GetToSlot() => toSlot;
        public IPlayer GetPlayer() => player;

        public MoveCreatureAction(ICreature creature, ITarget fromSlot, ITarget toSlot, IPlayer player) {
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
