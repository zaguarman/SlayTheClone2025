namespace SlayTheClone {
    /// <summary>
    /// The game's single source of randomness: deck shuffles, random targets, chain directions and random summons.
    /// Resetting it with the same seed replays the same game, given the same moves.
    /// </summary>
    public static class GameRandom {
        private static System.Random random;

        public static int Seed { get; private set; }

        static GameRandom() => Reset(System.Environment.TickCount);

        public static void Reset(int seed) {
            Seed = seed;
            random = new System.Random(seed);
        }

        /// <summary>A random int from 0 up to, not including, <paramref name="maxExclusive"/>.</summary>
        public static int Next(int maxExclusive) => random.Next(maxExclusive);
    }
}
