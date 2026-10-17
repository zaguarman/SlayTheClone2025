using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Mimic (the user, 2026-10-17, Pokémon's Ditto: Monkey See, Monkey Do): a fresh copy of the target creature, from its
    /// card, appears in the slot facing it on the caster's side. Whatever stands there is replaced (discarded, as any summon
    /// over it would); an Irreplaceable creature there keeps its slot and nothing appears. The copy is a token: no card goes
    /// back to a hand when it leaves.
    /// </summary>
    public class MimicAction : IGameAction {
        public ICreature Model { get; }
        public IPlayer Caster { get; }

        public MimicAction(ICreature model, IPlayer caster) {
            Model = model;
            Caster = caster;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"MimicAction: a copy of {Model?.Name} for {Caster?.Name}";
    }

    public class MimicActionExecutor : ActionExecutor<MimicAction> {
        protected override void ExecuteAction(MimicAction mimic, ActionExecutionContext context) {
            var model = mimic.Model;
            var caster = mimic.Caster;
            if (model == null || model.IsDead || model.Slot == null || caster?.Battlefield == null || model.SourceData == null) return;
            if (model.Slot.Index >= caster.Battlefield.Count) return;
            var slot = caster.Battlefield[model.Slot.Index];
            if (!Abilities.CanBeReplaced(slot.OccupyingCreature)) {
                Log($"{slot.OccupyingCreature.Name} is Irreplaceable: no copy of {model.Name} appears", LogTag.Effects);
                return;
            }
            if (CardFactory.CreateCard(model.SourceData) is not Creature copy) return;
            copy.IsToken = true;
            Log($"Monkey see, monkey do: a copy of {model.Name} appears on {caster.Name}'s side, in slot {slot.Index}", LogTag.Effects | LogTag.Creatures);
            context.ActionsQueue.AddAction(new SummonCreatureAction(copy, caster, slot, fromDeck: true));
        }
    }

    /// <summary>
    /// Trade Stats (the user, 2026-10-17, Pokémon's Trick turned to stats: Freaky Friday): two creatures trade Attack and
    /// Health, each waking up in the other's body. Current Health goes with the body (a hurt one stays hurt), as Copy Stats
    /// sets it.
    /// </summary>
    public class TradeStatsAction : IGameAction {
        public ICreature One { get; }
        public ICreature Other { get; }

        public TradeStatsAction(ICreature one, ICreature other) {
            One = one;
            Other = other;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"TradeStatsAction: {One?.Name} and {Other?.Name} trade Attack and Health";
    }

    public class TradeStatsActionExecutor : ActionExecutor<TradeStatsAction> {
        protected override void ExecuteAction(TradeStatsAction trade, ActionExecutionContext context) {
            var one = trade.One;
            var other = trade.Other;
            if (one == null || other == null || one == other || one.IsDead || other.IsDead || one.Slot == null || other.Slot == null) {
                Log($"Nothing to trade: {trade}", LogTag.Effects);
                return;
            }
            int turn = context.TurnClock.TurnNumber;
            var manager = context.ModifierManager;
            var (oneAttack, oneMax, oneHealth) = (one.Attack, one.MaxHealth, one.Health);
            var (otherAttack, otherMax, otherHealth) = (other.Attack, other.MaxHealth, other.Health);
            Become(one, otherAttack, otherMax, otherHealth);
            Become(other, oneAttack, oneMax, oneHealth);
            Log($"{one.Name} and {other.Name} trade Attack and Health: {one.Attack}/{one.Health} and {other.Attack}/{other.Health}", LogTag.Effects);
            context.GameMediator.NotifyCreatureStatsChanged(one);
            context.GameMediator.NotifyCreatureStatsChanged(other);
            if (one.Health <= 0) context.ActionsQueue.AddAction(new CheckLethalAction(one));
            if (other.Health <= 0) context.ActionsQueue.AddAction(new CheckLethalAction(other));

            void Become(ICreature creature, int attack, int maxHealth, int health) {
                StatModifiers.Apply(manager, context.ModifierFactory, creature, attack - creature.Attack, true, false, false, ModifierCalculationType.Flat, 0, turn);
                StatModifiers.Apply(manager, context.ModifierFactory, creature, maxHealth - creature.MaxHealth, false, true, false, ModifierCalculationType.Flat, 0, turn);
                if (creature.Health < health) creature.Heal(health - creature.Health);
                else if (creature.Health > health) creature.TakeHealthDamage(creature.Health - health); // Not damage: its Health is set
            }
        }
    }

    /// <summary>
    /// Seal (the user, 2026-10-17, Pokémon's Imprison: DMCA): for some Days, that player can't play a card that shares its
    /// name with a creature on the board, either side's. The copy on the board holds the rights; a second copy in their hand
    /// waits until the first is gone (or the Days run out).
    /// </summary>
    public class SealAction : IGameAction {
        public IPlayer Player { get; }
        public int Days { get; }

        public SealAction(IPlayer player, int days) {
            Player = player;
            Days = days;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"SealAction: {Player?.Name} sealed for {Days} Days";
    }

    public class SealActionExecutor : ActionExecutor<SealAction> {
        protected override void ExecuteAction(SealAction seal, ActionExecutionContext context) {
            if (seal.Player == null || seal.Days <= 0) return;
            int until = context.TurnClock.TurnNumber + seal.Days - 1;
            if (seal.Player.SealedUntilDay is int already && already > until) return; // A longer seal already on them
            seal.Player.SealedUntilDay = until;
            Log($"DMCA: {seal.Player.Name} can't play a card that shares its name with a creature on the board until the end of Day {until}", LogTag.Effects);
        }
    }

    /// <summary>What stops a card from being played at all this Day (the DMCA), checked where a play is offered or queued.</summary>
    public static class PlayRestrictions {
        /// <summary>Why the card can't be played today, or null if it can.</summary>
        public static string Why(ICard card, IPlayer owner, int today) {
            if (card is not ICreature || owner == null || owner.SealedUntilDay is not int until || today > until) return null;
            var sides = new[] { owner, owner.Opponent }.Where(p => p?.Battlefield != null).SelectMany(p => p.Battlefield);
            bool onTheBoard = sides.Any(s => s.OccupyingCreature is { IsDead: false } standing && standing.Name == card.Name);
            return onTheBoard ? $"DMCA: {card.Name} is already on the board" : null;
        }
    }
}
