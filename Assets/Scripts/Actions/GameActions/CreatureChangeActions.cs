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

    /// <summary>Geomagnetic Reversal (the user, 2026-10-16): its Attack and Health trade places.</summary>
    public class SwapStatsAction : IGameAction {
        public ICreature Creature { get; }

        public SwapStatsAction(ICreature creature) {
            Creature = creature;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"SwapStatsAction: {Creature?.Name}'s Attack and Health trade places";
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

    /// <summary>
    /// The strongest takes from the rest (Laissez-Faire, The Invisible Hand): every other creature in the group, the whole
    /// board or the taker's side, loses up to <see cref="Value"/> Attack, and the taker gains as much as they had to give.
    /// </summary>
    public class ConsolidateAction : IGameAction {
        public ICreature Taker { get; }
        public int Value { get; }
        public bool WholeBoard { get; }
        public IPlayer Controller { get; }
        public ICard Origin { get; }

        public ConsolidateAction(ICreature taker, int value, bool wholeBoard, IPlayer controller, ICard origin) {
            Taker = taker;
            Value = value;
            WholeBoard = wholeBoard;
            Controller = controller;
            Origin = origin;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"ConsolidateAction: {Taker?.Name} takes {Value} Attack from each other creature {(WholeBoard ? "on the board" : "on its side")}";
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

    // Its Attack becomes its Health as it is now (damage counted), and its Health its Attack, at full: a 5/2 (hurt from 5/6)
    // becomes a 2/5. Lasting stat changes, as when a creature copies another; 0 Attack becomes 0 Health, and it dies
    public class SwapStatsActionExecutor : ActionExecutor<SwapStatsAction> {
        protected override void ExecuteAction(SwapStatsAction swap, ActionExecutionContext context) {
            var creature = swap.Creature;
            if (creature == null || creature.IsDead || creature.Slot == null) return;
            int attack = creature.Attack, health = creature.Health, turn = context.TurnClock.TurnNumber;
            var manager = context.ModifierManager;
            StatModifiers.Apply(manager, context.ModifierFactory, creature, health - attack, true, false, false, ModifierCalculationType.Flat, 0, turn);
            StatModifiers.Apply(manager, context.ModifierFactory, creature, attack - creature.MaxHealth, false, true, false, ModifierCalculationType.Flat, 0, turn);
            if (creature.Health < attack) creature.Heal(attack - creature.Health);
            else if (creature.Health > attack) creature.TakeHealthDamage(creature.Health - attack); // Not damage: its Health is set
            Log($"{creature.Name}'s Attack and Health trade places: {creature.Attack}/{creature.Health}", LogTag.Effects);
            context.GameMediator.NotifyCreatureStatsChanged(creature);
            if (creature.Health <= 0) context.ActionsQueue.AddAction(new CheckLethalAction(creature));
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

    // The creature trades places with its partner in the formation (the other Flank, the other Headquarters slot; the
    // Vanguard with the left Flank). An empty slot just moves it.
    // Nobody chose to move, so it isn't a move: nothing stops it, but Slot Effects see both leave and enter
    public class SwapPositionsActionExecutor : ActionExecutor<SwapPositionsAction> {
        protected override void ExecuteAction(SwapPositionsAction swap, ActionExecutionContext context) {
            var creature = swap.Creature;
            var from = creature?.Slot;
            if (creature == null || creature.IsDead || from?.Owner == null) return;

            var row = from.Owner.Battlefield;
            var to = row[Formation.Partner(from.Index)];
            var partner = to.OccupyingCreature;
            if (MovementRules.IsRooted(from.OccupyingCreature) || MovementRules.IsRooted(to.OccupyingCreature)) return; // Rooted: nothing moves it
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

    // Each other creature with Attack to give loses it as a stat change from the effect's owner (its abilities can stop or
    // bounce that), and the taker gains what was asked of them, while they all stay in play. Too Mainstream creatures are
    // left out: it's an effect on more than one creature
    public class ConsolidateActionExecutor : ActionExecutor<ConsolidateAction> {
        protected override void ExecuteAction(ConsolidateAction consolidate, ActionExecutionContext context) {
            var taker = consolidate.Taker;
            if (taker == null || taker.IsDead || taker.Slot?.Owner == null || consolidate.Value <= 0) return;
            var sides = consolidate.WholeBoard ? new[] { taker.Slot.Owner, taker.Slot.Owner.Opponent } : new[] { taker.Slot.Owner };
            var givers = sides.Where(side => side != null).SelectMany(side => side.Battlefield).Select(s => s.OccupyingCreature)
                .Where(c => c != null && c != taker && !c.IsDead && c.Attack > 0 && !Abilities.Has(c, CreatureAbility.TooMainstream)).ToList();
            int taken = 0;
            foreach (var giver in givers) {
                int amount = System.Math.Min(consolidate.Value, giver.Attack);
                context.ActionsQueue.AddAction(new ModifyAction(giver, -amount, true, false, false, controller: consolidate.Controller) { Origin = consolidate.Origin });
                taken += amount;
            }
            Log($"{taker.Name} takes {taken} Attack from {givers.Count} creatures", LogTag.Effects);
            if (taken > 0) context.ActionsQueue.AddAction(new ModifyAction(taker, taken, true, false, false, controller: consolidate.Controller) { Origin = consolidate.Origin });
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
