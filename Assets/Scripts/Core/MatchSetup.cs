using UnityEngine;

namespace SlayTheClone {
    /// <summary>Which seats the AI plays (see <see cref="AIController"/>); the others are played on this screen.</summary>
    [System.Flags]
    public enum AISeats { None = 0, Player1 = 1, Player2 = 2, Both = Player1 | Player2 }

    /// <summary>
    /// The match chosen in the main menu, carried over to the game scene: your deck, who you face and their deck, and which
    /// seats the AI plays. When nothing was chosen (the game scene started on its own), the scene's own decks are used and
    /// both seats are played on this screen (hot seat).
    /// </summary>
    public static class MatchSetup {
        public const string DefaultPlayerName = "Player 1";
        public const string DefaultOpponentName = "Player 2";

        public static DeckScriptableObject PlayerDeck { get; private set; }
        public static DeckScriptableObject OpponentDeck { get; private set; }
        public static string OpponentName { get; private set; }
        /// <summary>The seats the AI plays. It can change during the game (see <see cref="AIController"/>).</summary>
        public static AISeats AI { get; set; }
        /// <summary>How well the AI plays its seats.</summary>
        public static AILevel AILevel { get; set; } = AILevel.Medium;

        public static bool PlayedByAI(IPlayer player) =>
            player != null && (AI & (player.IsPlayer1 ? AISeats.Player1 : AISeats.Player2)) != 0;

        public static bool IsChosen => PlayerDeck != null && OpponentDeck != null;

        public static void Choose(DeckScriptableObject playerDeck, string opponentName, DeckScriptableObject opponentDeck, AISeats ai = AISeats.None) {
            PlayerDeck = playerDeck;
            AI = ai;
            OpponentDeck = opponentDeck;
            OpponentName = string.IsNullOrEmpty(opponentName) ? DefaultOpponentName : opponentName;
        }

        // Also runs when play mode starts, so a match chosen in an earlier session never leaks into this one
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() {
            PlayerDeck = null;
            OpponentDeck = null;
            OpponentName = null;
            AI = AISeats.None;
            AILevel = AILevel.Medium;
        }
    }
}
