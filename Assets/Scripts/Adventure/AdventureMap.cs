using System;
using System.Collections.Generic;
using System.Linq;

namespace SlayTheClone {
    /// <summary>What a room on the adventure map holds. For now only the fights do something; the rest are walked through.</summary>
    public enum RoomType { Fight, Elite, Unknown, Rest, Shop, Treasure, Boss }

    /// <summary>A room on the map: its row (0 at the bottom) and column, what it holds, and the rooms its paths lead to.</summary>
    public class MapNode {
        public int Row { get; }
        public int Column { get; }
        public RoomType Type { get; internal set; }
        public List<MapNode> Next { get; } = new List<MapNode>();
        public List<MapNode> Previous { get; } = new List<MapNode>();
        public string Id => $"{Row}:{Column}";
        public bool IsCombat => Type is RoomType.Fight or RoomType.Elite or RoomType.Boss;

        public MapNode(int row, int column, RoomType type = RoomType.Fight) {
            Row = row;
            Column = column;
            Type = type;
        }

        public override string ToString() => $"{Type} {Id}";
    }

    /// <summary>
    /// An act's map, built like Slay the Spire 2's (see 'Adventure Map.txt'): a grid of <see cref="Rows"/> rows and
    /// <see cref="Columns"/> columns drawn by six paths from the bottom up, each step to one of the three rooms above, never
    /// crossing another path; the rooms no path reaches aren't there. Then every room is given what it holds:
    /// • Fixed rows: the first is all fights, the one 7 rows from the end all treasure, the last all rest sites; the boss
    ///   waits above, reached from every room of the last row.
    /// • The rest, from set counts (Slay the Spire 2 counts rooms where the first game rolled percentages): 5 elites,
    ///   3 shops, 6 rest sites, 10 to 14 unknown rooms, and fights for every other room, all in one shuffled bag the size
    ///   of the map, so the special rooms spread over every row; each room draws one at random among those it may hold.
    /// • Placement: no elite or rest site in the first 5 rows; no rest site just before the last row (it's all rest sites);
    ///   an elite, shop or rest site never leads straight into another of its kind; and where a path forks, the branches
    ///   hold different kinds of room (fights aside), so every fork is a choice.
    /// </summary>
    public class AdventureMap {
        public const int Rows = 15, Columns = 7, Paths = 6;
        public const int SafeRows = 5; // No elite or rest site before this row
        public const int TreasureRow = Rows - 7;
        public const int Elites = 5, Shops = 3, RestSites = 6, MinUnknown = 10, MaxUnknown = 14;

        public List<MapNode> Nodes { get; } = new List<MapNode>();
        public MapNode Boss { get; private set; }
        public int Seed { get; private set; }

        public IEnumerable<MapNode> Row(int row) => Nodes.Where(n => n.Row == row).OrderBy(n => n.Column);
        public MapNode Find(string id) => id == Boss?.Id ? Boss : Nodes.FirstOrDefault(n => n.Id == id);

        public static AdventureMap Generate(int seed) {
            var map = new AdventureMap { Seed = seed };
            var random = new Random(seed);
            map.DrawPaths(random);
            map.AssignRooms(random);
            return map;
        }

        #region Paths
        private void DrawPaths(Random random) {
            var grid = new MapNode[Rows, Columns];
            MapNode At(int row, int column) => grid[row, column] ??= new MapNode(row, column);
            int firstStart = -1;
            for (int path = 0; path < Paths; path++) {
                int column = random.Next(Columns);
                if (path == 1) while (column == firstStart) column = random.Next(Columns); // At least two ways in
                if (path == 0) firstStart = column;
                var node = At(0, column);
                for (int row = 1; row < Rows; row++) {
                    int from = node.Column;
                    // One of the three rooms above, never crossing a path already drawn between these two rows
                    var options = new[] { from - 1, from, from + 1 }.Where(c => c >= 0 && c < Columns && !Crosses(grid, row - 1, from, c)).ToList();
                    int to = options.Count > 0 ? options[random.Next(options.Count)] : from; // Straight up never crosses
                    var next = At(row, to);
                    if (!node.Next.Contains(next)) {
                        node.Next.Add(next);
                        next.Previous.Add(node);
                    }
                    node = next;
                }
            }
            for (int row = 0; row < Rows; row++)
                for (int column = 0; column < Columns; column++)
                    if (grid[row, column] != null) Nodes.Add(grid[row, column]);

            Boss = new MapNode(Rows, Columns / 2, RoomType.Boss);
            foreach (var last in Row(Rows - 1)) {
                last.Next.Add(Boss);
                Boss.Previous.Add(last);
            }
        }

        // Whether a step from (row, from) to (row + 1, to) would cross a step already drawn between those rows
        private static bool Crosses(MapNode[,] grid, int row, int from, int to) {
            for (int column = 0; column < Columns; column++) {
                var other = grid[row, column];
                if (other == null || column == from) continue;
                foreach (var next in other.Next) {
                    if ((column < from && next.Column > to) || (column > from && next.Column < to)) return true;
                }
            }
            return false;
        }
        #endregion

        #region Rooms
        private void AssignRooms(Random random) {
            foreach (var node in Nodes) {
                if (node.Row == 0) node.Type = RoomType.Fight;
                else if (node.Row == TreasureRow) node.Type = RoomType.Treasure;
                else if (node.Row == Rows - 1) node.Type = RoomType.Rest;
            }

            var open = Nodes.Where(n => n.Row != 0 && n.Row != TreasureRow && n.Row != Rows - 1).OrderBy(n => n.Row).ThenBy(n => n.Column).ToList();
            // As Slay the Spire does: the bag holds a room for every open spot, the fights included, so the special rooms
            // spread over the whole map instead of the first rows taking them all (the rest sites bunched after row 5)
            var specials = Enumerable.Repeat(RoomType.Elite, Elites)
                .Concat(Enumerable.Repeat(RoomType.Shop, Shops))
                .Concat(Enumerable.Repeat(RoomType.Rest, RestSites))
                .Concat(Enumerable.Repeat(RoomType.Unknown, random.Next(MinUnknown, MaxUnknown + 1))).ToList();
            var pool = specials.Concat(Enumerable.Repeat(RoomType.Fight, Math.Max(0, open.Count - specials.Count)))
                .OrderBy(_ => random.Next()).ToList();
            var assigned = new HashSet<MapNode>(Nodes.Where(n => !open.Contains(n)));
            foreach (var node in open) {
                // A random one of the rooms it may hold: taking the first allowed would leave what the early rows can't
                // hold (elites, rest sites) at the front of the bag, all landing on the first row that can
                var fits = Enumerable.Range(0, pool.Count).Where(i => Allowed(node, pool[i], assigned)).ToList();
                int pick = fits.Count > 0 ? fits[random.Next(fits.Count)] : -1;
                node.Type = pick >= 0 ? pool[pick] : RoomType.Fight;
                if (pick >= 0) pool.RemoveAt(pick);
                assigned.Add(node);
            }
        }

        /// <summary>Whether the room can hold this, by the placement rules (see the class summary).</summary>
        public static bool Allowed(MapNode node, RoomType type, ICollection<MapNode> assigned = null) {
            if (type == RoomType.Fight) return true;
            if ((type is RoomType.Elite or RoomType.Rest) && node.Row < SafeRows) return false;
            if (type == RoomType.Rest && node.Row == Rows - 2) return false;
            bool Known(MapNode other) => assigned == null || assigned.Contains(other);
            // Never two elites, shops or rest sites in a row
            if (type is RoomType.Elite or RoomType.Shop or RoomType.Rest
                && node.Previous.Concat(node.Next).Any(n => Known(n) && n.Type == type && n != node)) return false;
            // A fork's branches hold different rooms
            return !node.Previous.SelectMany(parent => parent.Next).Any(sibling => sibling != node && Known(sibling) && sibling.Type == type);
        }
        #endregion
    }
}
