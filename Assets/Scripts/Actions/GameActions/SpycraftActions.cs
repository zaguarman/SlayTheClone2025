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

    /// <summary>Never Let Them Know Your Next Move: every other creature on its side hands its stat raises to this one.</summary>
    public class FunnelAction : IGameAction {
        public ICreature Creature { get; }

        public FunnelAction(ICreature creature) {
            Creature = creature;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"FunnelAction: {Creature?.Name} takes its side's stat raises";
    }

    // One steal from each of the others, in a row, as part of this action (see StealStatChangesActionExecutor)
    public class FunnelActionExecutor : ActionExecutor<FunnelAction> {
        protected override void ExecuteAction(FunnelAction funnel, ActionExecutionContext context) {
            var creature = funnel.Creature;
            if (creature == null || creature.IsDead || creature.Slot?.Owner == null) return;
            foreach (var other in creature.Slot.Owner.Battlefield.Select(s => s.OccupyingCreature).Where(c => c != null && c != creature && !c.IsDead).ToList()) {
                context.ActionsQueue.AddAction(new StealStatChangesAction(creature, other));
            }
        }
    }

    /// <summary>
    /// Statuses move from one creature to another, with the Days they have left: Frame Job pins the harmful ones on the
    /// creature facing its target; Devil's Breath takes the good ones of the creature facing its target.
    /// </summary>
    public class PassStatusesAction : IGameAction {
        public ICreature From { get; }
        public ICreature To { get; }
        public bool Harmful { get; } // The harmful ones (what a Cleanse removes), or the good ones

        public PassStatusesAction(ICreature from, ICreature to, bool harmful) {
            From = from;
            To = to;
            Harmful = harmful;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"PassStatusesAction: {From?.Name}'s {(Harmful ? "harmful" : "good")} statuses go to {To?.Name}";
    }

    // Each lands as if applied on its original Day, so it keeps the Days it had left; Blessed and Heat keep their stacks.
    // What stops a status (Blessed, Doomed) stops it here too: then it's gone from both
    public class PassStatusesActionExecutor : ActionExecutor<PassStatusesAction> {
        protected override void ExecuteAction(PassStatusesAction pass, ActionExecutionContext context) {
            var from = pass.From;
            var to = pass.To;
            if (from == null || to == null || from == to || from.IsDead || to.IsDead || from.Slot == null || to.Slot == null) {
                Log($"Nothing passes: {pass}", LogTag.Effects);
                return;
            }
            var manager = context.ModifierManager;
            foreach (var status in manager.GetActiveModifiersFor(from).OfType<StatusEffectModifier>().Where(s => s.IsNegative == pass.Harmful).ToList()) {
                manager.RemoveModifier(from, status);
                int potency = status.IsPersistent ? status.Stacks : status.Potency;
                StatusRules.Land(context, to, status.EffectType, status.DurationInTurns, potency, status.TurnApplied);
                Log($"{status.EffectType} passes from {from.Name} to {to.Name}", LogTag.Effects);
            }
            from.Heat = manager.GetActiveModifiersFor(from).OfType<StatusEffectModifier>().Where(s => s.EffectType == StatusEffectType.HeatSignature).Sum(s => s.Stacks);
            context.GameMediator.NotifyCreatureStatsChanged(from);
            context.GameMediator.NotifyCreatureStatsChanged(to);
        }
    }
}
