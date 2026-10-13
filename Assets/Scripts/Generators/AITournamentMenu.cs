#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace SlayTheClone {
    /// <summary>
    /// Debug > AI Tournament: the AI levels play each other with every faction deck (see <see cref="AITournament"/>), and the
    /// results go to 'Documentation/AI Tournament.txt'. Takes a few minutes; the editor waits for it.
    /// </summary>
    public static class AITournamentMenu {
        public const string ReportPath = "Assets/Documentation/AI Tournament.txt";
        private const int GamesPerSeat = 10;

        [MenuItem("Debug/AI Tournament")]
        private static void RunFromMenu() => Run();

        /// <summary>Runs the tournament on the catalog's faction decks and writes the report.</summary>
        public static List<AITournament.Result> Run() {
            var guids = AssetDatabase.FindAssets("t:DeckCatalog");
            if (guids.Length == 0) {
                UnityEngine.Debug.LogError("AI Tournament: no deck catalog found");
                return new List<AITournament.Result>();
            }
            var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            try {
                var results = AITournament.Run(catalog.Decks, GamesPerSeat, out string report,
                    done => EditorUtility.DisplayProgressBar("AI Tournament", "The AI levels are playing each other...", done));
                File.WriteAllText(ReportPath, report);
                AssetDatabase.ImportAsset(ReportPath);
                UnityEngine.Debug.Log($"AI Tournament done, written to {ReportPath}:\n{report}");
                return results;
            }
            finally {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif
