using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DiscardHandActionExecutor : ActionExecutor<DiscardHandAction> {
        protected override void ExecuteAction(DiscardHandAction discardAction, ActionExecutionContext context) {
            var player = discardAction.GetPlayer();
            if (player == null) {
                LogError("DiscardHandActionExecutor: Player is null", LogTag.Actions | LogTag.Cards);
                return;
            }

            int initialHandCount = player.Hand.Count;
            // Laid Off: the creatures given up at Dawn do what they do when discarded, after the discard
            var laidOff = player.Hand.Where(c => c is ICreature && LaidOff.Has(c)).ToList();
            player.DiscardHand();
            foreach (var card in laidOff) LaidOff.Queue(card, player, context.ActionsQueue);
            Ripening.Ripen(player, context.GameMediator); // The spells kept ripen
            player.SpellsToday = 0; // A new Day: Combo counts again
            // Paid for a card that never resolved (a Watchdog nullified it): spent all the same
            foreach (var card in player.SetAside.ToList()) {
                player.SetAside.Remove(card);
                player.Deck.AddToDiscardPile(card);
            }

            // Cards on their way back to the hand join the new one; past the hand limit they go to the discard pile
            foreach (var card in player.ComingBack.ToList()) {
                player.ComingBack.Remove(card);
                if (player.Hand.Count < Player.MaxHandSize) player.AddToHand(card);
                else player.Deck.AddToDiscardPile(card);
            }

            Log($"Executed DiscardHandAction via Executor for {player.Name} - discarded {initialHandCount} cards", LogTag.Actions | LogTag.Cards);
        }
    }
}
