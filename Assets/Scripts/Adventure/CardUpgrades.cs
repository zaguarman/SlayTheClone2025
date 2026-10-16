using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Card upgrades (Slay the Spire's, the user 2026-10-16): at a rest site (and some events) a card of your deck is swapped
    /// for its "+" version, one better number. A creature gets +1 Attack and +1 Health; a spell, 1 more on each of its
    /// numbers (see <see cref="Numbers"/>). A card is upgraded once. The upgraded copy lives only in the run: it's made from the
    /// card asset when needed, never saved.
    /// </summary>
    public static class CardUpgrades {
        public const string Mark = "+";

        public static bool IsUpgraded(CardDataScriptableObject card) => card != null && card.upgraded;

        /// <summary>Whether it has a "+" version: not one already, and a spell needs a number to raise.</summary>
        public static bool CanUpgrade(CardDataScriptableObject card) => card != null && !card.upgraded
            && (card is CreatureCardScriptableObject || Numbers.CanRaiseAny(card.ToCardData().effects));

        /// <summary>The card's "+" version (a new runtime copy), or the card itself if it has none.</summary>
        public static CardDataScriptableObject Upgrade(CardDataScriptableObject card) {
            if (!CanUpgrade(card)) return card;
            var upgraded = Object.Instantiate(card);
            upgraded.name = card.name + Mark;
            upgraded.cardName = card.cardName + Mark;
            upgraded.upgraded = true;
            if (upgraded is CreatureCardScriptableObject creature) {
                creature.attack += 1;
                creature.health += 1;
            }
            else {
                var effects = card.ToCardData().effects;
                foreach (var action in effects.SelectMany(e => e.actions)) Numbers.Raise(action);
                upgraded.ReplaceEffects(effects);
            }
            return upgraded;
        }

        /// <summary>What the upgrade does, in a few words, for the button.</summary>
        public static string Describe(CardDataScriptableObject card) =>
            card is CreatureCardScriptableObject ? "+1 Attack and +1 Health" : "1 more on each of its numbers";

        /// <summary>The cards of the deck that can be upgraded, each once.</summary>
        public static List<CardDataScriptableObject> Upgradable(IEnumerable<CardDataScriptableObject> deck) =>
            deck.Where(CanUpgrade).Distinct().ToList();
    }
}
