namespace Zeitgeist {
    /// <summary>An action made by a card's effect, which remembers that card (for the balance ledger and the game records: damage, draws, heals).</summary>
    public interface ISourcedAction {
        ICard Source { get; set; }
    }
}
