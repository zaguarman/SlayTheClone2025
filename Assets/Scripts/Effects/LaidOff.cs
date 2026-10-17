using System.Linq;
using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// Laid Off (the user, 2026-10-16, from Slay the Spire's discard cards): what a card does when it's given up from the
    /// hand, its <see cref="EffectTrigger.OnDiscarded"/> effects. Creatures leave the hand at every Dawn now (only 3 come a
    /// Day), and discard costs give cards up too: this turns both into a plan. It's its owner's effect, with no creature in
    /// play behind it: random targets are picked as a spell's are, and "itself" is nothing.
    /// </summary>
    public static class LaidOff {
        public static bool Has(ICard card) => card?.Effects.Any(e => e.trigger == EffectTrigger.OnDiscarded) == true;

        /// <summary>Queues what the card does now that it's been discarded from <paramref name="owner"/>'s hand.</summary>
        public static void Queue(ICard card, IPlayer owner, IActionsQueue queue) {
            if (!Has(card) || owner == null || queue == null) return;
            Log($"{card.Name} is laid off from {owner.Name}'s hand", LogTag.Cards | LogTag.Effects);
            EffectResolver.QueueCardEffects(card, owner, EffectTrigger.OnDiscarded, queue);
        }
    }
}
