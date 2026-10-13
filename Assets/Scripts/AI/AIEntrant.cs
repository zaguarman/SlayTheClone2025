using System.Collections.Generic;
using System.Linq;

namespace SlayTheClone {
    /// <summary>An AI that can take a seat: a level, and for Hard and Very Hard a style.</summary>
    public readonly struct AIEntrant {
        public readonly AILevel Level;
        public readonly AIStyle Style;

        public AIEntrant(AILevel level, AIStyle style = null) {
            Level = level;
            Style = level >= AILevel.Hard ? style ?? AIStyle.Balanced : AIStyle.Balanced;
        }

        public string Name {
            get {
                string level = Level == AILevel.VeryHard ? "Very Hard" : Level.ToString();
                return Level >= AILevel.Hard && Style != AIStyle.Balanced ? $"{level} {Style}" : level;
            }
        }
        public AIPlanner Planner(IGameManager game, int seed) => Planner(game.ActionsQueue, game.CombatHandler, seed);
        public AIPlanner Planner(IActionsQueue queue, IBattlefieldCombatHandler combat, int seed) => new AIPlanner(queue, combat, Level, seed, Style);

        /// <summary>Every AI there is, weakest first: Easy, Medium, Hard in each style, then Very Hard (the long game).</summary>
        public static readonly IReadOnlyList<AIEntrant> All =
            new[] { new AIEntrant(AILevel.Easy), new AIEntrant(AILevel.Medium) }.Concat(AIStyle.All.Select(s => new AIEntrant(AILevel.Hard, s)))
                .Append(new AIEntrant(AILevel.VeryHard)).ToList();
        public override string ToString() => Name;
    }
}
