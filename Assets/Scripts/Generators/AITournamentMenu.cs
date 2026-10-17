#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Zeitgeist {
    /// <summary>
    /// Debug > AI Tournament: the AI levels play each other with every faction deck; Debug > AI Style Tournament: the Hard
    /// styles do (see <see cref="AITournament"/>). The results, and the problems the games ran into, go to
    /// 'Documentation/AI Tournament.txt' and 'Documentation/AI Styles.txt'. A minute or two each; the editor waits for them.
    /// Debug > AI Balance Report: which strategies, decks and cards win ('Documentation/Balance Report (AI games).txt').
    /// </summary>
    public static class AITournamentMenu {
        public const string LevelsReportPath = "Assets/Documentation/AI Tournament.txt";
        public const string StylesReportPath = "Assets/Documentation/AI Styles.txt";
        public const string BalanceReportPath = "Assets/Documentation/Balance Report (AI games).txt";
        public const string AdventureReportPath = "Assets/Documentation/Adventure Simulation.txt";
        public const string CardScoresPath = "Assets/Resources/CardScores.json";

        [MenuItem("Debug/AI Adventure Simulation")]
        private static void RunAdventuresFromMenu() => RunAdventures();

        /// <summary>Adventures played by the AI, every faction (see <see cref="AITournament.RunAdventures"/>).</summary>
        public static void RunAdventures(int runsPerFaction = 20) =>
            Run(AdventureReportPath, (decks, progress) => {
                var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:DeckCatalog")[0]));
                var factions = Umbrellas.All.Where(u => catalog.AllCards.Any(c => Umbrellas.Of(c.faction) == u)).ToList(); // The four (2026-10-17)
                return (new List<AITournament.Result>(), AITournament.RunAdventures(factions, catalog.AllCards, runsPerFaction, new AIEntrant(AILevel.Hard), progress, catalog.Potions));
            });

        [MenuItem("Debug/AI Tournament")]
        private static void RunLevelsFromMenu() => RunLevels();

        [MenuItem("Debug/AI Style Tournament")]
        private static void RunStylesFromMenu() => RunStyles();

        [MenuItem("Debug/AI Balance Report")]
        private static void RunBalanceFromMenu() => RunBalance();

        public const string ShuffledReportPath = "Assets/Documentation/Shuffled Tournament.txt";
        public const string ShuffledScoresPath = "Assets/Resources/ShuffledScores.json";

        [MenuItem("Debug/AI Shuffled Tournament")]
        private static void RunShuffledFromMenu() => RunShuffled();

        /// <summary>
        /// Every card in play (the user, 2026-10-17): random decks drawn from the whole pool, factions mixed, played round
        /// robin by the Very Hard AI, to see which cards, roles and faction pairs carry the most potential (see
        /// <see cref="AITournament.RunShuffled"/>). A minute or two.
        /// </summary>
        public static void RunShuffled() =>
            Run(ShuffledReportPath, (decks, progress) => {
                var pool = AssetDatabase.FindAssets("t:CardDataScriptableObject")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<CardDataScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(c => c != null && AssetDatabase.GetAssetPath(c).StartsWith("Assets/Scriptables/Cards/")).ToList();
                return (new List<AITournament.Result>(), AITournament.RunShuffled(pool, progress));
            });

        /// <summary>The balance study (see <see cref="AITournament.RunBalance"/>): two to three thousand games.</summary>
        public static void RunBalance() =>
            Run(BalanceReportPath, (decks, progress) => (new List<AITournament.Result>(), AITournament.RunBalance(decks, progress)));

        /// <summary>The levels, 10 games per seat for each deck and pairing.</summary>
        public static List<AITournament.Result> RunLevels() =>
            Run(LevelsReportPath, (decks, progress) => (AITournament.RunLevels(decks, 10, out string report, progress), report));

        /// <summary>The Hard styles and Medium, 2 games per seat for each deck and pairing.</summary>
        public static List<AITournament.Result> RunStyles() =>
            Run(StylesReportPath, (decks, progress) => (AITournament.RunStyles(decks, 2, out string report, progress), report));

        private static List<AITournament.Result> Run(string reportPath,
            System.Func<IReadOnlyList<DeckScriptableObject>, System.Action<float>, (List<AITournament.Result>, string)> run) {
            var guids = AssetDatabase.FindAssets("t:DeckCatalog");
            if (guids.Length == 0) {
                UnityEngine.Debug.LogError("AI Tournament: no deck catalog found");
                return new List<AITournament.Result>();
            }
            var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            AITournament.BeginRun();
            Telemetry.Load(); // Whether the headless games are recorded too (Debug > Telemetry > Settings)
            try {
                // Cancel stops it early (the user): the games under way finish, and the report is written from those played
                var (results, report) = run(catalog.Decks, done => {
                    string doing = AITournament.StopRequested ? "Stopping: the games under way finish, then the report is written..."
                        : "The AIs are playing each other... (Cancel stops early and writes the report so far)";
                    if (EditorUtility.DisplayCancelableProgressBar("AI Tournament", doing, done)) AITournament.StopRequested = true;
                });
                if (AITournament.Stopped) {
                    report = $"STOPPED EARLY: {AITournament.GamesPlayed} of the {AITournament.GamesPlanned} games planned so far were played; "
                             + "everything below comes from those (later stages may be missing).\n\n" + report;
                }
                File.WriteAllText(reportPath, report);
                AssetDatabase.ImportAsset(reportPath);
                // The balance study's card tiers, for the deck builder's Tier filter (the user, 2026-10-17)
                if (reportPath == BalanceReportPath && AITournament.LastCardScores is { cards: { Count: > 0 } } scores) {
                    File.WriteAllText(CardScoresPath, UnityEngine.JsonUtility.ToJson(scores, true));
                    AssetDatabase.ImportAsset(CardScoresPath);
                    CardScores.Reload();
                }
                // The shuffled run's numbers for every card, for Cards > Propose Decks
                if (reportPath == ShuffledReportPath && AITournament.LastShuffledScores is { cards: { Count: > 0 } } shuffled) {
                    File.WriteAllText(ShuffledScoresPath, UnityEngine.JsonUtility.ToJson(shuffled, true));
                    AssetDatabase.ImportAsset(ShuffledScoresPath);
                }
                UnityEngine.Debug.Log($"AI Tournament done, written to {reportPath}:\n{report}");
                return results;
            }
            finally {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif
