#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace SlayTheClone {
    /// <summary>
    /// Debug > AI Tournament: the AI levels play each other with every faction deck; Debug > AI Style Tournament: the Hard
    /// styles do (see <see cref="AITournament"/>). The results, and the problems the games ran into, go to
    /// 'Documentation/AI Tournament.txt' and 'Documentation/AI Styles.txt'. A minute or two each; the editor waits for them.
    /// </summary>
    public static class AITournamentMenu {
        public const string LevelsReportPath = "Assets/Documentation/AI Tournament.txt";
        public const string StylesReportPath = "Assets/Documentation/AI Styles.txt";

        [MenuItem("Debug/AI Tournament")]
        private static void RunLevelsFromMenu() => RunLevels();

        [MenuItem("Debug/AI Style Tournament")]
        private static void RunStylesFromMenu() => RunStyles();

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
            try {
                var (results, report) = run(catalog.Decks,
                    done => EditorUtility.DisplayProgressBar("AI Tournament", "The AIs are playing each other...", done));
                File.WriteAllText(reportPath, report);
                AssetDatabase.ImportAsset(reportPath);
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
