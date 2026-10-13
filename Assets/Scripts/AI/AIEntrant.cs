using System.Collections.Generic;
using System.Linq;

namespace SlayTheClone {
    /// <summary>An AI that can take a seat: a level, and for Hard a style.</summary>
    public readonly struct AIEntrant {
        public readonly AILevel Level;
        public readonly AIStyle Style;

        public AIEntrant(AILevel level, AIStyle style = null) {
            Level = level;
            Style = level == AILevel.Hard ? style ?? AIStyle.Balanced : AIStyle.Balanced;
        }

        public string Name => Level == AILevel.Hard && Style != AIStyle.Balanced ? $"Hard {Style}" : Level.ToString();
        public AIPlanner Planner(IGameManager game, int seed) => Planner(game.ActionsQueue, game.CombatHandler, seed);
        public AIPlanner Planner(IActionsQueue queue, IBattlefieldCombatHandler combat, int seed) => new AIPlanner(queue, combat, Level, seed, Style);

        /// <summary>Every AI there is, weakest first: Easy, Medium, then Hard in each style.</summary>
        public static readonly IReadOnlyList<AIEntrant> All =
            new[] { new AIEntrant(AILevel.Easy), new AIEntrant(AILevel.Medium) }.Concat(AIStyle.All.Select(s => new AIEntrant(AILevel.Hard, s))).ToList();
        public override string ToString() => Name;
    }
}
