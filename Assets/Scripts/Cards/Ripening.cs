using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Ripening (the user, 2026-10-16, from Slay the Spire's Retain cards that grow): a spell kept in the hand gets better.
    /// At each Dawn discard, every Ripening spell still in the hand ripens once: its numbers go up by 1, up to
    /// <see cref="Most"/> times. Played or discarded, it leaves as a fresh copy (see Deck.FreshCopy): the next time it's
    /// green again. Holding a spell becomes a choice instead of a leftover.
    /// </summary>
    public static class Ripening {
        public const int Most = 3;

        public static bool Has(ICard card) => card?.SourceData?.ripening == true;

        /// <summary>At the Dawn discard: each Ripening spell in the hand ripens once (until it's fully ripe).</summary>
        public static void Ripen(IPlayer player, IGameMediator mediator) {
            bool any = false;
            foreach (var spell in player.Hand.OfType<Spell>().Where(s => Has(s) && s.Ripeness < Most)) {
                spell.Ripeness++;
                foreach (var action in spell.Effects.SelectMany(e => e.actions)) Numbers.Raise(action);
                Log($"{spell.Name} ripens in {player.Name}'s hand (+{spell.Ripeness})", LogTag.Cards | LogTag.Effects);
                any = true;
            }
            if (any) mediator?.NotifyHandStateChanged(player);
        }
    }

    /// <summary>
    /// "One better number", the one rule Ripening and card upgrades share: a damage, heal, Armor, draw or stat change of 1 more
    /// (a stat cut of 1 more), a Burn, Blessed or Heat of 1 more stack. Other parts (stuns, Days, destroys) stay.
    /// </summary>
    public static class Numbers {
        public static bool CanRaise(EffectAction action) => action.actionType switch {
            ActionType.Damage or ActionType.Heal or ActionType.Armor or ActionType.Draw or ActionType.Earthquake or ActionType.SummonDecoys => action.value > 0,
            ActionType.ModifyStat => action.value != 0,
            ActionType.ApplyStatus => action.statusPotency > 0,
            ActionType.PlaceSlotEffect => action.value > 0, // A Landmine's or a Checkpoint's damage
            _ => false
        };

        /// <summary>Raises the action's number by 1, if it has one. False: nothing to raise.</summary>
        public static bool Raise(EffectAction action) {
            if (!CanRaise(action)) return false;
            if (action.actionType == ActionType.ApplyStatus) action.statusPotency++;
            else if (action.actionType == ActionType.ModifyStat) action.value += action.value > 0 ? 1 : -1;
            else action.value++;
            return true;
        }

        public static bool CanRaiseAny(IEnumerable<CardEffect> effects) => effects?.SelectMany(e => e.actions).Any(CanRaise) == true;
    }
}
