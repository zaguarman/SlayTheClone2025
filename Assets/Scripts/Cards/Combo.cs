using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Combo (the user, 2026-10-16, from Slay the Spire's Finisher and Pokémon's Stored Power): a spell that grows with the
    /// others its player plays the same Day. When it resolves, its numbers go up by 1 (see <see cref="Numbers"/>) for each
    /// other spell of that player's that resolved today or is still waiting to: spells resolve in random order, so every
    /// spell of the Day counts, before or after. Engine and Combo decks get a payoff for a hand full of spells.
    /// </summary>
    public static class Combo {
        public static bool Has(ICard card) => card?.SourceData?.combo == true;

        /// <summary>The other spells its player plays today: those resolved, and those still in the queue.</summary>
        public static int OthersToday(ICard spell, IPlayer owner, IActionsQueue queue) {
            int waiting = queue?.GetPendingActions().Count(a => a switch {
                PlayCardAction play => play.GetOwner() == owner && play.GetCard() is Spell && play.GetCard() != spell,
                PlaySpellAction cast => cast.GetOwner() == owner && cast.GetSpell() != spell,
                _ => false
            }) ?? 0;
            return owner.SpellsToday + waiting;
        }

        /// <summary>As it resolves: its numbers go up by 1 for each other spell its player plays today.</summary>
        public static void Charge(Spell spell, IPlayer owner, IActionsQueue queue) {
            if (!Has(spell) || owner == null) return;
            int others = OthersToday(spell, owner, queue);
            for (int i = 0; i < others; i++) {
                foreach (var action in spell.Effects.SelectMany(e => e.actions)) Numbers.Raise(action);
            }
            if (others > 0) Log($"{spell.Name}: Combo +{others}", LogTag.Effects | LogTag.Cards);
        }
    }
}
