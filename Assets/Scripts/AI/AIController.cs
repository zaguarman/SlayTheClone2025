using System.Collections.Generic;
using UnityEngine;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>
    /// Plugs the <see cref="AIPlanner"/> into a running game. At the start of each Day it plans the Day of every seat the AI
    /// plays (<see cref="MatchSetup.AI"/>), as chosen (<see cref="MatchSetup.AIPlayer"/>: a level, and for Hard a style), so its plans are on the table before you make yours (GDD 3.6: the AI plans first).
    /// When it plays both seats it also ends the turn, after a pause so its plans can be watched. Which seats it plays can
    /// change at any time (<see cref="CycleSeat"/>: F6 for Player 1, F7 for Player 2, each press Off, Easy, Medium, Hard,
    /// Very Hard, Off again); it stops when the combat ends.
    /// </summary>
    public class AIController : MonoBehaviour {
        public const UnityEngine.InputSystem.Key Player1Key = UnityEngine.InputSystem.Key.F6, Player2Key = UnityEngine.InputSystem.Key.F7;

        /// <summary>Real seconds between the AI planning a Day and ending it, when it plays both seats.</summary>
        public static float TurnPause = 1f;

        private IGameManager game;
        private readonly Dictionary<AILevel, AIPlanner> planners = new Dictionary<AILevel, AIPlanner>(); // One a level, made when first needed
        private readonly Dictionary<IPlayer, int> plannedDay = new Dictionary<IPlayer, int>(); // The last Day each seat was planned
        private float plannedAt;
        private bool gameOver;
        private IGameMediator mediator;

        public static AIController Create(Transform parent) {
            var controller = new GameObject("AIController").AddComponent<AIController>();
            controller.transform.SetParent(parent, false);
            return controller;
        }

        public void Initialize(IGameManager gameManager, IGameMediator gameMediator) {
            game = gameManager ?? throw new System.ArgumentNullException(nameof(gameManager));
            mediator = gameMediator ?? throw new System.ArgumentNullException(nameof(gameMediator));
            MatchSetup.DirectPlayDefaults(); // Play pressed straight in the game scene: a Medium AI opponent
            mediator.GameOver += OnGameOver;
            ShowHumanSeat();
            Log($"AIController initialized: the AI ({MatchSetup.AIPlayer}) plays {MatchSetup.AI}", LogTag.Initialization | LogTag.Turns);
        }

        private void OnDestroy() {
            if (mediator != null) mediator.GameOver -= OnGameOver;
        }

        private void OnGameOver(IPlayer winner) => gameOver = true;

        private void Update() {
            if (game == null || gameOver || Time.timeScale == 0f) return; // Not while paused
            var turns = game.TurnManager;
            if (turns == null || turns.IsResolving) return;

            foreach (var player in new[] { game.Player1, game.Player2 }) {
                if (!MatchSetup.PlayedByAI(player)) continue;
                if (plannedDay.TryGetValue(player, out int day) && day == turns.TurnNumber) continue;
                PlannerFor(player).PlanDay(player);
                plannedDay[player] = turns.TurnNumber;
                plannedAt = Time.unscaledTime;
            }

            if (MatchSetup.AI == AISeats.Both && Time.unscaledTime - plannedAt >= TurnPause) turns.EndTurn();
        }

        private AIPlanner PlannerFor(IPlayer player) {
            var level = MatchSetup.LevelOf(player);
            if (!planners.TryGetValue(level, out var planner)) {
                planner = MatchSetup.AIFor(player).Planner(game, System.Environment.TickCount + (int)level);
                planners[level] = planner;
            }
            return planner;
        }

        /// <summary>
        /// The next AI for this seat (the user: a key each, F6 and F7): Off, Easy, Medium, Hard, Very Hard, then Off again. The
        /// screen stays on the side it shows: the switch moves no one round the table (F8 does).
        /// </summary>
        public static void CycleSeat(AISeats seat) {
            bool on = (MatchSetup.AI & seat) != 0;
            var level = MatchSetup.LevelOf(seat);
            if (!on) {
                MatchSetup.AI |= seat;
                MatchSetup.SetLevel(seat, AILevel.Easy);
            }
            else if (level < AILevel.VeryHard) MatchSetup.SetLevel(seat, level + 1);
            else MatchSetup.AI &= ~seat;
            Log($"The AI now plays {MatchSetup.AI} ({seat}: {SeatText(seat)})", LogTag.Turns);
        }

        /// <summary>The AI on this seat and how well it plays, for the note under End Turn.</summary>
        public static string SeatText(AISeats seat) => (MatchSetup.AI & seat) != 0 ? new AIEntrant(MatchSetup.LevelOf(seat), MatchSetup.AIStyle).Name : "Off";

        // At the start, with one seat left to you, the screen shows yours
        private static void ShowHumanSeat() {
            if (MatchSetup.AI == AISeats.Player1) FogOfWar.PlaysAsPlayer1 = false;
            else if (MatchSetup.AI == AISeats.Player2) FogOfWar.PlaysAsPlayer1 = true;
        }

        /// <summary>For the note under End Turn: which seats the AI plays, and how well.</summary>
        public static string SeatsText => MatchSetup.AI switch {
            AISeats.None => "Off",
            AISeats.Both => $"Both players, {MatchSetup.AIPlayer.Name}",
            AISeats.Player1 => $"Player 1, {MatchSetup.AIPlayer.Name}",
            _ => $"Player 2, {MatchSetup.AIPlayer.Name}"
        };
    }
}
