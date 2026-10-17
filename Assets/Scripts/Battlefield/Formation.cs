using System.Collections.Generic;
using System.Linq;

namespace Zeitgeist {
    /// <summary>
    /// The shape of a side's five slots (the user, 2026-10-15), as in a real war: the Vanguard alone in front (the wall that
    /// takes the melee), the two Flanks behind it (the shooters), and Headquarters at the back (the important people, hard to
    /// reach). Ours is a Λ, the enemy's a V, facing each other point to point:
    /// <code>
    ///   enemy   H1         H2      (Headquarters)
    ///              F1   F2         (Flanks)
    ///                 V            (Vanguard)
    ///                 V
    ///              F1   F2
    ///   ours    H1         H2
    /// </code>
    /// Slot indexes: 0 the Vanguard, 1 and 2 the Flanks (left, right), 3 and 4 Headquarters (left, right). A slot faces the
    /// one with the same index across the table (the same column).
    /// Steps (moving, and what's "beside" a slot): the Vanguard to either Flank, Flank to Flank, each Flank to the
    /// Headquarters slot behind it, and Headquarters to Headquarters: the back rows are never blocked by the ones ahead.
    /// Reach (an attack): the distance between two slots is how deep each sits (Vanguard 0, Flanks 1, Headquarters 2),
    /// added, plus 1. The slot decides: the Vanguard melees (distance 1, their Vanguard); the Flanks and Headquarters shoot
    /// (distance 2 to 3). Artillery overrides it: exactly 4, never less.
    /// </summary>
    public static class Formation {
        public enum Position { Vanguard, Flanks, Headquarters }

        public const int Size = 5;
        public const int Vanguard = 0;

        // The slots' bonuses (the user, 2026-10-15; built 2026-10-17): the Vanguard puts on Armor each Dawn, up to this much;
        // the Flanks shoot, +1 Attack and +1 Speed while a creature stands there; Headquarters has none (being hard to reach is its guard)
        public const int VanguardArmorCap = 2;
        public const int FlankAttack = 1;
        public const int FlankSpeed = 1;

        // Which slots are one step from each slot
        private static readonly int[][] Steps = {
            new[] { 1, 2 },    // Vanguard: both Flanks
            new[] { 0, 2, 3 }, // Left Flank: the Vanguard, the other Flank, the Headquarters slot behind it
            new[] { 0, 1, 4 }, // Right Flank
            new[] { 1, 4 },    // Left Headquarters: its Flank, the other Headquarters slot
            new[] { 2, 3 },    // Right Headquarters
        };

        // The column each slot sits in across the board, left to right: H1 F1 V F2 H2
        private static readonly int[] Columns = { 2, 1, 3, 0, 4 };

        public static Position PositionOf(int index) => index == Vanguard ? Position.Vanguard : index <= 2 ? Position.Flanks : Position.Headquarters;

        /// <summary>How deep the slot sits: the Vanguard 0, the Flanks 1, Headquarters 2.</summary>
        public static int Depth(int index) => (int)PositionOf(index);

        /// <summary>The column the slot sits in across the board, 0 (left) to 4.</summary>
        public static int Column(int index) => index >= 0 && index < Columns.Length ? Columns[index] : index;

        /// <summary>The slots one step from this one: where it can walk, and what's "beside" it.</summary>
        public static IReadOnlyList<int> Neighbours(int index) => index >= 0 && index < Steps.Length ? Steps[index] : new int[0];

        public static bool AreNeighbours(int a, int b) => Neighbours(a).Contains(b);

        /// <summary>The slot it trades places with when it's made to swap: its partner in the same row (Flank with Flank,
        /// Headquarters with Headquarters); the Vanguard, alone in its row, with the left Flank.</summary>
        public static int Partner(int index) => index switch { 0 => 1, 1 => 2, 2 => 1, 3 => 4, 4 => 3, _ => index };

        /// <summary>How many steps apart two slots of the same side are (0 for the same slot).</summary>
        public static int StepsBetween(int from, int to) {
            if (from == to) return 0;
            var seen = new HashSet<int> { from };
            var frontier = new List<int> { from };
            for (int steps = 1; frontier.Count > 0 && steps <= Size; steps++) {
                frontier = frontier.SelectMany(Neighbours).Where(seen.Add).ToList();
                if (frontier.Contains(to)) return steps;
            }
            return int.MaxValue;
        }

        /// <summary>The distance an attack covers, from a slot on one side to a slot on the other.</summary>
        public static int Distance(int attacker, int target) => Depth(attacker) + Depth(target) + 1;

        /// <summary>
        /// Whether a creature in this slot can attack that slot across the table: the Vanguard at distance 1 (melee), the
        /// Flanks and Headquarters at 2 to 3 (they shoot); with Artillery exactly 4.
        /// </summary>
        public static bool InReach(int attacker, int target, bool artillery = false) {
            int distance = Distance(attacker, target);
            if (artillery) return distance == 4;
            return PositionOf(attacker) == Position.Vanguard ? distance == 1 : distance >= 2 && distance <= 3;
        }

        public static string NameOf(Position position) => position switch {
            Position.Vanguard => "Vanguard",
            Position.Flanks => "Flanks",
            _ => "Headquarters"
        };

        public static string NameOf(int index) => NameOf(PositionOf(index));

        /// <summary>The slot in words: "Vanguard", "left Flank", "right Headquarters".</summary>
        public static string Describe(int index) => index switch {
            0 => "Vanguard",
            1 => "left Flank",
            2 => "right Flank",
            3 => "left Headquarters",
            4 => "right Headquarters",
            _ => $"slot {index + 1}"
        };
    }
}
