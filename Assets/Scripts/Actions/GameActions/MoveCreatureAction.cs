using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

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
        /// <summary>What the move does, fixed when it's planned: each destination has one meaning (GDD 2.4).</summary>
        public MoveKind Kind { get; }

        /// <param name="kind">What the move is, when the planner worked it out with the other plans (a slot being left is walked into).</param>
        public MoveCreatureAction(ICreature creature, BoardSlot fromSlot, BoardSlot toSlot, IPlayer player, MoveKind? kind = null) {
            this.creature = creature;
            this.fromSlot = fromSlot;
            this.toSlot = toSlot;
            this.player = player;
            Kind = kind ?? MovementRules.KindOf(creature, toSlot);
            Log($"Created {this}", LogTag.Actions | LogTag.Creatures);
        }

        public override string ToString() {
            return $"MoveCreatureAction ({Kind}): Creature={creature?.Name} (TargetID: {creature?.TargetId.ToUpper()}), FromSlot={fromSlot?.TargetId.ToUpper()}, ToSlot={toSlot?.TargetId.ToUpper()}";
        }
    }
}
