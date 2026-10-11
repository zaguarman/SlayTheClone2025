namespace SlayTheClone {
    /// <summary>The current turn number, as the rules see it (timed modifiers count turns from it).</summary>
    public interface ITurnClock {
        int TurnNumber { get; }
    }

    /// <summary>Owned by the game's rules; only the turn flow (<see cref="TurnManager"/>) moves it forward.</summary>
    public class TurnClock : ITurnClock {
        public int TurnNumber { get; private set; }

        public void Advance() => TurnNumber++;
    }
}
