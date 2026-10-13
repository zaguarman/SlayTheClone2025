using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// AI against AI, with no screen: whole combats played by the real rules (GameManager and TurnManager, as in a game) as
    /// fast as they run. <see cref="RunLevels"/> pits the levels against each other, <see cref="RunStyles"/> the Hard styles
    /// (and Medium, as a yardstick), each deck on both sides (the same deck, so only the play differs) and in both seats.
    /// Every game is audited (see <see cref="GameAudit"/>): what breaks the rules is reported with where it happened. Each
    /// game has its own seed, so a run replays exactly.
    /// </summary>
    public static class AITournament {
        /// <summary>A combat still going after this many Days is called off: nobody wins it.</summary>
        public const int MaxDays = 40;

        public readonly struct Result {
            public readonly string Deck;
            public readonly AIEntrant First, Second;
            /// <summary>1 or 2; 0 when nobody won (both at 0 on the same action, or called off).</summary>
            public readonly int Winner;
            public readonly int Days;

            public Result(string deck, AIEntrant first, AIEntrant second, int winner, int days) {
                (Deck, First, Second, Winner, Days) = (deck, first, second, winner, days);
            }

            public AILevel Level1 => First.Level;
            public AILevel Level2 => Second.Level;
            public string WinnerName => Winner == 1 ? First.Name : Winner == 2 ? Second.Name : null;
            public AILevel? WinningLevel => Winner == 1 ? First.Level : Winner == 2 ? Second.Level : (AILevel?)null;
            public bool Between(string a, string b) => (First.Name == a && Second.Name == b) || (First.Name == b && Second.Name == a);
        }

        /// <summary>Plays one combat to its end (or <see cref="MaxDays"/>): each AI plans its seat each Day.</summary>
        public static Result Play(DeckScriptableObject deck1, AIEntrant first, DeckScriptableObject deck2, AIEntrant second, int seed,
            GameAudit audit = null) {
            var root = new GameObject("AI Match") { hideFlags = HideFlags.HideAndDontSave };
            var manager = root.AddComponent<GameManager>();
            var turns = root.AddComponent<TurnManager>();
            string where = $"{deck1.GetDeckName()}{(deck2 != deck1 ? " vs " + deck2.GetDeckName() : "")}, {first} vs {second}, seed {seed}";
            try {
                manager.RandomSeed = seed;
                var mediator = new GameMediator();
                bool over = false;
                mediator.GameOver += _ => over = true;
                manager.Initialize(mediator, deck1.GetCardDataList(), deck2.GetCardDataList(), turns, new SimpleModifierFactory());
                var planners = new[] { first.Planner(manager, seed), second.Planner(manager, seed + 1) };
                audit?.Begin(manager, where);

                while (!over && turns.TurnNumber < MaxDays) {
                    planners[0].PlanDay(manager.Player1);
                    planners[1].PlanDay(manager.Player2);
                    audit?.CheckPlans(turns.TurnNumber);
                    int day = turns.TurnNumber;
                    turns.EndTurnImmediately();
                    audit?.CheckDay(day, advanced: turns.TurnNumber == day + 1, over);
                }

                bool firstDown = manager.Player1.Health <= 0, secondDown = manager.Player2.Health <= 0;
                int winner = firstDown == secondDown ? 0 : firstDown ? 2 : 1;
                return new Result(deck1.GetDeckName(), first, second, winner, turns.TurnNumber);
            }
            catch (System.Exception exception) {
                audit?.Note("Exception", $"{exception.GetType().Name}: {exception.Message}", $"{where}, Day {turns.TurnNumber}\n{exception.StackTrace}");
                return new Result(deck1.GetDeckName(), first, second, 0, turns.TurnNumber);
            }
            finally {
                audit?.End();
                manager.ModifierManager?.Cleanup();
                manager.ActionsQueue?.Cleanup();
                Object.DestroyImmediate(root);
            }
        }

        public static readonly AIEntrant[] Levels = { new AIEntrant(AILevel.Easy), new AIEntrant(AILevel.Medium), new AIEntrant(AILevel.Hard) };

        /// <summary>Medium, as a yardstick, then every Hard style.</summary>
        public static readonly AIEntrant[] Styles =
            new[] { new AIEntrant(AILevel.Medium) }.Concat(AIStyle.All.Select(s => new AIEntrant(AILevel.Hard, s))).ToArray();

        /// <summary>The levels against each other: every pairing, every deck against itself, both seats.</summary>
        public static List<Result> RunLevels(IReadOnlyList<DeckScriptableObject> decks, int gamesPerSeat, out string report,
            System.Action<float> progress = null) =>
            Run("AI TOURNAMENT: THE LEVELS", Levels, decks, gamesPerSeat, out report, progress);

        /// <summary>The Hard styles (and Medium) against each other: every pairing, every deck against itself, both seats.</summary>
        public static List<Result> RunStyles(IReadOnlyList<DeckScriptableObject> decks, int gamesPerSeat, out string report,
            System.Action<float> progress = null) =>
            Run("AI TOURNAMENT: THE HARD STYLES", Styles, decks, gamesPerSeat, out report, progress);

        private static List<Result> Run(string title, AIEntrant[] entrants, IReadOnlyList<DeckScriptableObject> decks, int gamesPerSeat,
            out string report, System.Action<float> progress) {
            bool muted = DebugLogger.MuteInfo;
            DebugLogger.MuteInfo = true; // Thousands of lines a Day otherwise
            var audit = new GameAudit();
            var results = new List<Result>();
            var pairings = (from i in Enumerable.Range(0, entrants.Length) from j in Enumerable.Range(i + 1, entrants.Length - i - 1)
                            select (entrants[i], entrants[j])).ToList();
            int total = decks.Count * pairings.Count * 2 * gamesPerSeat, seed = 1000;
            try {
                foreach (var deck in decks) {
                    foreach (var (a, b) in pairings) {
                        for (int game = 0; game < gamesPerSeat; game++) {
                            results.Add(Play(deck, a, deck, b, seed++, audit));
                            results.Add(Play(deck, b, deck, a, seed++, audit));
                            progress?.Invoke(results.Count / (float)total);
                        }
                    }
                }
            }
            finally {
                DebugLogger.MuteInfo = muted;
            }
            report = Report(title, results, entrants, pairings, decks) + "\n" + audit.Report();
            Problems = audit.Problems;
            return results;
        }

        /// <summary>What the last run's audit found.</summary>
        public static IReadOnlyList<GameAudit.Problem> Problems { get; private set; } = new List<GameAudit.Problem>();

        private static string Report(string title, List<Result> results, AIEntrant[] entrants, List<(AIEntrant, AIEntrant)> pairings,
            IReadOnlyList<DeckScriptableObject> decks) {
            var text = new StringBuilder();
            text.AppendLine(title);
            text.AppendLine($"Each deck against itself, each pairing, both seats. {results.Count} games, called off after {MaxDays} Days.");
            text.AppendLine();

            text.AppendLine("Ranking: share of all its games won (draws count as not won)");
            foreach (var entrant in entrants.OrderByDescending(e => WinShare(results, e.Name))) {
                var games = results.Where(r => r.First.Name == entrant.Name || r.Second.Name == entrant.Name).ToList();
                string about = entrant.Level == AILevel.Hard ? $"  {entrant.Style.Description}" : "";
                text.AppendLine($"  {entrant.Name,-16} {Percent(games.Count(r => r.WinnerName == entrant.Name), games.Count),5} of {games.Count}{about}");
            }
            text.AppendLine();

            text.AppendLine("Head to head: the row's wins - the column's wins");
            text.AppendLine("  " + new string(' ', 16) + string.Join("", entrants.Select(e => $"{Short(e.Name),12}")));
            foreach (var row in entrants) {
                var cells = entrants.Select(column => {
                    if (row.Name == column.Name) return $"{"-",12}";
                    var games = results.Where(r => r.Between(row.Name, column.Name)).ToList();
                    return $"{games.Count(r => r.WinnerName == row.Name) + " - " + games.Count(r => r.WinnerName == column.Name),12}";
                });
                text.AppendLine($"  {row.Name,-16}" + string.Join("", cells));
            }
            text.AppendLine();

            int firstSeat = results.Count(r => r.Winner == 1), secondSeat = results.Count(r => r.Winner == 2);
            text.AppendLine($"Seats: Player 1 won {firstSeat}, Player 2 won {secondSeat}, {results.Count - firstSeat - secondSeat} draws; {results.Average(r => r.Days):0.0} Days on average");
            text.AppendLine();

            text.AppendLine("By deck: which AI won the most with it");
            foreach (var deck in decks.Select(d => d.GetDeckName())) {
                var games = results.Where(r => r.Deck == deck).ToList();
                var ranked = entrants.Select(e => (e.Name, Share: WinShare(games, e.Name))).OrderByDescending(e => e.Share).ToList();
                string last = ranked.Count > 3 ? $"; last {ranked.Last().Name} {ranked.Last().Share:0%}" : "";
                text.AppendLine($"  {deck}: {string.Join(", ", ranked.Take(3).Select(e => $"{e.Name} {e.Share:0%}"))}{last}");
            }
            return text.ToString();
        }

        private static float WinShare(List<Result> results, string name) {
            var games = results.Where(r => r.First.Name == name || r.Second.Name == name).ToList();
            return games.Count == 0 ? 0 : games.Count(r => r.WinnerName == name) / (float)games.Count;
        }

        private static string Short(string name) => name.Replace("Hard ", "");
        private static string Percent(int part, int whole) => whole == 0 ? "-" : $"{100f * part / whole:0}%";
    }

    /// <summary>
    /// Watches AI games for bugs: after every Day it checks what must always hold (no card lost or in two places, slots and
    /// creatures agree, no dead or 0-Health creature left on the board, Health within bounds, the queue empty, the Day
    /// moved on), checks the AI only planned cards from its own hand, and collects every error, exception and warning the
    /// game logged. Each kind of problem is counted once with its first example.
    /// </summary>
    public sealed class GameAudit {
        public sealed class Problem {
            public string Kind { get; internal set; }
            public string Message { get; internal set; }
            public string FirstSeen { get; internal set; }
            public int Count { get; internal set; }
        }

        private readonly Dictionary<string, Problem> problems = new Dictionary<string, Problem>();
        private GameManager game;
        private string where;
        private int day;
        private Dictionary<Player, int> cardCounts;

        public IReadOnlyList<Problem> Problems => problems.Values.OrderBy(p => p.Kind == "Warning").ThenByDescending(p => p.Count).ToList();

        internal void Begin(GameManager manager, string match) {
            this.game = manager;
            where = match;
            day = 0;
            cardCounts = Players.ToDictionary(p => p, CountCards);
            Application.logMessageReceived += OnLog;
        }

        internal void End() => Application.logMessageReceived -= OnLog;

        private IEnumerable<Player> Players => new[] { game.Player1, game.Player2 };

        private void OnLog(string message, string stackTrace, LogType type) {
            if (type == LogType.Log) return;
            Note(type == LogType.Warning ? "Warning" : type.ToString(), message, $"{where}, Day {day}");
        }

        public void Note(string kind, string message, string at) {
            string key = kind + ": " + Normalize(message);
            if (!problems.TryGetValue(key, out var problem)) {
                problems[key] = problem = new Problem { Kind = kind, Message = Normalize(message), FirstSeen = at };
            }
            problem.Count++;
        }

        // The same problem with other creatures' ids, colors and numbers is the same problem
        private static string Normalize(string message) {
            message = Regex.Replace(message, "<[^>]+>", "");
            message = Regex.Replace(message, "[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}", "<id>");
            message = Regex.Replace(message, @"\d+", "#");
            return message.Split('\n')[0].Trim();
        }

        internal void CheckPlans(int today) {
            day = today;
            foreach (var play in game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>()) {
                if (!play.GetOwner().Hand.Contains(play.GetCard())) Note("Rules", "A card was planned that isn't in its player's hand", $"{where}, Day {day}: {play.GetCard().Name}");
            }
        }

        internal void CheckDay(int endedDay, bool advanced, bool over) {
            string at = $"{where}, Day {endedDay}";
            if (!advanced && !over) Note("Rules", "Ending the turn didn't start the next Day", at);
            if (game.ActionsQueue.GetPendingActionsCount() > 0) {
                Note("Rules", "Actions left in the queue after the turn", $"{at}: {string.Join(", ", game.ActionsQueue.GetPendingActions().Select(a => a.GetType().Name))}");
            }
            var placed = new HashSet<ICreature>();
            foreach (var player in Players) {
                if (CountCards(player) != cardCounts[player]) {
                    Note("Rules", "A player lost or gained cards", $"{at}: {player.Name} has {CountCards(player)}, had {cardCounts[player]}");
                    cardCounts[player] = CountCards(player);
                }
                var seen = new HashSet<ICard>();
                foreach (var card in player.Deck.GetDeckPreview().Concat(player.Hand).Concat(player.Deck.GetDiscardPilePreview())
                             .Concat(player.Battlefield.Where(s => s.IsOccupied()).Select(s => (ICard)s.OccupyingCreature))) {
                    if (!seen.Add(card)) Note("Rules", "A card is in two places at once", $"{at}: {card.Name} ({player.Name})");
                }
                if (player.Health < 0 || player.Health > Player.MaxHealth) Note("Rules", "A player's Health is out of bounds", $"{at}: {player.Name} at {player.Health}");
                if (player.Hand.Count > Player.MaxHandSize) Note("Rules", "A hand is over the maximum", $"{at}: {player.Name} holds {player.Hand.Count}");
                foreach (var slot in player.Battlefield) {
                    var creature = slot.OccupyingCreature;
                    if (creature == null) continue;
                    if (creature.Slot != slot) Note("Rules", "A creature and its slot disagree on where it is", $"{at}: {creature.Name}");
                    if (!placed.Add(creature)) Note("Rules", "A creature is in two slots", $"{at}: {creature.Name}");
                    if (creature.IsDead) Note("Rules", "A dead creature is still on the board", $"{at}: {creature.Name}");
                    else if (creature.Health <= 0) Note("Rules", "A creature at 0 Health is still alive on the board", $"{at}: {creature.Name} {creature.Attack}/{creature.Health}");
                    if (creature.Attack < 0) Note("Rules", "A creature has negative Attack", $"{at}: {creature.Name} {creature.Attack}/{creature.Health}");
                    if (creature.Owner != player) Note("Rules", "A creature is on a side that isn't its owner's", $"{at}: {creature.Name}");
                }
            }
        }

        private static int CountCards(Player player) =>
            player.Deck.CardsRemaining + player.Hand.Count + player.Battlefield.Count(s => s.IsOccupied()) + player.Deck.GetDiscardPilePreview().Count;

        public string Report() {
            var text = new StringBuilder();
            var list = Problems;
            text.AppendLine(list.Count == 0 ? "PROBLEMS: none found." : $"PROBLEMS: {list.Count} kinds");
            foreach (var problem in list) {
                text.AppendLine($"  [{problem.Kind}] x{problem.Count}: {problem.Message}");
                text.AppendLine($"      first: {problem.FirstSeen.Split('\n')[0]}");
            }
            return text.ToString();
        }
    }
}
