using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// The match chosen in the main menu, carried over to the game scene: your deck, who you face and their deck.
    /// When nothing was chosen (the game scene started on its own), the scene's own decks are used.
    /// </summary>
    public static class MatchSetup {
        public const string DefaultPlayerName = "Player 1";
        public const string DefaultOpponentName = "Player 2";

        public static DeckScriptableObject PlayerDeck { get; private set; }
        public static DeckScriptableObject OpponentDeck { get; private set; }
        public static string OpponentName { get; private set; }

        public static bool IsChosen => PlayerDeck != null && OpponentDeck != null;

        public static void Choose(DeckScriptableObject playerDeck, string opponentName, DeckScriptableObject opponentDeck) {
            PlayerDeck = playerDeck;
            OpponentDeck = opponentDeck;
            OpponentName = string.IsNullOrEmpty(opponentName) ? DefaultOpponentName : opponentName;
        }

        // Also runs when play mode starts, so a match chosen in an earlier session never leaks into this one
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() {
            PlayerDeck = null;
            OpponentDeck = null;
            OpponentName = null;
        }
    }
}
