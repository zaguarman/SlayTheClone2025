using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// AI against AI, with no screen: whole combats played by the real rules (a GameSession and its TurnFlow, as in a game)
    /// as fast as they run, many at once, one a CPU thread (see <see cref="Workers"/>). <see cref="RunLevels"/> pits the levels against each other, <see cref="RunStyles"/> the Hard styles
    /// (and Medium, as a yardstick), each deck on both sides (the same deck, so only the play differs) and in both seats.
    /// Every game is audited (see <see cref="GameAudit"/>): what breaks the rules is reported with where it happened. Each
    /// game has its own seed, so a run replays exactly.
    /// </summary>
    public static partial class AITournament {
        /// <summary>A combat still going after this many Days is called off: nobody wins it.</summary>
        public const int MaxDays = 40;

        public readonly struct Result {
            public readonly string Deck, Deck2; // Player 1's and player 2's
            public readonly AIEntrant First, Second;
            /// <summary>1 or 2; 0 when nobody won (both at 0 on the same action, or called off).</summary>
            public readonly int Winner;
            public readonly int Days;
            /// <summary>Each player's Health at the end.</summary>
            public readonly int Health1, Health2;
            /// <summary>The potions still in player 1's hand at the end (an adventure keeps those), by name.</summary>
            public readonly string[] PotionsLeft1;

            public Result(string deck, string deck2, AIEntrant first, AIEntrant second, int winner, int days, int health1 = 0, int health2 = 0, string[] potionsLeft1 = null) {
                (Deck, Deck2, First, Second, Winner, Days, Health1, Health2) = (deck, deck2, first, second, winner, days, health1, health2);
                PotionsLeft1 = potionsLeft1 ?? new string[0];
            }

            /// <summary>It was played: false for a game skipped because the run was stopped early.</summary>
            public bool Played => Deck != null;
            public AILevel Level1 => First.Level;
            public AILevel Level2 => Second.Level;
            public string WinnerName => Winner == 1 ? First.Name : Winner == 2 ? Second.Name : null;
            public AILevel? WinningLevel => Winner == 1 ? First.Level : Winner == 2 ? Second.Level : (AILevel?)null;
            public bool Between(string a, string b) => (First.Name == a && Second.Name == b) || (First.Name == b && Second.Name == a);
        }

        /// <summary>A deck's cards, read once on the main thread (Unity assets can't be read from others) and shared by its games.</summary>
        internal sealed class DeckCards {
            public string Name;
            public List<CardData> Cards;
        }

        /// <summary>One game to play: the decks and AIs in each seat, and its seed.</summary>
        internal readonly struct Game {
            public readonly DeckCards Deck1, Deck2;
            public readonly AIEntrant First, Second;
            public readonly int Seed;
            public readonly int Health1, Health2; // What each player starts at (an adventure's fights differ from 20)
            public readonly Relic[] Relics1; // An adventure's kit for player 1: its relics and potions
            public readonly List<CardData> Potions1;

            public Game(DeckCards deck1, AIEntrant first, DeckCards deck2, AIEntrant second, int seed,
                int health1 = Player.StartingHealth, int health2 = Player.StartingHealth, Relic[] relics1 = null, List<CardData> potions1 = null) {
                (Deck1, First, Deck2, Second, Seed, Health1, Health2) = (deck1, first, deck2, second, seed, health1, health2);
                Relics1 = relics1;
                Potions1 = potions1;
            }
        }

        /// <summary>
        /// How many games are played at once, one a thread. Measured on a 32-thread CPU: 1 thread 20 games a second, 3 27, 5 33,
        /// 8 32, 16 13, 31 10. The editor's .NET runtime takes one global lock to allocate memory and the games allocate a lot,
        /// so past about 5 the threads mostly wait for each other. Using every core would take separate processes.
        /// </summary>
        public static int Workers { get; set; } = System.Math.Max(1, System.Math.Min(5, System.Environment.ProcessorCount - 1));

        /// <summary>
        /// Stop early (the user, 2026-10-16: the progress bar's Cancel): no new game starts, the ones under way finish, and the
        /// report is written from the games played so far.
        /// </summary>
        public static volatile bool StopRequested;
        /// <summary>The run since <see cref="BeginRun"/> was stopped early: the games it planned and the ones it played.</summary>
        public static bool Stopped { get; private set; }
        public static int GamesPlanned { get; private set; }
        public static int GamesPlayed { get; private set; }

        /// <summary>A new run: not stopped, nothing played yet.</summary>
        public static void BeginRun() {
            StopRequested = Stopped = false;
            GamesPlanned = GamesPlayed = 0;
        }

        internal static List<DeckCards> Read(IReadOnlyList<DeckScriptableObject> decks) =>
            decks.Select(d => new DeckCards { Name = d.GetDeckName(), Cards = d.GetCardDataList() }).ToList();

        // The card definitions read for a run are made for it: let them go when it's over
        internal static void Release(IEnumerable<DeckCards> decks) {
            foreach (var data in decks.SelectMany(d => d.Cards).Distinct().SelectMany(WithNested).Distinct().ToList()) Object.DestroyImmediate(data);
        }

        // A card and the definitions made along with it: the tokens it summons, and their fake faces
        private static IEnumerable<CardData> WithNested(CardData data) {
            if (data == null) yield break;
            yield return data;
            if (data is CreatureData { disguise: not null } creature) yield return creature.disguise;
            foreach (var decoy in data.effects.SelectMany(e => e.actions).Select(a => a.decoyData).Where(d => d != null)) {
                yield return decoy;
                if (decoy.disguise != null) yield return decoy.disguise;
            }
        }

        /// <summary>
        /// Plays one combat to its end (or <see cref="MaxDays"/>): each AI plans its seat each Day. It touches nothing of
        /// Unity's and nothing shared with other games, so many can run at once on other threads.
        /// </summary>
        internal static Result Play(Game game, GameAudit audit = null, CardLedger ledger = null) {
            string where = $"{game.Deck1.Name}{(game.Deck2 != game.Deck1 ? " vs " + game.Deck2.Name : "")}, {game.First} vs {game.Second}, seed {game.Seed}";
            var mediator = new GameMediator();
            bool over = false;
            IPlayer champion = null;
            mediator.GameOver += winner => { over = true; champion = winner; };
            GameSession session = null;
            TurnFlow turns = null;
            GameAudit.Watch watch = null;
            CardLedger.Watch tally = null;
            try {
                // Each game deals from its own copy of the deck lists: decks shuffle what they're given
                session = new GameSession(mediator, new List<CardData>(game.Deck1.Cards), new List<CardData>(game.Deck2.Cards),
                    new SimpleModifierFactory(), game.Seed, health1: game.Health1, health2: game.Health2);
                turns = session.CreateTurnFlow(mediator);
                session.Equip(session.Player1, game.Relics1, game.Potions1);
                session.DealOpeningHands();
                var planners = new[] {
                    game.First.Planner(session.ActionsQueue, session.CombatHandler, game.Seed),
                    game.Second.Planner(session.ActionsQueue, session.CombatHandler, game.Seed + 1)
                };
                watch = audit?.Begin(session, where);
                tally = ledger?.Begin(session, mediator, game.First, game.Second, mirror: game.Deck1 == game.Deck2, game.Deck1.Name, game.Deck2.Name);

                while (!over && turns.TurnNumber < MaxDays) {
                    planners[0].PlanDay(session.Player1);
                    planners[1].PlanDay(session.Player2);
                    watch?.CheckPlans(turns.TurnNumber);
                    int day = turns.TurnNumber;
                    turns.EndTurnImmediately();
                    watch?.CheckDay(day, advanced: turns.TurnNumber == day + 1, over);
                }

                bool firstDown = session.Player1.Health <= 0, secondDown = session.Player2.Health <= 0;
                int winner = champion == session.Player1 ? 1 : champion == session.Player2 ? 2 : firstDown == secondDown ? 0 : firstDown ? 2 : 1;
                tally?.End(winner);
                var potionsLeft = session.Player1.Hand.Where(c => c.SourceData?.potion == true).Select(c => c.Name).ToArray();
                return new Result(game.Deck1.Name, game.Deck2.Name, game.First, game.Second, winner, turns.TurnNumber, session.Player1.Health, session.Player2.Health, potionsLeft);
            }
            catch (System.Exception exception) {
                audit?.Note("Exception", $"{exception.GetType().Name}: {exception.Message}", $"{where}, Day {turns?.TurnNumber ?? 0}\n{exception.StackTrace}");
                return new Result(game.Deck1.Name, game.Deck2.Name, game.First, game.Second, 0, turns?.TurnNumber ?? 0);
            }
            finally {
                watch?.End();
                tally?.Detach();
                session?.Cleanup();
            }
        }

        /// <summary>
        /// Plays the games side by side, <see cref="Workers"/> at a time, each on a thread of its own; returns the results
        /// in the order of the games (each game's seed makes it play out the same whatever thread it's on). The calling
        /// (main) thread reports the progress and listens for what the games log.
        /// </summary>
        /// <param name="aligned">Keep a result for each game, in their order (a game skipped by a stop is not <see cref="Result.Played"/>);
        /// otherwise only the games played are returned.</param>
        internal static Result[] PlayAll(IReadOnlyList<Game> games, GameAudit audit, CardLedger ledger, System.Action<float> progress, bool aligned = false) {
            var results = new Result[games.Count];
            int done = 0;
            bool muted = DebugLogger.MuteInfo;
            DebugLogger.MuteInfo = true; // Thousands of lines a Day otherwise
            audit?.Listen();
            try {
                var options = new ParallelOptions { MaxDegreeOfParallelism = Workers };
                var work = Task.Run(() => Parallel.For(0, games.Count, options, (i, loop) => {
                    if (StopRequested) { // Stopped: no new game starts
                        loop.Stop();
                        return;
                    }
                    results[i] = Play(games[i], audit, ledger);
                    Interlocked.Increment(ref done);
                }));
                while (!work.Wait(100)) progress?.Invoke(Volatile.Read(ref done) / (float)games.Count);
                work.GetAwaiter().GetResult(); // What went wrong on a thread, here
            }
            finally {
                audit?.StopListening();
                DebugLogger.MuteInfo = muted;
            }
            GamesPlanned += games.Count;
            GamesPlayed += done;
            if (done < games.Count) Stopped = true;
            return aligned ? results : results.Where(r => r.Played).ToArray();
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

        private static List<(AIEntrant, AIEntrant)> Pairings(AIEntrant[] entrants) =>
            (from i in Enumerable.Range(0, entrants.Length) from j in Enumerable.Range(i + 1, entrants.Length - i - 1)
             select (entrants[i], entrants[j])).ToList();

        // Every pairing with each deck against itself, both seats, gamesPerSeat times each
        internal static List<Game> PairingGames(AIEntrant[] entrants, IReadOnlyList<DeckCards> decks, int gamesPerSeat, ref int seed) {
            var games = new List<Game>();
            foreach (var deck in decks) {
                foreach (var (a, b) in Pairings(entrants)) {
                    for (int game = 0; game < gamesPerSeat; game++) {
                        games.Add(new Game(deck, a, deck, b, seed++));
                        games.Add(new Game(deck, b, deck, a, seed++));
                    }
                }
            }
            return games;
        }

        private static List<Result> Run(string title, AIEntrant[] entrants, IReadOnlyList<DeckScriptableObject> decks, int gamesPerSeat,
            out string report, System.Action<float> progress) {
            var audit = new GameAudit();
            var cards = Read(decks);
            List<Result> results;
            try {
                int seed = 1000;
                results = PlayAll(PairingGames(entrants, cards, gamesPerSeat, ref seed), audit, null, progress).ToList();
            }
            finally {
                Release(cards);
            }
            report = Report(title, results, entrants, Pairings(entrants), decks) + "\n" + audit.Report();
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
            text.AppendLine($"Seats: Player 1 won {firstSeat}, Player 2 won {secondSeat}, {results.Count - firstSeat - secondSeat} draws; {(results.Count == 0 ? 0 : results.Average(r => r.Days)):0.0} Days on average");
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

        private readonly object gate = new object();
        private readonly Dictionary<string, Problem> problems = new Dictionary<string, Problem>();

        public IReadOnlyList<Problem> Problems {
            get {
                lock (gate) return problems.Values.OrderBy(p => p.Kind == "Warning").ThenByDescending(p => p.Count).ToList();
            }
        }

        // What a game logs goes to the game playing on the thread that logged it
        [System.ThreadStatic] private static Watch current;

        /// <summary>Starts hearing what games log (on the main thread, before they start).</summary>
        internal void Listen() => Application.logMessageReceivedThreaded += OnAnyLog;
        internal void StopListening() => Application.logMessageReceivedThreaded -= OnAnyLog;

        private static void OnAnyLog(string message, string stackTrace, LogType type) {
            if (type == LogType.Log) return;
            // An exception keeps where it was thrown, to find it later (it may not happen again on the main thread)
            current?.Noticed(type == LogType.Warning ? "Warning" : type.ToString(), message,
                type == LogType.Exception ? string.Join("\n", (stackTrace ?? "").Split('\n').Take(8)) : null);
        }

        public void Note(string kind, string message, string at) {
            string key = kind + ": " + Normalize(message);
            lock (gate) {
                if (!problems.TryGetValue(key, out var problem)) {
                    problems[key] = problem = new Problem { Kind = kind, Message = Normalize(message), FirstSeen = at };
                }
                problem.Count++;
            }
        }

        // The same problem with other creatures' ids, colors and numbers is the same problem
        private static string Normalize(string message) {
            message = Regex.Replace(message, "<[^>]+>", "");
            message = Regex.Replace(message, "[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}", "<id>");
            message = Regex.Replace(message, @"\d+", "#");
            return message.Split('\n')[0].Trim();
        }

        /// <summary>Starts watching one game, on the thread that plays it.</summary>
        internal Watch Begin(GameSession session, string match) => new Watch(this, session, match);

        /// <summary>One game under watch: its checks, and what it logs.</summary>
        internal sealed class Watch {
            private readonly GameAudit audit;
            private readonly GameSession game;
            private readonly string where;
            private readonly Dictionary<Player, int> cardCounts;
            private int day;

            public Watch(GameAudit audit, GameSession session, string match) {
                this.audit = audit;
                game = session;
                where = match;
                cardCounts = Players.ToDictionary(p => p, CountCards);
                current = this;
            }

            public void End() {
                if (current == this) current = null;
            }

            private IEnumerable<Player> Players => new[] { game.Player1, game.Player2 };

            public void Noticed(string kind, string message, string stack = null) =>
                audit.Note(kind, message, $"{where}, Day {day}" + (string.IsNullOrEmpty(stack) ? "" : "\n" + stack));

            private void Note(string kind, string message, string at) => audit.Note(kind, message, at);

            public void CheckPlans(int today) {
                day = today;
                foreach (var play in game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>()) {
                    if (!play.GetOwner().Hand.Contains(play.GetCard())) Note("Rules", "A card was planned that isn't in its player's hand", $"{where}, Day {day}: {play.GetCard().Name}");
                }
            }

            public void CheckDay(int endedDay, bool advanced, bool over) {
                string at = $"{where}, Day {endedDay}";
                if (!advanced && !over) Note("Rules", "Ending the turn didn't start the next Day", at);
                if (game.ActionsQueue.GetPendingActionsCount() > 0 && !over) { // Won mid-Day, what's still planned is dropped: fine
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
                    if (player.Health < 0) Note("Rules", "A player's Health is out of bounds", $"{at}: {player.Name} at {player.Health}");
                    if (player.Hand.Count > Player.MaxHandSize) Note("Rules", "A hand is over the maximum", $"{at}: {player.Name} holds {player.Hand.Count}");
                    foreach (var slot in player.Battlefield) {
                        var creature = slot.OccupyingCreature;
                        if (creature == null) continue;
                        if (creature.Slot != slot) Note("Rules", "A creature and its slot disagree on where it is", $"{at}: {creature.Name}");
                        if (!placed.Add(creature)) Note("Rules", "A creature is in two slots", $"{at}: {creature.Name}");
                        if (creature.IsDead) Note("Rules", "A dead creature is still on the board", $"{at}: {creature.Name}");
                        else if (creature.Health <= 0 && !game.ActionsQueue.Modifiers.HasStatusEffect(creature, Enums.StatusEffectType.BorrowedTime)) Note("Rules", "A creature at 0 Health is still alive on the board", $"{at}: {creature.Name} {creature.Attack}/{creature.Health}");
                        if (creature.Attack < 0) Note("Rules", "A creature has negative Attack", $"{at}: {creature.Name} {creature.Attack}/{creature.Health}");
                        if (creature.Owner != player) Note("Rules", "A creature is on a side that isn't its owner's", $"{at}: {creature.Name}");
                    }
                }
            }
        }

        // Whose cards they are: a Gift on the other side is still its giver's
        private static int CountCards(Player player) => CardCount.Of(player);

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
