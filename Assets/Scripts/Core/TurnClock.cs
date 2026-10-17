namespace Zeitgeist {
    /// <summary>The current turn number, as the rules see it (timed modifiers count turns from it).</summary>
    public interface ITurnClock {
        int TurnNumber { get; }
        /// <summary>From the start of resolution to the end of Dusk: a hand drawn now would be discarded, so draws wait for the next Day.</summary>
        bool DrawsWaitForNextDay { get; }
    }

    /// <summary>Owned by the game's rules; only the turn flow (<see cref="TurnManager"/>) moves it forward.</summary>
    public class TurnClock : ITurnClock {
        public int TurnNumber { get; private set; }
        public bool DrawsWaitForNextDay { get; set; }

        public void Advance() => TurnNumber++;
    }
}
