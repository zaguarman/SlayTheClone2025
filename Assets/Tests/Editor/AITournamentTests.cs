using System.Linq;
using NUnit.Framework;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The AI tournament (Debug > AI Tournament), run on demand: it takes a few minutes, so it isn't part of the suite.
    /// Writes the report and checks each level beats the ones below it.
    /// </summary>
    public class AITournamentTests {
        [Test, Explicit("Plays a few hundred games")]
        public void AITournament_EachLevelBeatsTheOnesBelow() {
            var results = AITournamentMenu.Run();
            TestContext.WriteLine(System.IO.File.ReadAllText(AITournamentMenu.ReportPath));
            foreach (var (lower, higher) in new[] { (AILevel.Easy, AILevel.Medium), (AILevel.Medium, AILevel.Hard), (AILevel.Easy, AILevel.Hard) }) {
                var games = results.Where(r => (r.Level1 == lower && r.Level2 == higher) || (r.Level1 == higher && r.Level2 == lower)).ToList();
                Assert.Greater(games.Count(r => r.WinningLevel == higher), games.Count(r => r.WinningLevel == lower), $"{higher} should beat {lower}");
            }
        }
    }
}
