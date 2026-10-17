using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

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

        /// <summary>
        /// The combo glow (the user, 2026-10-17: like Slay the Spire's cards that glow when their bonus is on): a card in your
        /// hand lights up softly once what it was made to follow has been played from your hand today. Only real payoffs glow (the
        /// second card clearly better, or only working, after the first), and only cards played count, not what's already in
        /// play (the user). Its intended combos are the designers' (the cards that set it up, named on it), plus two the rules
        /// give: a Combo card once another spell of yours is queued today, and an On Trend creature once you've queued a Trend.
        /// Later, maybe: a glow when a target condition is met (any enemy Revealed for Perfectly Timed Camera Malfunction).
        /// </summary>
        public static bool IsSetUp(ICard card, IPlayer owner, IActionsQueue queue) {
            var data = card?.SourceData;
            if (data == null || owner == null) return false;
            var queued = queue?.GetPendingActions().OfType<PlayCardAction>().Where(p => p.GetOwner() == owner && p.GetCard() != card)
                .Select(p => p.GetCard()).Where(c => c != null).ToList() ?? new List<ICard>();
            if (data.setUpBy is { Length: > 0 } && queued.Any(c => data.setUpBy.Contains(c.Name))) return true;
            if (data.combo && queued.Any(c => c is Spell)) return true;
            return data is CreatureData { ability: CreatureAbility.OnTrend } && queued.OfType<Spell>().Any(EffectResolver.IsTrend);
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
