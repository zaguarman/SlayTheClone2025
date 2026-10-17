using System;
using System.Collections.Generic;
using System.Linq;

namespace Zeitgeist {
    /// <summary>
    /// Heat Signature ('Status Effects Compendium'): every effect with a randomly chosen target goes to the creature with the
    /// most Heat among those it can affect; random damage and redirected attacks to the one with the most Heat on either
    /// side. Ties: the
    /// nearest, first in the source's own row counted in slots, then in the other row counted by column; then at random.
    /// </summary>
    public static class HeatRules {
        public static int HeatOf(ICreature creature) => creature?.Heat ?? 0;

        /// <summary>The creature drawing random damage or a redirected attack, on either side, or null when no creature has Heat.</summary>
        /// <param name="attacker">Measures "nearest"; may be null (a spell), then ties are random.</param>
        /// <param name="side">Any player in the match: both sides' creatures count.</param>
        public static ICreature Hottest(ICreature attacker, IPlayer side, IEnumerable<ICreature> exclude = null) {
            if (side == null) return null;
            var excluded = new HashSet<ICreature>(exclude?.Where(c => c != null) ?? Enumerable.Empty<ICreature>());
            var creatures = side.Battlefield.Concat(side.Opponent?.Battlefield ?? Enumerable.Empty<BoardSlot>())
                .Select(s => s.OccupyingCreature)
                .Where(c => c != null && !excluded.Contains(c));
            return Hottest(attacker, creatures);
        }

        /// <summary>The one of these creatures drawing a random effect, or null when none of them has Heat.</summary>
        public static ICreature Hottest(ICreature source, IEnumerable<ICreature> candidates) {
            var creatures = candidates
                .Where(c => c != null && !c.IsDead)
                .Select(c => (creature: c, heat: HeatOf(c)))
                .Where(x => x.heat > 0)
                .ToList();
            if (creatures.Count == 0) return null;

            int most = creatures.Max(x => x.heat);
            return creatures.Where(x => x.heat == most)
                .OrderBy(x => Distance(source, x.creature))
                .ThenBy(_ => GameRandom.Next(int.MaxValue))
                .First().creature;
        }

        // Its own row first, by slots; then the other row, by column difference
        private static int Distance(ICreature from, ICreature to) {
            if (from?.Slot == null || to.Slot == null) return 0;
            int columns = Math.Abs(from.Slot.Index - to.Slot.Index);
            return to.Slot.Owner == from.Slot.Owner ? columns : 100 + columns;
        }
    }
}
