using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// A creature changes sides (GDD 5.4 Changing Sides): Coup d'État, Operation Paperclip, Brainwashed running out. Its new
    /// controller takes it; its owner never changes, so its card still goes back to them when it leaves play.
    /// </summary>
    public class ChangeSidesAction : IGameAction {
        public ICreature Creature { get; }
        public IPlayer NewController { get; }

        public ChangeSidesAction(ICreature creature, IPlayer newController) {
            Creature = creature;
            NewController = newController;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"ChangeSidesAction: {Creature?.Name} goes over to {NewController?.Name}";
    }

    // Placement: the slot directly opposite the one it stood in; whatever stands there is discarded, as when a summon lands on
    // it. Not a move: nothing stops it, but Slot Effects see it leave and enter. It keeps its Health, Armor, statuses and stat
    // changes; what its old side planned for it is cancelled. Both sides know what it is
    public class ChangeSidesActionExecutor : ActionExecutor<ChangeSidesAction> {
        protected override void ExecuteAction(ChangeSidesAction change, ActionExecutionContext context) {
            var creature = change.Creature;
            var newSide = change.NewController;
            var from = creature?.Slot;
            if (creature == null || newSide == null || creature.IsDead || from == null || creature.Owner == newSide) {
                Log($"Nothing changes sides: {change}", LogTag.Effects);
                return;
            }

            var oldSide = creature.Owner;
            var to = newSide.Battlefield[from.Index];
            context.ActionsQueue.CancelActionsOf(creature);
            var replaced = to.OccupyingCreature;
            if (replaced != null) {
                Log($"{creature.Name} lands on {replaced.Name}, which is discarded", LogTag.Effects | LogTag.Creatures);
                context.ActionsQueue.CancelActionsOf(replaced);
                context.ModifierManager.UnregisterCreature(replaced);
                (replaced.Owner ?? newSide).DiscardCard(replaced);
            }

            from.Clear();
            to.Place(creature);
            creature.SetOwner(newSide);
            // Whose card it is: its old controller's, unless it's going home (a Gift or a stolen creature taken back)
            var cardOwner = creature.DeckOwner ?? oldSide;
            creature.DeckOwner = cardOwner == newSide ? null : cardOwner;
            Intel.MakeKnown(creature, oldSide);
            Log($"{creature.Name} goes over to {newSide.Name}", LogTag.Effects | LogTag.Creatures);
            NotifyBattlefieldsChanged(context, oldSide, newSide);
            context.GameMediator.NotifyCreatureStatsChanged(creature);

            SlotEffectRules.OnLeave(creature, from, context);
            SlotEffectRules.OnEnter(creature, to, context);
        }
    }
}
