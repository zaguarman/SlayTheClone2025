using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class PlayCardActionExecutor : ActionExecutor<PlayCardAction> {
        protected override void ExecuteAction(PlayCardAction playAction, ActionExecutionContext context) {
            var card = playAction.GetCard();
            var owner = playAction.GetOwner();
            var target = playAction.GetTarget();

            if (card == null || owner == null) {
                LogError("PlayCardActionExecutor: Card or Owner is null.", LogTag.Actions);
                return;
            }

            var actionsQueue = context.ActionsQueue;
            if (actionsQueue == null) {
                LogError("PlayCardActionExecutor: ActionsQueue is null in context.", LogTag.Actions);
                return;
            }

            if (!owner.Hand.Contains(card)) {
                LogError($"PlayCardActionExecutor: Cannot play card - {card.Name} is not in {owner.TargetId}'s hand", LogTag.Actions);
                return;
            }

            // The card leaves the hand now and is discarded once it leaves play (see Player.DiscardCard)
            owner.RemoveFromHand(card);
            DiscardCost.Spend(playAction); // What paid for it goes to the discard pile
            if (!Sacrifice.Pay(card, owner, actionsQueue)) { // Nobody left to sacrifice: it fizzles
                owner.DiscardCard(card);
                return;
            }

            if (card is Spell spell) {
                actionsQueue.AddAction(new PlaySpellAction(spell, owner, target));
            }
            else if (card is ICreature creature) {
                // A creature joins the battlefield it was dropped on, under that battlefield's player. A Gift on the other
                // side stays its giver's card (see ICreature.DeckOwner)
                var summoner = (target as BoardSlot)?.Owner ?? owner;
                if (summoner != owner) creature.DeckOwner = owner;
                actionsQueue.AddAction(new SummonCreatureAction(creature, summoner, target));
            }

            Log($"Executed PlayCardAction via Executor for {card.Name}", LogTag.Actions | LogTag.Cards);
        }
    }
}
