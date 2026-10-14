using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlayTheClone {
    public static partial class AITournament {
        /// <summary>
        /// One deck against a faction's decks, for tuning a faction: each pairing played in both seats at each level, the
        /// same AI on both sides. Returns the deck's win share against each deck and in all, and what its cards did
        /// (damage and kills a game they're played in).
        /// </summary>
        public static string RunMatchup(DeckScriptableObject deck, IReadOnlyList<DeckScriptableObject> opponents, IReadOnlyList<AILevel> levels,
            int gamesPerSeat, int seed = 1) {
            var decks = Read(new[] { deck }.Concat(opponents).ToList());
            var mine = decks[0];
            var names = new HashSet<string>(mine.Cards.Select(c => c.cardName)); // Read before the definitions are let go
            var games = new List<Game>();
            foreach (var level in levels) {
                var ai = new AIEntrant(level);
                foreach (var other in decks.Skip(1)) {
                    for (int i = 0; i < gamesPerSeat; i++) {
                        games.Add(new Game(mine, ai, other, ai, seed + games.Count * 7919));
                        games.Add(new Game(other, ai, mine, ai, seed + games.Count * 7919));
                    }
                }
            }
            var ledger = new CardLedger();
            var audit = new GameAudit();
            List<Result> results;
            try {
                results = PlayAll(games, audit, ledger, null).ToList();
            }
            finally {
                Release(decks);
            }

            bool Won(Result r) => (r.Winner == 1 && r.Deck == mine.Name) || (r.Winner == 2 && r.Deck2 == mine.Name);
            string Share(IEnumerable<Result> rs) {
                var list = rs.ToList();
                return list.Count == 0 ? "-" : $"{list.Count(Won) / (float)list.Count:0%} ({list.Count(Won)}/{list.Count})";
            }
            var text = new StringBuilder();
            text.AppendLine($"{mine.Name}: {Share(results)} of {results.Count} games, {results.Average(r => r.Days):0.0} Days on average");
            foreach (var level in levels) {
                var atLevel = results.Where(r => r.Level1 == level).ToList();
                text.AppendLine($"  {new AIEntrant(level).Name}: {Share(atLevel)}");
                foreach (var other in decks.Skip(1)) {
                    text.AppendLine($"    vs {other.Name}: {Share(atLevel.Where(r => r.Deck == other.Name || r.Deck2 == other.Name))}");
                }
            }
            text.AppendLine("  Its cards (in the games they're played: plays, damage, kills, win share of those games)");
            foreach (var card in ledger.Cards.Where(c => names.Contains(c.Name) && c.Games > 0).OrderByDescending(c => c.WinRate)) {
                text.AppendLine($"    {card.Name,-40} {card.PlaysPerGame,4:0.0} plays {(card.FaceDamage + card.CreatureDamage) / (float)card.Games,5:0.0} dmg " +
                                $"{card.Kills / (float)card.Games,4:0.0} kills  {card.WinRate:0%} of {card.Games}");
            }
            if (audit.Problems.Count > 0) text.AppendLine($"  PROBLEMS: {audit.Problems.Count}, first: {audit.Problems[0].Kind}: {audit.Problems[0].Message} at {audit.Problems[0].FirstSeen}");
            return text.ToString();
        }
    }
}
