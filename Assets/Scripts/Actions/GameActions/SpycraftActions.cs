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

    /// <summary>Supply Chain Attack: the player draws fewer cards with their next hand (never fewer than none).</summary>
    public class CutDrawsAction : IGameAction, ISourcedAction {
        public ICard Source { get; set; }
        public IPlayer Player { get; }
        public int Cards { get; }

        public CutDrawsAction(IPlayer player, int cards) {
            Player = player;
            Cards = cards;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"CutDrawsAction: {Player?.Name} draws {Cards} fewer next Day";
    }

    public class CutDrawsActionExecutor : ActionExecutor<CutDrawsAction> {
        protected override void ExecuteAction(CutDrawsAction cut, ActionExecutionContext context) {
            if (cut.Player == null) return;
            cut.Player.ExtraDraws -= cut.Cards;
            Log($"{cut.Player.Name} will draw {cut.Cards} fewer cards next Day", LogTag.Actions | LogTag.Cards);
        }
    }

    /// <summary>GoldenEye: every Watchdog a player controls is wiped out.</summary>
    public class WipeWatchdogsAction : IGameAction {
        public IPlayer Side { get; }

        public WipeWatchdogsAction(IPlayer side) {
            Side = side;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"WipeWatchdogsAction: {Side?.Name}'s Watchdogs";
    }

    public class WipeWatchdogsActionExecutor : ActionExecutor<WipeWatchdogsAction> {
        protected override void ExecuteAction(WipeWatchdogsAction wipe, ActionExecutionContext context) =>
            context.ActionsQueue.WipeWatchdogs(wipe.Side);
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

    /// <summary>
    /// A discard cost (Social Engineering: you need to give information to get some): a payment. To play the card, that many
    /// other cards from the hand, among those not being played, are set aside; they're discarded when it resolves, and come
    /// back to the hand if it's taken back. It's paid in full or not at all: with fewer cards left than the cost, the card
    /// isn't played and stays in the hand. Two ways to pay, a card's theme decides: at random (chaotic: Social Engineering,
    /// Bilderberg Membership) or picked by hand (calculated: Headhunter, NDA, Dead Drop; see <see cref="IPlayer.DiscardPicks"/>
    /// and the picker on screen, which can change the picks while the play is queued).
    /// </summary>
    public static class DiscardCost {
        public static int Of(ICard card) => card?.SourceData?.discardCost ?? 0;

        /// <summary>Picked by hand rather than at random.</summary>
        public static bool IsChosen(ICard card) => card?.SourceData?.discardChosen ?? false;

        /// <summary>The cards that could pay for it now: the rest of the hand, minus the cards already being played.</summary>
        public static List<ICard> Payable(ICard card, IPlayer owner, IActionsQueue queue) {
            var playing = new HashSet<ICard>(queue?.GetPendingActions().OfType<PlayCardAction>().Select(p => p.GetCard()) ?? Enumerable.Empty<ICard>());
            return owner?.Hand.Where(c => c != card && !playing.Contains(c)).ToList() ?? new List<ICard>();
        }

        public static bool CanPay(ICard card, IPlayer owner, IActionsQueue queue) => Payable(card, owner, queue).Count >= Of(card);

        // Called when a play is queued; false: it can't be paid, so it isn't played
        internal static bool TryPay(PlayCardAction play, IActionsQueue queue) {
            var card = play.GetCard();
            var owner = play.GetOwner();
            int cost = Of(card);
            if (cost <= 0 || play.Paid.Count > 0 || owner == null || !owner.Hand.Contains(card)) return true;
            var payable = Payable(card, owner, queue);
            if (payable.Count < cost) {
                Log($"{card.Name} needs {cost} other cards from the hand to play; {owner.Name} has {payable.Count}", LogTag.Cards | LogTag.Actions);
                return false;
            }
            // Chosen: the cards picked, if they still can pay; otherwise (and for a random cost) at random
            var picked = IsChosen(card) && owner.DiscardPicks.TryGetValue(card, out var picks) ? picks : null;
            owner.DiscardPicks.Remove(card);
            if (picked != null && picked.Count == cost && picked.Distinct().Count() == cost && picked.All(payable.Contains)) {
                foreach (var paid in picked) SetAside(play, owner, paid);
            }
            else {
                for (int i = 0; i < cost; i++) {
                    var paid = payable[GameRandom.Next(payable.Count)];
                    payable.Remove(paid);
                    SetAside(play, owner, paid);
                }
            }
            Log($"{owner.Name} gives up {string.Join(", ", play.Paid.Select(c => c.Name))} to play {card.Name}", LogTag.Cards | LogTag.Actions);
            return true;
        }

        private static void SetAside(PlayCardAction play, IPlayer owner, ICard paid) {
            owner.RemoveFromHand(paid);
            owner.SetAside.Add(paid);
            play.Paid.Add(paid);
        }

        /// <summary>
        /// A chosen cost picked again while the play is queued (the picker on screen): what was set aside goes back to the
        /// hand and these are set aside instead. False, and nothing changes, if they can't pay for it.
        /// </summary>
        public static bool Repay(PlayCardAction play, List<ICard> chosen, IActionsQueue queue) {
            var owner = play.GetOwner();
            var card = play.GetCard();
            if (owner == null || chosen == null || chosen.Count != Of(card) || chosen.Distinct().Count() != chosen.Count) return false;
            var payable = new HashSet<ICard>(Payable(card, owner, queue).Concat(play.Paid));
            if (!chosen.All(payable.Contains)) return false;
            Refund(play);
            foreach (var paid in chosen) SetAside(play, owner, paid);
            Log($"{owner.Name} picks {string.Join(", ", chosen.Select(c => c.Name))} to pay for {card.Name}", LogTag.Cards | LogTag.Actions);
            return true;
        }

        internal static void Refund(PlayCardAction play) {
            var owner = play.GetOwner();
            foreach (var card in play.Paid) {
                owner.SetAside.Remove(card);
                owner.AddToHand(card);
            }
            play.Paid.Clear();
        }

        internal static void Spend(PlayCardAction play) {
            var owner = play.GetOwner();
            foreach (var card in play.Paid) {
                owner.SetAside.Remove(card);
                owner.Deck.AddToDiscardPile(card);
            }
            play.Paid.Clear();
            if (owner != null) Log($"{owner.Name}'s discard cost is spent", LogTag.Cards);
        }
    }

    /// <summary>Ghost Army: decoy tokens enter the caster's empty slots, left to right.</summary>
    public class SummonDecoysAction : IGameAction {
        public IPlayer Side { get; }
        public CreatureData Decoy { get; }
        public int Count { get; }

        public SummonDecoysAction(IPlayer side, CreatureData decoy, int count) {
            Side = side;
            Decoy = decoy;
            Count = count;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"SummonDecoysAction: {Count} {Decoy?.cardName} for {Side?.Name}";
    }

    // Each is a summon into its own free slot (no free slot: no more decoys), resolving right after this
    public class SummonDecoysActionExecutor : ActionExecutor<SummonDecoysAction> {
        protected override void ExecuteAction(SummonDecoysAction decoys, ActionExecutionContext context) {
            if (decoys.Side == null || decoys.Decoy == null) return;
            foreach (var slot in decoys.Side.Battlefield.Where(s => !s.IsOccupied() && EffectResolver.IncomingCreature(s, context.ActionsQueue) == null).Take(decoys.Count).ToList()) {
                var decoy = (ICreature)CardFactory.CreateCard(decoys.Decoy);
                context.ActionsQueue.AddAction(new SummonCreatureAction(decoy, decoys.Side, slot, true));
            }
        }
    }

    /// <summary>A creature of one side, off the board for now (Witness Protection Program), and when it comes back.</summary>
    public class GoneDark {
        public ICreature Creature { get; }
        public int SlotIndex { get; }    // Where it stood: it comes back there if it's free
        public int ReturnsOnTurn { get; } // The Day whose Dawn brings it back
        public bool Reborn { get; } // It died and rises again (Revive): no new identity, a new arrival
        public bool Ready { get; } // Rises ready, no Deployment Time (Phoenix Feather)
        public (int attack, int health, int speed) Kept { get; } // Stat changes it had in play, back on it when it rises

        public GoneDark(ICreature creature, int slotIndex, int returnsOnTurn, bool reborn = false, bool ready = false,
                        (int attack, int health, int speed) kept = default) {
            Creature = creature;
            SlotIndex = slotIndex;
            ReturnsOnTurn = returnsOnTurn;
            Reborn = reborn;
            Ready = ready;
            Kept = kept;
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
            if (creature.IsToken) { // No card to go back to a hand: it's gone
                home.DiscardCard(creature);
                return;
            }
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
            if (hidden.Reborn) { // Revive: back from the dead for all to see, a new arrival (its Arrival happens again)
                Log($"{creature.Name} rises again", LogTag.Effects | LogTag.Creatures);
                if (hidden.Ready) creature.MakeOperative(); // Phoenix Feather: ready at once
                // The stat changes it had come back with it (its statuses don't: it died); full Health, raised ones included
                int turn = context.TurnClock.TurnNumber;
                StatModifiers.Apply(context.ModifierManager, context.ModifierFactory, creature, hidden.Kept.attack, true, false, false, ModifierCalculationType.Flat, 0, turn);
                StatModifiers.Apply(context.ModifierManager, context.ModifierFactory, creature, hidden.Kept.health, false, true, false, ModifierCalculationType.Flat, 0, turn);
                StatModifiers.Apply(context.ModifierManager, context.ModifierFactory, creature, hidden.Kept.speed, false, false, true, ModifierCalculationType.Flat, 0, turn);
                Intel.OnEnteredPlay(creature, context.ModifierManager);
                context.GameMediator.NotifyBattlefieldStateChanged(side);
                SlotEffectRules.OnEnter(creature, slot, context);
                creature.HandleEffect(EffectTrigger.OnPlay, context.ActionsQueue);
                context.GameMediator.NotifyCreatureSummoned(creature, side);
                // Health lost for good stays on the card: one that lost it all rises only to die again
                if (creature.Health <= 0) context.ActionsQueue.AddAction(new CheckLethalAction(creature));
                return;
            }
            StatusRules.Land(context, creature, StatusEffectType.Concealed, ReturnToHandActionExecutor.NewIdentityDays, 0, context.TurnClock.TurnNumber);
            Intel.OnEnteredPlay(creature, context.ModifierManager);
            Log($"{creature.Name} is back, with a new identity", LogTag.Effects | LogTag.Creatures);
            context.GameMediator.NotifyBattlefieldStateChanged(side);
            SlotEffectRules.OnEnter(creature, slot, context);
            // Its stat changes stayed behind: a Health raise it was living on took its Health back, which can kill it
            if (creature.Health <= 0) context.ActionsQueue.AddAction(new CheckLethalAction(creature));
        }
    }
}
