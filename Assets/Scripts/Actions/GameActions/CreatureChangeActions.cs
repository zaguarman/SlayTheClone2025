using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Effects that change one creature after another, or a creature's place: copying stats (Wannabe), stealing stat raises
    /// (Credit Grab), swapping places (Shell Game) and gaining an ability. Any card can carry them: a creature's ability or
    /// trigger, or a spell (see EffectResolver).
    /// </summary>
    public class CopyStatsAction : IGameAction {
        public ICreature Copier { get; }
        public ICreature Model { get; }

        public CopyStatsAction(ICreature copier, ICreature model) {
            Copier = copier;
            Model = model;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"CopyStatsAction: {Copier?.Name} copies {Model?.Name}";
    }

    public class StealStatChangesAction : IGameAction {
        public ICreature Thief { get; }
        public ICreature Victim { get; }

        public StealStatChangesAction(ICreature thief, ICreature victim) {
            Thief = thief;
            Victim = victim;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"StealStatChangesAction: {Thief?.Name} steals from {Victim?.Name}";
    }

    public class SwapPositionsAction : IGameAction {
        public ICreature Creature { get; }

        public SwapPositionsAction(ICreature creature) {
            Creature = creature;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"SwapPositionsAction: {Creature?.Name} trades places with its neighbor";
    }

    public class GrantAbilityAction : IGameAction {
        public ICreature Creature { get; }
        public CreatureAbility Ability { get; }

        public GrantAbilityAction(ICreature creature, CreatureAbility ability) {
            Creature = creature;
            Ability = ability;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"GrantAbilityAction: {Creature?.Name} gains {Ability}";
    }

    // Its Attack, maximum Health and Speed become the model's, and its Health too: a copy, damage included. It lasts
    // while it stays in play, like a lasting stat change (it isn't one: abilities that bend stat changes don't touch it)
    public class CopyStatsActionExecutor : ActionExecutor<CopyStatsAction> {
        protected override void ExecuteAction(CopyStatsAction copy, ActionExecutionContext context) {
            var copier = copy.Copier;
            var model = copy.Model;
            if (copier == null || model == null || copier == model || copier.IsDead || model.IsDead || copier.Slot == null) {
                Log($"Nothing to copy: {copy}", LogTag.Effects);
                return;
            }

            int turn = context.TurnClock.TurnNumber;
            var manager = context.ModifierManager;
            int modelHealth = model.Health;
            StatModifiers.Apply(manager, context.ModifierFactory, copier, model.Attack - copier.Attack, true, false, false, ModifierCalculationType.Flat, 0, turn);
            StatModifiers.Apply(manager, context.ModifierFactory, copier, model.MaxHealth - copier.MaxHealth, false, true, false, ModifierCalculationType.Flat, 0, turn);
            StatModifiers.Apply(manager, context.ModifierFactory, copier, model.Speed - copier.Speed, false, false, true, ModifierCalculationType.Flat, 0, turn);
            if (copier.Health < modelHealth) copier.Heal(modelHealth - copier.Health);
            else if (copier.Health > modelHealth) copier.TakeHealthDamage(copier.Health - modelHealth); // Not damage: its Health is set
            Log($"{copier.Name} copies {model.Name}: {copier.Attack}/{copier.Health}/{copier.Speed}", LogTag.Effects);
            context.GameMediator.NotifyCreatureStatsChanged(copier);
            if (copier.Health <= 0) context.ActionsQueue.AddAction(new CheckLethalAction(copier));
        }
    }

    // Every stat raise on the victim moves to the thief: its lasting and timed raises (as they are, with their Days left)
    // and the raises in its Persistent Stat Modifications. Lowered stats stay where they are
    public class StealStatChangesActionExecutor : ActionExecutor<StealStatChangesAction> {
        protected override void ExecuteAction(StealStatChangesAction steal, ActionExecutionContext context) {
            var thief = steal.Thief;
            var victim = steal.Victim;
            if (thief == null || victim == null || thief == victim || thief.IsDead || victim.IsDead || thief.Slot == null) {
                Log($"Nothing to steal: {steal}", LogTag.Effects);
                return;
            }

            var manager = context.ModifierManager;
            var raises = manager.GetActiveModifiersFor(victim).Where(m => !(m is StatusEffectModifier) && IsRaise(m)).ToList();
            foreach (var raise in raises) {
                manager.RemoveModifier(victim, raise);
                manager.ApplyModifier(thief, raise);
            }
            var (attack, health, speed) = victim.PersistentChanges;
            (attack, health, speed) = (System.Math.Max(0, attack), System.Math.Max(0, health), System.Math.Max(0, speed));
            if (attack + health + speed > 0) {
                manager.ChangeBaseStats(victim, -attack, -health, -speed);
                manager.ChangeBaseStats(thief, attack, health, speed);
            }
            Log($"{thief.Name} steals {raises.Count} stat raises and +{attack}/+{health}/+{speed} for good from {victim.Name}", LogTag.Effects);
            context.GameMediator.NotifyCreatureStatsChanged(thief);
            context.GameMediator.NotifyCreatureStatsChanged(victim);
            if (victim.Health <= 0) context.ActionsQueue.AddAction(new CheckLethalAction(victim)); // Its Health raise was holding it up
        }

        private static bool IsRaise(IModifier modifier) {
            foreach (ModifiableStat stat in System.Enum.GetValues(typeof(ModifiableStat))) {
                if (modifier.TryGetStatModification(stat, out _, out int value) && value > 0) return true;
            }
            return false;
        }
    }

    // The creature trades places with the one to its right, or to its left at the end of the row. An empty slot just moves it.
    // Nobody chose to move, so it isn't a move: nothing stops it, but Slot Effects see both leave and enter
    public class SwapPositionsActionExecutor : ActionExecutor<SwapPositionsAction> {
        protected override void ExecuteAction(SwapPositionsAction swap, ActionExecutionContext context) {
            var creature = swap.Creature;
            var from = creature?.Slot;
            if (creature == null || creature.IsDead || from?.Owner == null) return;

            var row = from.Owner.Battlefield;
            var to = row[from.Index + 1 < row.Count ? from.Index + 1 : from.Index - 1];
            var partner = to.OccupyingCreature;
            BoardSlot.Swap(from, to);
            Log($"{creature.Name} trades places with {partner?.Name ?? "an empty slot"}", LogTag.Effects | LogTag.Creatures);
            NotifyBattlefieldsChanged(context, from.Owner, from.Owner);

            SlotEffectRules.OnLeave(creature, from, context);
            SlotEffectRules.OnEnter(creature, creature.Slot, context);
            if (partner != null) {
                SlotEffectRules.OnLeave(partner, to, context);
                SlotEffectRules.OnEnter(partner, partner.Slot, context);
            }
        }
    }

    // Like Hive Mind: the new ability replaces its own until its card is reshuffled
    public class GrantAbilityActionExecutor : ActionExecutor<GrantAbilityAction> {
        protected override void ExecuteAction(GrantAbilityAction grant, ActionExecutionContext context) {
            var creature = grant.Creature;
            if (creature == null || creature.IsDead || grant.Ability == CreatureAbility.None) return;
            Log($"{creature.Name} gains {grant.Ability} (was {creature.Ability})", LogTag.Effects);
            creature.Ability = grant.Ability;
            context.GameMediator.NotifyCreatureStatsChanged(creature);
        }
    }
}
