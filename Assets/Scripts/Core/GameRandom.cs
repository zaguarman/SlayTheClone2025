namespace Zeitgeist {
    /// <summary>
    /// The game's single source of randomness: deck shuffles, random targets, chain directions and random summons.
    /// Resetting it with the same seed replays the same game, given the same moves. Each thread has its own, so games
    /// played side by side on other threads (the AI tournament) don't draw from each other's.
    /// </summary>
    public static class GameRandom {
        [System.ThreadStatic] private static System.Random random;
        [System.ThreadStatic] private static int seed;

        public static int Seed => seed;

        public static void Reset(int newSeed) {
            seed = newSeed;
            random = new System.Random(newSeed);
        }

        /// <summary>A random int from 0 up to, not including, <paramref name="maxExclusive"/>.</summary>
        public static int Next(int maxExclusive) {
            if (random == null) Reset(System.Environment.TickCount ^ System.Threading.Thread.CurrentThread.ManagedThreadId);
            return random.Next(maxExclusive);
        }
    }
}
