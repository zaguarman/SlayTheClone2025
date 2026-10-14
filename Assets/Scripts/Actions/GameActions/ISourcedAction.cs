namespace SlayTheClone {
    /// <summary>An action made by a card's effect, which remembers that card (for the balance ledger: draws, heals).</summary>
    public interface ISourcedAction {
        ICard Source { get; set; }
    }
}
