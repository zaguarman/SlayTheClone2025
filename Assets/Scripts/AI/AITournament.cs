using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// AI against AI, with no screen: whole combats played by the real rules (GameManager and TurnManager, as in a game) as
    /// fast as they run. <see cref="Run"/> pits the levels against each other with each deck on both sides (the same deck, so
    /// only the play differs), in both seats, and writes what came out. Each game has its own seed, so a run replays exactly.
    /// </summary>
    public static class AITournament {
        /// <summary>A combat still going after this many Days is called off: nobody wins it.</summary>
        public const int MaxDays = 40;

        public readonly struct Result {
            public readonly string Deck1, Deck2;
            public readonly AILevel Level1, Level2;
            /// <summary>1 or 2; 0 when nobody won (both at 0 on the same action, or called off).</summary>
            public readonly int Winner;
            public readonly int Days;

            public Result(string deck1, AILevel level1, string deck2, AILevel level2, int winner, int days) {
                (Deck1, Level1, Deck2, Level2, Winner, Days) = (deck1, level1, deck2, level2, winner, days);
            }

            public AILevel? WinningLevel => Winner == 1 ? Level1 : Winner == 2 ? Level2 : (AILevel?)null;
        }

        /// <summary>Plays one combat to its end (or <see cref="MaxDays"/>): the AI at each level plans its seat each Day.</summary>
        public static Result Play(DeckScriptableObject deck1, AILevel level1, DeckScriptableObject deck2, AILevel level2, int seed) {
            var root = new GameObject("AI Match") { hideFlags = HideFlags.HideAndDontSave };
            var manager = root.AddComponent<GameManager>();
            var turns = root.AddComponent<TurnManager>();
            try {
                manager.RandomSeed = seed;
                var mediator = new GameMediator();
                bool over = false;
                mediator.GameOver += _ => over = true;
                manager.Initialize(mediator, deck1.GetCardDataList(), deck2.GetCardDataList(), turns, new SimpleModifierFactory());
                var first = new AIPlanner(manager.ActionsQueue, manager.CombatHandler, level1, seed);
                var second = new AIPlanner(manager.ActionsQueue, manager.CombatHandler, level2, seed + 1);

                while (!over && turns.TurnNumber < MaxDays) {
                    first.PlanDay(manager.Player1);
                    second.PlanDay(manager.Player2);
                    turns.EndTurnImmediately();
                }

                bool firstDown = manager.Player1.Health <= 0, secondDown = manager.Player2.Health <= 0;
                int winner = firstDown == secondDown ? 0 : firstDown ? 2 : 1;
                return new Result(deck1.GetDeckName(), level1, deck2.GetDeckName(), level2, winner, turns.TurnNumber);
            }
            finally {
                manager.ModifierManager?.Cleanup();
                manager.ActionsQueue?.Cleanup();
                Object.DestroyImmediate(root);
            }
        }

        private static readonly (AILevel, AILevel)[] Pairings = {
            (AILevel.Easy, AILevel.Medium), (AILevel.Medium, AILevel.Hard), (AILevel.Easy, AILevel.Hard)
        };

        /// <summary>
        /// Each pairing of levels plays each deck against itself, <paramref name="gamesPerSeat"/> times with each level in
        /// each seat. Returns every result; <paramref name="report"/> sums them up.
        /// </summary>
        public static List<Result> Run(IReadOnlyList<DeckScriptableObject> decks, int gamesPerSeat, out string report,
            System.Action<float> progress = null) {
            bool muted = DebugLogger.MuteInfo;
            DebugLogger.MuteInfo = true; // Thousands of lines a Day otherwise
            var results = new List<Result>();
            int total = decks.Count * Pairings.Length * 2 * gamesPerSeat, seed = 1000;
            try {
                foreach (var deck in decks) {
                    foreach (var (a, b) in Pairings) {
                        for (int game = 0; game < gamesPerSeat; game++) {
                            results.Add(Play(deck, a, deck, b, seed++));
                            results.Add(Play(deck, b, deck, a, seed++));
                            progress?.Invoke(results.Count / (float)total);
                        }
                    }
                }
            }
            finally {
                DebugLogger.MuteInfo = muted;
            }
            report = Report(results, decks);
            return results;
        }

        private static string Report(List<Result> results, IReadOnlyList<DeckScriptableObject> decks) {
            var text = new StringBuilder();
            text.AppendLine("AI TOURNAMENT");
            text.AppendLine($"Each deck against itself, each pairing of levels, both seats. {results.Count} games, called off after {MaxDays} Days.");
            text.AppendLine();

            text.AppendLine("Wins by pairing (draws: both at 0 at once, or called off)");
            foreach (var (a, b) in Pairings) {
                var games = results.Where(r => (r.Level1 == a && r.Level2 == b) || (r.Level1 == b && r.Level2 == a)).ToList();
                int aWins = games.Count(r => r.WinningLevel == a), bWins = games.Count(r => r.WinningLevel == b);
                text.AppendLine($"  {a} vs {b}: {aWins} - {bWins}, {games.Count - aWins - bWins} draws "
                    + $"({Percent(bWins, games.Count)} for {b}), {games.Average(r => r.Days):0.0} Days on average");
            }
            text.AppendLine();

            text.AppendLine("Overall, every game a level played");
            foreach (AILevel level in System.Enum.GetValues(typeof(AILevel))) {
                var games = results.Where(r => r.Level1 == level || r.Level2 == level).ToList();
                text.AppendLine($"  {level}: won {Percent(games.Count(r => r.WinningLevel == level), games.Count)} of {games.Count}");
            }
            text.AppendLine();

            int firstSeat = results.Count(r => r.Winner == 1), secondSeat = results.Count(r => r.Winner == 2);
            text.AppendLine($"By seat: Player 1 won {firstSeat}, Player 2 won {secondSeat} (Player 1 plans first; neither looks at the other's plans)");
            text.AppendLine();

            text.AppendLine("By deck: the better level's wins out of the games each pairing played with it");
            foreach (var deck in decks.Select(d => d.GetDeckName())) {
                var line = Pairings.Select(p => {
                    var games = results.Where(r => r.Deck1 == deck && ((r.Level1 == p.Item1 && r.Level2 == p.Item2) || (r.Level1 == p.Item2 && r.Level2 == p.Item1))).ToList();
                    return $"{p.Item2} over {p.Item1} {games.Count(r => r.WinningLevel == p.Item2)}/{games.Count}";
                });
                text.AppendLine($"  {deck}: {string.Join(", ", line)}");
            }
            return text.ToString();
        }

        private static string Percent(int part, int whole) => whole == 0 ? "-" : $"{100f * part / whole:0}%";
    }
}
