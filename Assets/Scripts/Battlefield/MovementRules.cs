using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>A move planned on a side: who goes where, and what kind of move it is.</summary>
    public readonly struct PlannedMove {
        public ICreature Creature { get; }
        public BoardSlot To { get; }
        public MoveKind Kind { get; }

        public PlannedMove(ICreature creature, BoardSlot to, MoveKind kind) {
            Creature = creature;
            To = to;
            Kind = kind;
        }
    }

    /// <summary>Where every creature of a row ends up once the planned moves resolve, and which moves can't reach their destination.</summary>
    public class FormationPreview {
        public IReadOnlyDictionary<ICreature, BoardSlot> FinalSlots { get; }
        public IReadOnlyCollection<ICreature> Blocked { get; }

        public FormationPreview(IReadOnlyDictionary<ICreature, BoardSlot> finalSlots, IReadOnlyCollection<ICreature> blocked) {
            FinalSlots = finalSlots;
            Blocked = blocked;
        }
    }

    /// <summary>
    /// How creatures move along their row (GDD 2.4 Slot Occupation &amp; Movement). By default a creature walks to an empty
    /// slot, without passing through others, or walks into an occupied one by pushing its creature one slot further.
    /// Leapfrog jumps over creatures; Swap trades places with a friendly creature at any distance.
    /// </summary>
    public static class MovementRules {
        // A move worked out on a row of occupants (index = slot), so it can run on the board or on a copy of it
        private readonly struct RowOutcome {
            public readonly int End;
            public readonly bool Reaches;
            public readonly int PartnerEnd; // -1 without a partner

            public RowOutcome(int end, bool reaches, int partnerEnd = -1) {
                End = end;
                Reaches = reaches;
                PartnerEnd = partnerEnd;
            }
        }

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
            var outcome = Resolve(Occupants(row), from.Index, to.Index, kind);
            return new MoveOutcome(row[outcome.End], outcome.Reaches,
                outcome.PartnerEnd >= 0 ? to.OccupyingCreature : null,
                outcome.PartnerEnd >= 0 ? row[outcome.PartnerEnd] : null);
        }

        private static RowOutcome Resolve(ICreature[] row, int from, int to, MoveKind kind) {
            int dir = to > from ? 1 : -1;
            bool occupied = row[to] != null;

            switch (kind) {
                case MoveKind.Swap:
                    // Ignores creatures in between; with nobody there any more, it takes the empty slot
                    return new RowOutcome(to, true, occupied ? from : -1);

                case MoveKind.Leapfrog:
                    if (!occupied) return new RowOutcome(to, true);
                    // Taken: the empty slot closest to the destination on its way there
                    for (int i = to - dir; i != from; i -= dir) {
                        if (row[i] == null) return new RowOutcome(i, false);
                    }
                    return new RowOutcome(from, false);

                default: // Walk, Push
                    int reached = from;
                    for (int i = from + dir; i != to; i += dir) {
                        if (row[i] != null) return new RowOutcome(reached, false);
                        reached = i;
                    }
                    if (!occupied) return new RowOutcome(to, true);

                    // A push moves the creature one slot further, only into an empty slot: never two creatures in a chain
                    int beyond = to + dir;
                    if (kind == MoveKind.Push && beyond >= 0 && beyond < row.Length && row[beyond] == null) {
                        return new RowOutcome(to, true, beyond);
                    }
                    return new RowOutcome(reached, false);
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

        private static void Apply(ICreature[] row, int from, MoveKind kind, RowOutcome outcome) {
            if (outcome.End == from) return;
            var mover = row[from];
            var partner = row[outcome.End];
            row[from] = null;
            if (partner != null) {
                row[kind == MoveKind.Swap ? from : outcome.PartnerEnd] = partner;
            }
            row[outcome.End] = mover;
        }

        /// <summary>
        /// Whether a planned move can go now in the Automatic Move Order (GDD 2.4): it reaches its destination, and doesn't
        /// push or swap a creature that is still planned to leave (that one gets to leave first).
        /// </summary>
        public static bool WorksNow(MoveOutcome outcome, ICollection<ICreature> stillLeaving) =>
            outcome.ReachesDestination && (outcome.Partner == null || !stillLeaving.Contains(outcome.Partner));

        /// <summary>
        /// Plays the row's planned moves out on a copy of the board, in the order the game would resolve them, to show
        /// the final formation while planning. A move that doesn't reach its destination is blocked.
        /// </summary>
        public static FormationPreview Preview(IReadOnlyList<BoardSlot> row, IEnumerable<PlannedMove> plannedMoves) {
            var occupants = Occupants(row);
            int IndexOf(ICreature creature) => System.Array.IndexOf(occupants, creature);

            var pending = plannedMoves.Where(m => m.Creature != null && m.To != null && IndexOf(m.Creature) >= 0).ToList();
            var blocked = new HashSet<ICreature>();
            while (pending.Count > 0) {
                var leaving = new HashSet<ICreature>(pending.Select(m => m.Creature));
                RowOutcome OutcomeOf(PlannedMove m) => m.To.Index == IndexOf(m.Creature)
                    ? new RowOutcome(m.To.Index, true)
                    : Resolve(occupants, IndexOf(m.Creature), m.To.Index, m.Kind);
                bool Works(PlannedMove m) {
                    var outcome = OutcomeOf(m);
                    var partner = outcome.PartnerEnd >= 0 ? occupants[m.To.Index] : null;
                    return outcome.Reaches && (partner == null || !leaving.Contains(partner));
                }

                var next = pending.Cast<PlannedMove?>().FirstOrDefault(m => Works(m.Value)) ?? pending[0];
                pending.Remove(next);
                var result = OutcomeOf(next);
                if (!result.Reaches) blocked.Add(next.Creature);
                Apply(occupants, IndexOf(next.Creature), next.Kind, result);
            }

            var finalSlots = new Dictionary<ICreature, BoardSlot>();
            for (int i = 0; i < occupants.Length; i++) {
                if (occupants[i] != null) finalSlots[occupants[i]] = row[i];
            }
            return new FormationPreview(finalSlots, blocked);
        }

        /// <summary>The moves planned on this side, in the order they were planned.</summary>
        /// <param name="include">Which moves to count, e.g. only the ones the screen may show (Fog of War); all if null.</param>
        public static List<PlannedMove> PlannedMovesOf(IPlayer side, IActionsQueue queue, Func<MoveCreatureAction, bool> include = null) =>
            queue?.GetPendingActions().OfType<MoveCreatureAction>()
                .Where(m => m.GetCreature()?.Slot != null && m.GetCreature().Slot.Owner == side && (include == null || include(m)))
                .Select(m => new PlannedMove(m.GetCreature(), m.GetToSlot(), m.Kind))
                .ToList() ?? new List<PlannedMove>();

        /// <summary>
        /// What dropping the creature on this slot of its row would do, given the side's other planned moves: the kind of
        /// move, and whether it's blocked (GDD 2.4 Impossible Plans) with a hint at what would make it work.
        /// </summary>
        public static (MoveKind kind, bool blocked, string hint) Evaluate(ICreature creature, BoardSlot to, IActionsQueue queue) {
            var kind = KindOf(creature, to);
            var moves = PlannedMovesOf(creature.Slot.Owner, queue).Where(m => m.Creature != creature).ToList();
            moves.Add(new PlannedMove(creature, to, kind)); // A new plan replaces the creature's old one, and is planned last
            bool blocked = Preview(creature.Slot.Owner.Battlefield, moves).Blocked.Contains(creature);
            string hint = !blocked ? null : kind switch {
                MoveKind.Walk => "needs Leapfrog",
                MoveKind.Push => "needs Swap",
                _ => "blocked"
            };
            return (kind, blocked, hint);
        }

        private static ICreature[] Occupants(IReadOnlyList<BoardSlot> row) => row.Select(s => s.OccupyingCreature).ToArray();
    }
}
