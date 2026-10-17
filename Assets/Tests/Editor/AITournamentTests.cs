using System.Linq;
using NUnit.Framework;

namespace Zeitgeist.Tests {
    /// <summary>
    /// The AI tournaments (Debug > AI Tournament, AI Style Tournament), run on demand: they take a minute or two each, so
    /// they aren't part of the suite. Each writes its report; the games must break no rule and log no error.
    /// </summary>
    public class AITournamentTests {
        [Test, Explicit("Plays several hundred games"), Timeout(1800000)]
        public void AITournament_EachLevelBeatsTheOnesBelow_AndNoGameBreaksARule() {
            var results = AITournamentMenu.RunLevels();
            TestContext.WriteLine(System.IO.File.ReadAllText(AITournamentMenu.LevelsReportPath));
            foreach (var (lower, higher) in new[] { (AILevel.Easy, AILevel.Medium), (AILevel.Medium, AILevel.Hard), (AILevel.Easy, AILevel.Hard) }) {
                var games = results.Where(r => (r.Level1 == lower && r.Level2 == higher) || (r.Level1 == higher && r.Level2 == lower)).ToList();
                Assert.Greater(games.Count(r => r.WinningLevel == higher), games.Count(r => r.WinningLevel == lower), $"{higher} should beat {lower}");
            }
            AssertNoBrokenRules();
        }

        [Test, Explicit("Plays over a thousand games"), Timeout(1800000)]
        public void AIStyleTournament_EveryStyleBeatsMedium_AndNoGameBreaksARule() {
            var results = AITournamentMenu.RunStyles();
            TestContext.WriteLine(System.IO.File.ReadAllText(AITournamentMenu.StylesReportPath));
            foreach (var style in AITournament.Styles.Where(e => e.Level == AILevel.Hard)) {
                var games = results.Where(r => r.Between(style.Name, "Medium")).ToList();
                Assert.Greater(games.Count(r => r.WinnerName == style.Name), games.Count(r => r.WinnerName == "Medium"), $"{style} should beat Medium");
            }
            AssertNoBrokenRules();
        }

        [Test, Explicit("Plays about three thousand games"), Timeout(1800000)]
        public void AIBalanceReport_IsWritten_AndNoGameBreaksARule() {
            AITournamentMenu.RunBalance();
            TestContext.WriteLine(System.IO.File.ReadAllText(AITournamentMenu.BalanceReportPath));
            AssertNoBrokenRules();
        }

        // Warnings are reported but allowed; broken rules, errors and exceptions aren't
        private static void AssertNoBrokenRules() {
            var broken = AITournament.Problems.Where(p => p.Kind != "Warning").ToList();
            Assert.IsEmpty(broken, string.Join("\n", broken.Select(p => $"[{p.Kind}] x{p.Count}: {p.Message} (first: {p.FirstSeen.Split('\n')[0]})")));
        }
    }
}
