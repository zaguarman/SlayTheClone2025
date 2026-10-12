using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>Where a move ends up on the board as it is now: the mover's slot, and the creature it pushes or swaps with, if any.</summary>
    public readonly struct MoveOutcome {
        public BoardSlot End { get; }        // Where the mover ends up; its own slot if it can't move at all
        public ICreature Partner { get; }    // The creature it pushes or swaps with
        public BoardSlot PartnerEnd { get; }
        public bool ReachesDestination { get; }

        public MoveOutcome(BoardSlot end, bool reachesDestination, ICreature partner = null, BoardSlot partnerEnd = null) {
            End = end;
            ReachesDestination = reachesDestination;
            Partner = partner;
            PartnerEnd = partnerEnd;
        }
    }

    /// <summary>
    /// How creatures move along their row (GDD 2.4 Slot Occupation &amp; Movement). By default a creature walks to an empty
    /// slot, without passing through others, or walks into an occupied one by pushing its creature one slot further.
    /// Leapfrog jumps over creatures; Swap trades places with a friendly creature at any distance.
    /// </summary>
    public static class MovementRules {
        /// <summary>
        /// What moving to this slot means, decided when the move is planned: each destination has one meaning, and the
        /// creature uses the best option it has (Leapfrog over walking for an empty slot, Swap over Push for an occupied one).
        /// </summary>
        public static MoveKind KindOf(ICreature creature, BoardSlot to) {
            var movement = creature?.Movement ?? MovementKeyword.None;
            bool occupied = to != null && to.IsOccupied() && to.OccupyingCreature != creature;
            if (occupied) return movement.HasFlag(MovementKeyword.Swap) ? MoveKind.Swap : MoveKind.Push;
            return movement.HasFlag(MovementKeyword.Leapfrog) ? MoveKind.Leapfrog : MoveKind.Walk;
        }

        /// <summary>
        /// Where the move would end up if it resolved now. Blocked on the way, the creature stops at the furthest empty slot
        /// it can still reach toward its destination (GDD 2.4 Blocked Before It Resolves); if there's none, it stays put.
        /// </summary>
        public static MoveOutcome Resolve(ICreature creature, BoardSlot to, MoveKind kind) {
            var from = creature?.Slot;
            if (from == null || to == null || to == from || to.Owner != from.Owner) return new MoveOutcome(from, to == from);

            var row = from.Owner.Battlefield;
            int dir = to.Index > from.Index ? 1 : -1;
            var occupant = to.OccupyingCreature;

            switch (kind) {
                case MoveKind.Swap:
                    // Ignores creatures in between; with nobody there any more, it takes the empty slot
                    return new MoveOutcome(to, true, occupant, occupant != null ? from : null);

                case MoveKind.Leapfrog:
                    if (occupant == null) return new MoveOutcome(to, true);
                    // Taken: the empty slot closest to the destination on its way there
                    for (int i = to.Index - dir; i != from.Index; i -= dir) {
                        if (!row[i].IsOccupied()) return new MoveOutcome(row[i], false);
                    }
                    return new MoveOutcome(from, false);

                default: // Walk, Push
                    var reached = from;
                    for (int i = from.Index + dir; i != to.Index; i += dir) {
                        if (row[i].IsOccupied()) return new MoveOutcome(reached, false);
                        reached = row[i];
                    }
                    if (occupant == null) return new MoveOutcome(to, true);

                    // A push moves the creature one slot further, only into an empty slot: never two creatures in a chain
                    int beyond = to.Index + dir;
                    if (kind == MoveKind.Push && beyond >= 0 && beyond < row.Count && !row[beyond].IsOccupied()) {
                        return new MoveOutcome(to, true, occupant, row[beyond]);
                    }
                    return new MoveOutcome(reached, false);
            }
        }

        /// <summary>Carries out a move's outcome on the board.</summary>
        public static void Apply(ICreature creature, MoveKind kind, MoveOutcome outcome) {
            var from = creature.Slot;
            if (from == null || outcome.End == null || outcome.End == from) return;

            if (kind == MoveKind.Swap) {
                BoardSlot.Swap(from, outcome.End);
                return;
            }
            if (outcome.Partner != null) {
                var pushed = outcome.End.Clear();
                outcome.PartnerEnd.Place(pushed);
            }
            from.Clear();
            outcome.End.Place(creature);
        }
    }
}
