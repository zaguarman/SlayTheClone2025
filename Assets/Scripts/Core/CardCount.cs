using System.Linq;

namespace SlayTheClone {
    /// <summary>
    /// All of a player's cards, wherever they are: deck, hand, discard pile, on the way back to the hand, on the board (a Gift
    /// or a stolen creature on the other side is still its owner's) and off the board for now (gone dark). The total never
    /// changes during a combat: the AI games audit it, and so do the tests.
    /// </summary>
    public static class CardCount {
        public static int Of(IPlayer player) {
            bool Mine(ICreature c) => c != null && (c.DeckOwner ?? c.Owner) == player;
            var sides = new[] { player, player.Opponent }.Where(p => p != null).ToList();
            return player.Deck.CardsRemaining + player.Hand.Count + player.Deck.GetDiscardPilePreview().Count + player.ComingBack.Count
                + sides.SelectMany(p => p.Battlefield).Count(s => Mine(s.OccupyingCreature))
                + sides.SelectMany(p => p.GoneDark).Count(g => Mine(g.Creature));
        }
    }
}
