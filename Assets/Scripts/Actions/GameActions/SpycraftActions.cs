using System.Collections.Generic;
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
        /// <summary>Brainwashed: it loses its ability (until its card is reshuffled).</summary>
        public bool LosesAbility { get; }

        public ChangeSidesAction(ICreature creature, IPlayer newController, bool losesAbility = false) {
            Creature = creature;
            NewController = newController;
            LosesAbility = losesAbility;
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
            if (change.LosesAbility) creature.Ability = CreatureAbility.None;
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
            context.GameMediator.NotifyCreatureStatsChanged(from);
            context.GameMediator.NotifyCreatureStatsChanged(to);
        }
    }

    /// <summary>A creature of one side, off the board for now (Witness Protection Program), and when it comes back.</summary>
    public class GoneDark {
        public ICreature Creature { get; }
        public int SlotIndex { get; }    // Where it stood: it comes back there if it's free
        public int ReturnsOnTurn { get; } // The Day whose Dawn brings it back

        public GoneDark(ICreature creature, int slotIndex, int returnsOnTurn) {
            Creature = creature;
            SlotIndex = slotIndex;
            ReturnsOnTurn = returnsOnTurn;
        }

        /// <summary>At Dawn: the creatures due back today are queued to come back, ahead of the Day's Dawn effects.</summary>
        public static void ProcessDawn(IEnumerable<IPlayer> players, int turn, IActionsQueue queue) {
            foreach (var player in players) {
                foreach (var hidden in player.GoneDark.Where(g => g.ReturnsOnTurn <= turn).ToList()) {
                    queue.AddAction(new ReappearAction(player, hidden));
                }
            }
        }

        /// <summary>
        /// A creature leaves the board without dying: what it planned is cancelled, its statuses and stat changes go (it's
        /// out of play), and its slot is free.
        /// </summary>
        public static void LeavePlay(ICreature creature, ActionExecutionContext context) {
            var slot = creature.Slot;
            context.ActionsQueue.CancelActionsOf(creature);
            context.ModifierManager.UnregisterCreature(creature);
            slot?.Clear();
            if (slot?.Owner != null) context.GameMediator.NotifyBattlefieldStateChanged(slot.Owner);
        }
    }

    /// <summary>Extraction: the creature goes back to its owner's hand, with the next Day's hand.</summary>
    public class ReturnToHandAction : IGameAction {
        public ICreature Creature { get; }
        /// <summary>He Didn't Kill Himself: it "dies" first, so its Last Will happens.</summary>
        public bool FakesItsDeath { get; }

        public ReturnToHandAction(ICreature creature, bool fakesItsDeath = false) {
            Creature = creature;
            FakesItsDeath = fakesItsDeath;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"ReturnToHandAction: {Creature?.Name}{(FakesItsDeath ? " fakes its death and" : "")} goes back to the hand";
    }

    // It leaves play (not a death: a hand isn't a grave) unless it fakes its death, which everybody believes: its Last Will
    // happens and the death is announced. Either way its card comes back with the next Day's hand. Extracted while Revealed,
    // it comes back with a new face: played again, it arrives Concealed
    public class ReturnToHandActionExecutor : ActionExecutor<ReturnToHandAction> {
        public const int NewIdentityDays = 2;

        protected override void ExecuteAction(ReturnToHandAction back, ActionExecutionContext context) {
            var creature = back.Creature;
            if (creature == null || creature.IsDead || creature.Slot == null) {
                Log($"Nothing goes back: {back}", LogTag.Effects);
                return;
            }
            bool wasRevealed = context.ModifierManager.HasStatusEffect(creature, StatusEffectType.Revealed);
            if (back.FakesItsDeath) {
                Log($"{creature.Name} \"dies\"", LogTag.Effects | LogTag.Creatures);
                context.GameMediator.NotifyCreatureDied(creature);
                creature.HandleEffect(EffectTrigger.OnDeath, context.ActionsQueue); // A Last Will waits in its layer, like a real one
            }
            var home = creature.DeckOwner ?? creature.Owner;
            GoneDark.LeavePlay(creature, context);
            home.ReturnToHand(creature);
            if (wasRevealed && !back.FakesItsDeath && home.ComingBack.LastOrDefault() is ICreature copy) copy.ArrivesConcealed = NewIdentityDays;
            context.GameMediator.NotifyHandStateChanged(home);
        }
    }

    /// <summary>Witness Protection Program: the creature leaves the board, and comes back at the next Dawn with a new identity.</summary>
    public class VanishAction : IGameAction {
        public ICreature Creature { get; }

        public VanishAction(ICreature creature) {
            Creature = creature;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"VanishAction: {Creature?.Name} goes dark";
    }

    // Off the board, nothing reaches it: no damage, no status. Its slot is empty meanwhile, so an attack there hits its player
    public class VanishActionExecutor : ActionExecutor<VanishAction> {
        protected override void ExecuteAction(VanishAction vanish, ActionExecutionContext context) {
            var creature = vanish.Creature;
            var side = creature?.Owner;
            if (creature == null || creature.IsDead || creature.Slot == null || side == null) return;
            int index = creature.Slot.Index;
            GoneDark.LeavePlay(creature, context);
            side.GoneDark.Add(new GoneDark(creature, index, context.TurnClock.TurnNumber + 1));
            Log($"{creature.Name} goes dark until the next Dawn", LogTag.Effects | LogTag.Creatures);
        }
    }

    /// <summary>A creature that went dark comes back: to its old slot if it's free, else the first free one on its side.</summary>
    public class ReappearAction : IGameAction {
        public IPlayer Side { get; }
        public GoneDark Hidden { get; }

        public ReappearAction(IPlayer side, GoneDark hidden) {
            Side = side;
            Hidden = hidden;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"ReappearAction: {Hidden?.Creature?.Name} comes back to {Side?.Name}'s side";
    }

    // A new identity: it enters as ??? (Concealed for 2 Days, so it stays unknown), its old statuses gone. It isn't a summon:
    // its Arrival effects don't happen again. No free slot: it never comes back, and its card goes to the discard pile
    public class ReappearActionExecutor : ActionExecutor<ReappearAction> {
        protected override void ExecuteAction(ReappearAction back, ActionExecutionContext context) {
            var side = back.Side;
            var hidden = back.Hidden;
            if (side == null || hidden == null || !side.GoneDark.Remove(hidden)) return;
            var creature = hidden.Creature;
            var slot = !side.Battlefield[hidden.SlotIndex].IsOccupied() ? side.Battlefield[hidden.SlotIndex] : side.Battlefield.FirstOrDefault(s => !s.IsOccupied());
            if (slot == null) {
                Log($"{creature.Name} has nowhere to come back to: its card goes to the discard pile", LogTag.Effects | LogTag.Creatures);
                side.DiscardCard(creature);
                return;
            }
            slot.Place(creature);
            context.ModifierManager.RegisterCreature(creature);
            creature.EnterPlay(context.TurnClock.TurnNumber);
            StatusRules.Land(context, creature, StatusEffectType.Concealed, ReturnToHandActionExecutor.NewIdentityDays, 0, context.TurnClock.TurnNumber);
            Intel.OnEnteredPlay(creature, context.ModifierManager);
            Log($"{creature.Name} is back, with a new identity", LogTag.Effects | LogTag.Creatures);
            context.GameMediator.NotifyBattlefieldStateChanged(side);
            SlotEffectRules.OnEnter(creature, slot, context);
        }
    }
}
