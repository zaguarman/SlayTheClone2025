using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Maneuvers (the user, 2026-10-17): spells that only move creatures around the board, in three flavours by whose
    /// creatures they move. Ambivalent: either side's, to reorganise yours or disrupt theirs. Disruption: only the enemy's.
    /// Logistics: only yours. The moves: Swap (with its partner, <see cref="SwapPositionsAction"/>), Push (a step back),
    /// Pull (a step forward) and Shuffle (a whole side, at random). Nobody chose to move, so none is a move: a Rooted
    /// creature isn't moved, and Slot Effects see the creatures leave and enter.
    /// </summary>
    public static class Maneuvers {
        public static bool IsMove(ActionType type) => type is ActionType.SwapPositions or ActionType.Push or ActionType.Pull or ActionType.Shuffle or ActionType.Spin;

        /// <summary>
        /// Spin (the user, 2026-10-17, Fidget Spinner): the front three slots of a side, clockwise as its owner sees them: the
        /// Vanguard's creature to the right Flank, the right Flank's to the left Flank, the left Flank's to the Vanguard.
        /// </summary>
        public static readonly int[] SpinRing = { 0, 2, 1 };

        /// <summary>A spell that does nothing but move creatures.</summary>
        public static bool IsManeuver(IEnumerable<CardEffect> effects) {
            var actions = effects?.SelectMany(e => e.actions).ToList();
            return actions != null && actions.Count > 0 && actions.All(a => IsMove(a.actionType));
        }

        /// <summary>Its flavour, by whose creatures it moves: "Disruption", "Logistics" or "Ambivalent".</summary>
        public static string FlavourOf(IEnumerable<CardEffect> effects) {
            var targets = effects.SelectMany(e => e.actions).Select(a => a.targetType).ToList();
            if (targets.All(t => t is TargetType.Enemy or TargetType.EnemyCreatures)) return "Disruption";
            if (targets.All(t => t is TargetType.Player or TargetType.FriendlyCreatures)) return "Logistics";
            return "Ambivalent";
        }

        /// <summary>
        /// The slot a step back: the Vanguard to a Flank (an empty one if there is one, else the left), a Flank to the
        /// Headquarters slot behind it. Null: Headquarters can't go further back.
        /// </summary>
        public static int? Behind(int index, IReadOnlyList<BoardSlot> row) => index switch {
            0 => row.Count > 2 && !row[1].IsOccupied() ? 1 : row.Count > 2 && !row[2].IsOccupied() ? 2 : 1,
            1 => 3,
            2 => 4,
            _ => null
        };

        /// <summary>The slot a step forward: Headquarters to the Flank in front of it, a Flank to the Vanguard. Null: the Vanguard is already in front.</summary>
        public static int? Ahead(int index) => index switch {
            3 => 1,
            4 => 2,
            1 or 2 => 0,
            _ => null
        };

        /// <summary>Two creatures trading slots (or one moving to an empty slot): the board, then what the slots do to them.</summary>
        public static void Trade(BoardSlot from, BoardSlot to, ActionExecutionContext context) {
            var mover = from.OccupyingCreature;
            var other = to.OccupyingCreature;
            BoardSlot.Swap(from, to);
            if (mover != null) {
                SlotEffectRules.OnLeave(mover, from, context);
                SlotEffectRules.OnEnter(mover, to, context);
            }
            if (other != null) {
                SlotEffectRules.OnLeave(other, to, context);
                SlotEffectRules.OnEnter(other, from, context);
            }
        }
    }

    /// <summary>Push (back) or Pull (forward): one step in its formation; a creature already there trades places with it.</summary>
    public class PushAction : IGameAction {
        public ICreature Creature { get; }
        public bool Forward { get; }

        public PushAction(ICreature creature, bool forward) {
            Creature = creature;
            Forward = forward;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"PushAction: {Creature?.Name} {(Forward ? "pulled forward" : "pushed back")}";
    }

    /// <summary>Shuffle: every creature on that side that can be moved changes slots at random (empty slots included).</summary>
    public class ShuffleAction : IGameAction {
        public IPlayer Side { get; }

        public ShuffleAction(IPlayer side) {
            Side = side;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"ShuffleAction: {Side?.Name}'s side";
    }

    /// <summary>Spin: the creatures in that side's Vanguard and Flanks move round one slot, clockwise (see <see cref="Maneuvers.SpinRing"/>).</summary>
    public class SpinAction : IGameAction {
        public IPlayer Side { get; }

        public SpinAction(IPlayer side) {
            Side = side;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"SpinAction: {Side?.Name}'s Vanguard and Flanks";
    }

    // A Rooted creature stays put; the others go round the slots left, skipping it
    public class SpinActionExecutor : ActionExecutor<SpinAction> {
        protected override void ExecuteAction(SpinAction spin, ActionExecutionContext context) {
            var row = spin.Side?.Battlefield;
            if (row == null || row.Count < 3) return;
            var ring = Maneuvers.SpinRing.Select(i => row[i]).Where(s => !MovementRules.IsRooted(s.OccupyingCreature)).ToList();
            if (ring.Count < 2 || ring.All(s => s.OccupyingCreature == null)) return;
            var before = ring.ToDictionary(s => s, s => s.OccupyingCreature);
            var moving = ring.Select(s => s.Clear()).ToList();
            for (int i = 0; i < ring.Count; i++) {
                var creature = moving[i];
                if (creature != null) ring[(i + 1) % ring.Count].Place(creature);
            }
            Log($"{spin.Side.Name}'s Vanguard and Flanks spin clockwise", LogTag.Effects | LogTag.Creatures);
            foreach (var (slot, creature) in before.Where(pair => pair.Value != null && pair.Value.Slot != pair.Key)) {
                SlotEffectRules.OnLeave(creature, slot, context);
                SlotEffectRules.OnEnter(creature, creature.Slot, context);
            }
            NotifyBattlefieldsChanged(context, spin.Side, spin.Side);
        }
    }

    public class PushActionExecutor : ActionExecutor<PushAction> {
        protected override void ExecuteAction(PushAction push, ActionExecutionContext context) {
            var creature = push.Creature;
            var from = creature?.Slot;
            if (creature == null || creature.IsDead || from?.Owner == null) return;
            var row = from.Owner.Battlefield;
            int? index = push.Forward ? Maneuvers.Ahead(from.Index) : Maneuvers.Behind(from.Index, row);
            if (index == null || index.Value >= row.Count) {
                Log($"{creature.Name} can't go further {(push.Forward ? "forward" : "back")}", LogTag.Effects | LogTag.Creatures);
                return;
            }
            var to = row[index.Value];
            if (MovementRules.IsRooted(creature) || MovementRules.IsRooted(to.OccupyingCreature)) return; // Rooted: nothing moves it
            Log($"{creature.Name} is {(push.Forward ? "pulled forward" : "pushed back")} to slot {to.Index}" +
                (to.OccupyingCreature != null ? $", trading places with {to.OccupyingCreature.Name}" : ""), LogTag.Effects | LogTag.Creatures);
            Maneuvers.Trade(from, to, context);
            NotifyBattlefieldsChanged(context, from.Owner, from.Owner);
        }
    }

    public class ShuffleActionExecutor : ActionExecutor<ShuffleAction> {
        protected override void ExecuteAction(ShuffleAction shuffle, ActionExecutionContext context) {
            var side = shuffle.Side;
            if (side?.Battlefield == null) return;
            // The slots in play: every one but a Rooted creature's, which stays put
            var slots = side.Battlefield.Where(s => s.OccupyingCreature is not { IsDead: true } && !MovementRules.IsRooted(s.OccupyingCreature)).ToList();
            var before = slots.ToDictionary(s => s, s => s.OccupyingCreature);
            var creatures = slots.Select(s => s.Clear()).ToList();
            for (int i = creatures.Count - 1; i > 0; i--) { // Fisher-Yates, with the game's seed
                int j = GameRandom.Next(i + 1);
                (creatures[i], creatures[j]) = (creatures[j], creatures[i]);
            }
            for (int i = 0; i < slots.Count; i++) {
                if (creatures[i] != null) slots[i].Place(creatures[i]);
            }
            Log($"{side.Name}'s side is shuffled", LogTag.Effects | LogTag.Creatures);
            foreach (var (slot, creature) in before.Where(pair => pair.Value != null && pair.Value.Slot != pair.Key)) {
                SlotEffectRules.OnLeave(creature, slot, context);
                SlotEffectRules.OnEnter(creature, creature.Slot, context);
            }
            NotifyBattlefieldsChanged(context, side, side);
        }
    }
}
