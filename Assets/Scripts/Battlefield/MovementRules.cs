using System;
using System.Collections.Generic;
using System.Linq;
using static Zeitgeist.Enums;

namespace Zeitgeist {
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
    /// How creatures move in their formation (GDD 2.4 Slot Occupation &amp; Movement; the shape is <see cref="Formation"/>):
    /// a step goes to a slot one step away (the Vanguard to either Flank, Flank to Flank, a Flank to the Headquarters slot
    /// behind it, Headquarters to Headquarters), so the back rows are never blocked by the ones ahead. By default a creature
    /// walks to an empty slot along empty slots, or swaps with the friendly creature one step away (melee range; the user,
    /// 2026-10-16, Push archived). Leapfrog jumps over creatures to any empty slot; the Swap keyword swaps at any distance.
    /// A Rooted creature neither moves nor is moved.
    /// </summary>
    public static class MovementRules {
        /// <summary>
        /// Whether a creature is Rooted: it can't be moved or swapped. The Rooted status, or a Clingy Ex facing it. Read from the
        /// game's own modifiers (null: no status): the AI's games run side by side, so nothing static (the user, 2026-10-17).
        /// </summary>
        public static bool IsRooted(ICreature creature, IModifierManager modifiers) =>
            creature != null && ((modifiers?.HasStatusEffect(creature, StatusEffectType.Rooted) ?? false) || Abilities.IsPinned(creature));

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
        /// creature uses the best option it has (Leapfrog over walking for an empty slot; a swap for an occupied one).
        /// </summary>
        public static MoveKind KindOf(ICreature creature, BoardSlot to) {
            var movement = creature?.Movement ?? MovementKeyword.None;
            bool occupied = to != null && to.IsOccupied() && to.OccupyingCreature != creature;
            if (occupied) return MoveKind.Swap;
            return movement.HasFlag(MovementKeyword.Leapfrog) ? MoveKind.Leapfrog : MoveKind.Walk;
        }

        /// <summary>
        /// As above, given the side's planned moves (the user): a slot whose creature is planned to leave it (for anywhere
        /// but this creature's own slot) will be empty, so it's walked into, not swapped: both go where they were sent.
        /// </summary>
        public static MoveKind KindOf(ICreature creature, BoardSlot to, IActionsQueue queue) {
            var kind = KindOf(creature, to);
            if (kind != MoveKind.Swap || queue == null || to?.Owner == null) return kind;
            var occupant = to.OccupyingCreature;
            var leaving = PlannedMovesOf(to.Owner, queue).Where(m => m.Creature == occupant).Select(m => (PlannedMove?)m).FirstOrDefault();
            if (leaving == null || leaving.Value.To == creature.Slot || leaving.Value.To == to) return kind;
            return (creature.Movement & MovementKeyword.Leapfrog) != 0 ? MoveKind.Leapfrog : MoveKind.Walk;
        }

        // The Swap keyword: it swaps at any distance; without it, only with the creature one step away
        private static bool SwapsAnywhere(ICreature creature) => (creature?.Movement ?? MovementKeyword.None).HasFlag(MovementKeyword.Swap);

        /// <summary>
        /// Where the move would end up if it resolved now. Blocked on the way, the creature stops at the furthest empty slot
        /// it can still reach toward its destination (GDD 2.4 Blocked Before It Resolves); if there's none, it stays put.
        /// </summary>
        public static MoveOutcome Resolve(ICreature creature, BoardSlot to, MoveKind kind, IModifierManager modifiers = null) {
            var from = creature?.Slot;
            if (from == null || to == null || to == from || to.Owner != from.Owner) return new MoveOutcome(from, to == from);

            var row = from.Owner.Battlefield;
            var outcome = Resolve(Occupants(row), from.Index, to.Index, kind, SwapsAnywhere(creature), modifiers);
            return new MoveOutcome(row[outcome.End], outcome.Reaches,
                outcome.PartnerEnd >= 0 ? to.OccupyingCreature : null,
                outcome.PartnerEnd >= 0 ? row[outcome.PartnerEnd] : null);
        }

        private static RowOutcome Resolve(ICreature[] row, int from, int to, MoveKind kind, bool swapsAnywhere, IModifierManager modifiers) {
            bool occupied = row[to] != null;
            var reachable = Reachable(row, from, overCreatures: kind == MoveKind.Leapfrog);
            switch (kind) {
                case MoveKind.Swap:
                    // In melee range (a step away), or anywhere with the Swap keyword, over the creatures between; a Rooted
                    // one can't be swapped. With nobody there any more, it walks there like any move
                    if (!occupied) return reachable.Contains(to) || swapsAnywhere ? new RowOutcome(to, true) : Closest(row, from, to, reachable);
                    if ((swapsAnywhere || Formation.AreNeighbours(from, to)) && !IsRooted(row[to], modifiers)) return new RowOutcome(to, true, from);
                    return Closest(row, from, to, reachable);

                default: // Walk along empty slots; Leapfrog to any empty slot, over creatures
                    if (!occupied) return reachable.Contains(to) ? new RowOutcome(to, true) : Closest(row, from, to, reachable);
                    return Closest(row, from, to, reachable);
            }
        }

        // The slots it can get to: along empty slots, or (Leapfrog) to any empty slot, over creatures
        private static HashSet<int> Reachable(ICreature[] row, int from, bool overCreatures) {
            var reached = new HashSet<int>();
            if (overCreatures) {
                for (int i = 0; i < row.Length; i++) if (i != from && row[i] == null) reached.Add(i);
                return reached;
            }
            var frontier = new Queue<int>();
            frontier.Enqueue(from);
            while (frontier.Count > 0) {
                foreach (int next in Formation.Neighbours(frontier.Dequeue())) {
                    if (next < 0 || next >= row.Length || next == from || row[next] != null || !reached.Add(next)) continue;
                    frontier.Enqueue(next);
                }
            }
            return reached;
        }

        // Blocked on the way: the empty slot it can reach closest to where it was going, if that's closer than where it
        // stands (GDD 2.4 Blocked Before It Resolves); else it stays put
        private static RowOutcome Closest(ICreature[] row, int from, int to, HashSet<int> reachable) {
            int best = from, bestSteps = Formation.StepsBetween(from, to);
            foreach (int i in reachable.OrderBy(i => i)) {
                if (row[i] != null) continue;
                int steps = Formation.StepsBetween(i, to);
                if (steps < bestSteps) {
                    best = i;
                    bestSteps = steps;
                }
            }
            return new RowOutcome(best, false);
        }

        /// <summary>Carries out a move's outcome on the board.</summary>
        public static void Apply(ICreature creature, MoveKind kind, MoveOutcome outcome) {
            var from = creature.Slot;
            if (from == null || outcome.End == null || outcome.End == from) return;

            // A swap: the two trade places
            if (outcome.Partner != null) {
                BoardSlot.Swap(from, outcome.End);
                return;
            }
            from.Clear();
            outcome.End.Place(creature);
        }

        private static void Apply(ICreature[] row, int from, MoveKind kind, RowOutcome outcome) {
            if (outcome.End == from) return;
            var mover = row[from];
            var partner = row[outcome.End];
            row[from] = partner; // A swap's partner takes the mover's slot; otherwise it's left empty
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
        public static FormationPreview Preview(IReadOnlyList<BoardSlot> row, IEnumerable<PlannedMove> plannedMoves, IModifierManager modifiers = null) {
            var occupants = Occupants(row);
            int IndexOf(ICreature creature) => System.Array.IndexOf(occupants, creature);

            var pending = plannedMoves.Where(m => m.Creature != null && m.To != null && IndexOf(m.Creature) >= 0).ToList();
            var blocked = new HashSet<ICreature>();
            while (pending.Count > 0) {
                var leaving = new HashSet<ICreature>(pending.Select(m => m.Creature));
                RowOutcome OutcomeOf(PlannedMove m) => m.To.Index == IndexOf(m.Creature)
                    ? new RowOutcome(m.To.Index, true)
                    : Resolve(occupants, IndexOf(m.Creature), m.To.Index, m.Kind, SwapsAnywhere(m.Creature), modifiers);
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
            var kind = KindOf(creature, to, queue);
            var moves = PlannedMovesOf(creature.Slot.Owner, queue).Where(m => m.Creature != creature).ToList();
            moves.Add(new PlannedMove(creature, to, kind)); // A new plan replaces the creature's old one, and is planned last
            bool blocked = Preview(creature.Slot.Owner.Battlefield, moves).Blocked.Contains(creature);
            string hint = !blocked ? null
                : kind == MoveKind.Swap && to.OccupyingCreature != null && IsRooted(to.OccupyingCreature, queue?.Modifiers) ? "Rooted: it can't be moved"
                : kind switch {
                    MoveKind.Walk => "needs Leapfrog",
                    MoveKind.Swap => "too far: needs Swap",
                    _ => "blocked"
                };
            return (kind, blocked, hint);
        }

        private static ICreature[] Occupants(IReadOnlyList<BoardSlot> row) => row.Select(s => s.OccupyingCreature).ToArray();
    }
}
