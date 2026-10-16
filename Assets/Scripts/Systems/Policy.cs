using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Policy (the user, 2026-10-16, from Slay the Spire's Powers): a spell that becomes a rule of your side. It happens when
    /// played, then stays in force: at each Dawn after, it happens again, for the rest of the combat. It never goes to the
    /// discard pile: it's in force, listed in the queue view. Its targets are its own (a side, the weakest, at random): a
    /// Policy is never aimed.
    /// </summary>
    public static class Policy {
        public static bool Has(ICard card) => card?.SourceData?.policy == true;

        /// <summary>Played and resolved: it's in force from now on.</summary>
        public static void Enact(Spell spell, IPlayer owner) {
            owner.RemoveFromHand(spell);
            owner.Policies.Add(spell);
            Log($"{owner.Name}'s {spell.Name} is now policy: it happens again each Dawn", LogTag.Cards | LogTag.Effects);
        }

        /// <summary>At Dawn, after the prophecies: every Policy in force happens again.</summary>
        public static void ProcessDawn(IEnumerable<IPlayer> players, IActionsQueue queue) {
            foreach (var player in players) {
                foreach (var spell in player.Policies.OfType<Spell>().ToList()) {
                    Log($"{player.Name}'s policy: {spell.Name}", LogTag.Effects | LogTag.Turns);
                    EffectResolver.QueueSpellEffects(spell, player, null, queue);
                }
            }
        }
    }
}
