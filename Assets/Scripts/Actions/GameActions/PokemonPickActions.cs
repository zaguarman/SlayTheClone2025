using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// Double Down (the user, 2026-10-17, Belly Drum: 3rd Parallel Job): the creature loses half its Health and its Attack
    /// doubles. Its Health is set, not damaged (nothing answers it); it never goes below 1. The Attack is a lasting change, as
    /// every stat change is: it stays for the combat.
    /// </summary>
    public class DoubleDownAction : IGameAction {
        public ICreature Creature { get; }

        public DoubleDownAction(ICreature creature) {
            Creature = creature;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"DoubleDownAction: {Creature?.Name} halves its Health, doubles its Attack";
    }

    public class DoubleDownActionExecutor : ActionExecutor<DoubleDownAction> {
        protected override void ExecuteAction(DoubleDownAction action, ActionExecutionContext context) {
            var creature = action.Creature;
            if (creature == null || creature.IsDead || creature.Slot == null) return;
            int lost = creature.Health / 2;
            if (lost > 0) creature.TakeHealthDamage(lost); // Not damage: its Health is set
            StatModifiers.Apply(context.ModifierManager, context.ModifierFactory, creature, creature.Attack, true, false, false, ModifierCalculationType.Flat, 0, context.TurnClock.TurnNumber);
            Log($"{creature.Name} doubles down: {creature.Attack}/{creature.Health}", LogTag.Effects);
            context.GameMediator.NotifyCreatureStatsChanged(creature);
        }
    }

    /// <summary>
    /// Split Pain (the user, 2026-10-17, Pain Split: Parabiosis): two creatures share their Health out equally, the odd
    /// point to the one that had less. Not damage, not healing: nothing answers it, and nothing turns it round.
    /// </summary>
    public class SplitPainAction : IGameAction {
        public ICreature One { get; }
        public ICreature Other { get; }

        public SplitPainAction(ICreature one, ICreature other) {
            One = one;
            Other = other;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"SplitPainAction: {One?.Name} and {Other?.Name} share their Health";
    }

    public class SplitPainActionExecutor : ActionExecutor<SplitPainAction> {
        protected override void ExecuteAction(SplitPainAction split, ActionExecutionContext context) {
            var one = split.One;
            var other = split.Other;
            if (one == null || other == null || one == other || one.IsDead || other.IsDead || one.Slot == null || other.Slot == null) return;
            int total = one.Health + other.Health;
            var (lower, higher) = one.Health <= other.Health ? (one, other) : (other, one);
            Set(lower, (total + 1) / 2);
            Set(higher, total / 2);
            Log($"{one.Name} and {other.Name} share their Health: {one.Health} and {other.Health}", LogTag.Effects);
            context.GameMediator.NotifyCreatureStatsChanged(one);
            context.GameMediator.NotifyCreatureStatsChanged(other);

            void Set(ICreature creature, int health) {
                health = System.Math.Min(health, creature.MaxHealth);
                if (creature.Health < health) creature.Heal(health - creature.Health);
                else if (creature.Health > health) creature.TakeHealthDamage(creature.Health - health);
            }
        }
    }

    /// <summary>Swap Abilities (the user, 2026-10-17, Skill Swap: Job Rotation): two creatures swap abilities, as Identity Theft does, for the combat.</summary>
    public class SwapAbilitiesAction : IGameAction {
        public ICreature One { get; }
        public ICreature Other { get; }

        public SwapAbilitiesAction(ICreature one, ICreature other) {
            One = one;
            Other = other;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"SwapAbilitiesAction: {One?.Name} and {Other?.Name} swap abilities";
    }

    public class SwapAbilitiesActionExecutor : ActionExecutor<SwapAbilitiesAction> {
        protected override void ExecuteAction(SwapAbilitiesAction swap, ActionExecutionContext context) {
            var one = swap.One;
            var other = swap.Other;
            if (one == null || other == null || one == other || one.IsDead || other.IsDead) return;
            Log($"{one.Name} ({one.Ability}) and {other.Name} ({other.Ability}) swap abilities", LogTag.Effects);
            (one.Ability, other.Ability) = (other.Ability, one.Ability);
            context.GameMediator.NotifyCreatureStatsChanged(one);
            context.GameMediator.NotifyCreatureStatsChanged(other);
            context.ModifierManager.RefreshConditionalStats();
        }
    }
}
