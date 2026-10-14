using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// An Earthquake (HAARP): it starts at a random slot, either side, and zigzags out across the whole board, crossing to the
    /// other side at each step, one slot along: a hit in every column, the sides taking turns. Each hit deals the same damage to
    /// whatever creature is in that slot. Nobody's attack: nothing answers it or redirects it.
    /// </summary>
    public class EarthquakeAction : IGameAction {
        public IPlayer Controller { get; }
        public int Damage { get; }

        public EarthquakeAction(IPlayer controller, int damage) {
            Controller = controller;
            Damage = damage;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"EarthquakeAction: a zigzag of {Damage} damage across the board";
    }

    public class EarthquakeActionExecutor : ActionExecutor<EarthquakeAction> {
        protected override void ExecuteAction(EarthquakeAction quake, ActionExecutionContext context) {
            var near = quake.Controller;
            if (near?.Opponent == null) return;
            int columns = System.Math.Min(near.Battlefield.Count(), near.Opponent.Battlefield.Count());
            if (columns == 0) return;
            var path = Earthquake.Path(near, near.Opponent, GameRandom.Next(columns), GameRandom.Next(2) == 0);
            foreach (var slot in path) {
                var creature = slot.OccupyingCreature;
                if (creature == null || creature.IsDead) continue;
                Log($"Earthquake: {creature.Name} is shaken for {quake.Damage}", LogTag.Effects);
                context.ActionsQueue.AddAction(new DamageCreatureAction(creature, quake.Damage));
            }
        }
    }

    public static class Earthquake {
        /// <summary>
        /// The slots it hits, in order: the starting slot, then outward one column at a time both ways, switching sides at each
        /// step (so the starting side gets every other column, the other side the columns in between).
        /// </summary>
        public static List<BoardSlot> Path(IPlayer a, IPlayer b, int startColumn, bool startOnA) {
            var rows = new[] { a.Battlefield.ToList(), b.Battlefield.ToList() };
            int columns = System.Math.Min(rows[0].Count, rows[1].Count);
            var path = new List<BoardSlot>();
            for (int step = 0; step < columns; step++) {
                var row = rows[(startOnA ? 0 : 1) ^ (step % 2)];
                foreach (int column in step == 0 ? new[] { startColumn } : new[] { startColumn - step, startColumn + step }) {
                    if (column >= 0 && column < columns) path.Add(row[column]);
                }
            }
            return path;
        }
    }
}
