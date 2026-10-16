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
        /// <summary>For the Hard AI, the way it plays.</summary>
        public static AIStyle AIStyle { get; set; } = AIStyle.Balanced;
        public static AIEntrant AIPlayer => new AIEntrant(AILevel, AIStyle);
        // A seat's own level, once switched in the game (F6, F7); otherwise the match's
        private static readonly System.Collections.Generic.Dictionary<AISeats, AILevel> seatLevels = new System.Collections.Generic.Dictionary<AISeats, AILevel>();
        /// <summary>How well the AI plays this seat: its own level if one was picked in the game, the match's otherwise.</summary>
        public static AILevel LevelOf(AISeats seat) => seatLevels.TryGetValue(seat, out var level) ? level : AILevel;
        public static AILevel LevelOf(IPlayer player) => LevelOf(player != null && player.IsPlayer1 ? AISeats.Player1 : AISeats.Player2);
        public static void SetLevel(AISeats seat, AILevel level) => seatLevels[seat] = level;
        /// <summary>The AI that plays this seat.</summary>
        public static AIEntrant AIFor(IPlayer player) => new AIEntrant(LevelOf(player), AIStyle);
        /// <summary>The Health you (Player 1) start the fight at, when not the usual 20: an adventure's, carried from fight to fight.</summary>
        public static int? PlayerHealth { get; set; }
        /// <summary>The Health the opponent (Player 2) starts at, when not the usual 20: an adventure's enemies, 5 more each fight.</summary>
        public static int? OpponentHealth { get; set; }

        public static bool PlayedByAI(IPlayer player) =>
            player != null && (AI & (player.IsPlayer1 ? AISeats.Player1 : AISeats.Player2)) != 0;

        public static bool IsChosen => PlayerDeck != null && OpponentDeck != null;

        /// <summary>
        /// The match was set up on purpose (the menu chose one, or a test cleared it). False when Play is pressed straight in
        /// the game scene: then the opponent is a Medium AI (see <see cref="DirectPlayDefaults"/>).
        /// </summary>
        public static bool Prepared { get; private set; }

        /// <summary>Pressing Play straight in the game scene (the user): the opponent is played by a Medium AI.</summary>
        public static void DirectPlayDefaults() {
            if (IsChosen || Prepared) return;
            AI = AISeats.Player2;
            AILevel = AILevel.Medium;
            AIStyle = AIStyle.Balanced;
            Prepared = true;
        }

        public static void Choose(DeckScriptableObject playerDeck, string opponentName, DeckScriptableObject opponentDeck, AISeats ai = AISeats.None) {
            Prepared = true;
            seatLevels.Clear();
            PlayerDeck = playerDeck;
            PlayerHealth = OpponentHealth = null;
            AI = ai;
            OpponentDeck = opponentDeck;
            OpponentName = string.IsNullOrEmpty(opponentName) ? DefaultOpponentName : opponentName;
        }

        // When play mode starts: a match chosen in an earlier session never leaks into this one, and nothing is prepared yet
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnPlayStart() {
            Clear();
            Prepared = false;
        }

        public static void Clear() {
            Prepared = true;
            PlayerDeck = null;
            OpponentDeck = null;
            OpponentName = null;
            PlayerHealth = OpponentHealth = null;
            seatLevels.Clear();
            AI = AISeats.None;
            AILevel = AILevel.Medium;
            AIStyle = AIStyle.Balanced;
        }
    }
}
