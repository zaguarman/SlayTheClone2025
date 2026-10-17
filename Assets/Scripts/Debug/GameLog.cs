using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>
    /// The game log a player can hand over (the user, 2026-10-17: "a log export button to let me copy paste it here to show you
    /// my game and explain bugs"): the match (seats, decks, who plays them, the seed), the Day, both sides as they stand (Health,
    /// hands, piles, every creature on the board with its numbers and statuses), what's queued, then everything the game logged
    /// since it began. The pause menu's Copy game log puts it on the clipboard (and in a file beside the save data, outside the
    /// browser).
    /// </summary>
    public static class GameLog {
        private const int Most = 6000; // Lines kept; the oldest go first
        private static readonly List<string> lines = new List<string>();

        /// <summary>A new game: the lines start over.</summary>
        public static void Clear() => lines.Clear();

        /// <summary>Every line the game logs, as it's logged (see <see cref="DebugLogger"/>).</summary>
        public static void Note(LogType type, object message, LogTag tags) {
            if (lines.Count >= Most) lines.RemoveRange(0, Most / 4);
            string kind = type == LogType.Error ? "ERROR " : type == LogType.Warning ? "WARNING " : "";
            lines.Add($"[{tags}] {kind}{message}");
        }

        /// <summary>The whole log as text, the game's state first.</summary>
        public static string Text() {
            var text = new StringBuilder();
            text.AppendLine($"Zeitgeist game log, {DateTime.Now:yyyy-MM-dd HH:mm}, version {Telemetry.Version}, {Application.platform}");
            var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (game == null || game.Player1 == null) {
                text.AppendLine("No game under way.");
            }
            else {
                try { DescribeGame(game, text); }
                catch (Exception e) { text.AppendLine($"(The state couldn't be read: {e.Message})"); }
            }
            text.AppendLine();
            text.AppendLine($"LOG ({lines.Count} lines)");
            foreach (var line in lines) text.AppendLine(line);
            return text.ToString();
        }

        private static void DescribeGame(GameManager game, StringBuilder text) {
            string mode = AdventureRun.Fighting != null ? $"adventure ({Umbrellas.Name(AdventureRun.Umbrella)}, room {AdventureRun.Fighting.Type} on row {AdventureRun.Fighting.Row + 1}, Ascension {AdventureRun.Ascension})" : "match";
            text.AppendLine($"Mode: {mode}; seed {game.RandomSeed}; Day {game.TurnManager?.TurnNumber}; fog of war {(FogOfWar.Enabled ? "on" : "off")}, playing as Player {(FogOfWar.PlaysAsPlayer1 ? 1 : 2)}");
            Side(game, game.Player1, MatchSetup.PlayerDeck, text);
            Side(game, game.Player2, MatchSetup.OpponentDeck, text);
            var queued = game.ActionsQueue?.GetPendingActions()?.ToList() ?? new List<IGameAction>();
            text.AppendLine($"Queued: {(queued.Count == 0 ? "nothing" : string.Join("; ", queued.Select(a => a.ToString())))}");
        }

        private static void Side(GameManager game, IPlayer player, DeckScriptableObject deck, StringBuilder text) {
            string who = MatchSetup.PlayedByAI(player) ? $"AI {MatchSetup.AIFor(player).Name}" : "human";
            string relics = player.Relics.Count > 0 ? $"; relics {string.Join(", ", player.Relics.Select(Relics.NameOf))}" : "";
            text.AppendLine($"{player.Name} ({who}), deck {deck?.GetDeckName() ?? "(scene)"}: Health {player.Health}, hand {player.Hand.Count}, deck {player.Deck?.CardsRemaining}, discard {player.Deck?.GetDiscardPilePreview().Count}{relics}");
            text.AppendLine($"  Hand: {string.Join(", ", player.Hand.Select(c => c.Name))}");
            foreach (var slot in player.Battlefield) {
                var creature = slot.OccupyingCreature;
                if (creature == null) continue;
                var statuses = game.ActionsQueue?.Modifiers?.GetActiveModifiersFor(creature).Select(m => m.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList() ?? new List<string>();
                string deploying = creature.IsOperative ? "" : $", deploying {creature.TurnsUntilOperative}";
                string armor = creature.CurrentArmorPool > 0 ? $", armor {creature.CurrentArmorPool}" : "";
                text.AppendLine($"  Slot {slot.Index}: {creature.Name} {creature.Attack}/{creature.Health} (of {creature.MaxHealth}), speed {creature.Speed}{armor}{deploying}{(statuses.Count > 0 ? ", " + string.Join(", ", statuses) : "")}");
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern int GameLog_Copy(string text);
#endif

        /// <summary>
        /// Puts the log on the clipboard and, where there's a file system, saves it too; returns the file's path or null. In the
        /// browser the page does the copying and hands the log over as a download as well (Assets/Plugins/WebGL/GameLog.jslib).
        /// </summary>
        public static string Copy() {
            string text = Text();
#if UNITY_WEBGL && !UNITY_EDITOR
            try { GameLog_Copy(text); }
            catch (Exception e) { Debug.LogWarning($"The log couldn't be copied: {e.Message}"); }
            return "the browser's downloads (zeitgeist-game-log.txt)";
#else
            try { GUIUtility.systemCopyBuffer = text; }
            catch (Exception e) { Debug.LogWarning($"The log couldn't be copied: {e.Message}"); }
#endif
            if (Application.platform == RuntimePlatform.WebGLPlayer) return null;
            try {
                string folder = Path.Combine(Application.persistentDataPath, "GameLogs");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"{DateTime.Now:yyyy-MM-dd HH-mm-ss}.txt");
                File.WriteAllText(path, text, new UTF8Encoding(false));
                return path;
            }
            catch (Exception e) {
                Debug.LogWarning($"The log couldn't be saved: {e.Message}");
                return null;
            }
        }
    }
}
