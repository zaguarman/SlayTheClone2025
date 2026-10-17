using System.Collections.Generic;
using System.Linq;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// The four factions and their subfactions (the user, 2026-10-17): as Slay the Spire's Ironclad is Strength, Block and
    /// Exhaust at once, each faction here is two or three of the old factions, now its subfactions, mixed freely in a deck.
    /// A deck is legal with one or two factions, whatever their subfactions. Establishment: the old powers, throne, altar
    /// and dagger (Statecraft, Myth, Shadow Ops.). Capital: money and the machines it buys (Corporate, Tech). Enlightenment:
    /// the mind and the future it designs (Utopia, Mind). Digital Society: the culture, online and off (Online, Mainstream:
    /// the old Zeitgeist half, its name to be changed).
    /// </summary>
    public static class Umbrellas {
        public static readonly IReadOnlyList<Umbrella> All = new[] { Umbrella.Establishment, Umbrella.Capital, Umbrella.Enlightenment, Umbrella.DigitalSociety };

        public static Umbrella Of(Faction faction) => faction switch {
            Faction.Statecraft or Faction.Myth or Faction.ShadowOps => Umbrella.Establishment,
            Faction.Corporate or Faction.Tech => Umbrella.Capital,
            Faction.Utopia or Faction.Mind => Umbrella.Enlightenment,
            Faction.Online or Faction.Mainstream or Faction.PopCulture => Umbrella.DigitalSociety,
            _ => Umbrella.None
        };

        /// <summary>Its subfactions, in the order the menus list them.</summary>
        public static IReadOnlyList<Faction> Subfactions(Umbrella umbrella) => umbrella switch {
            Umbrella.Establishment => new[] { Faction.Statecraft, Faction.Myth, Faction.ShadowOps },
            Umbrella.Capital => new[] { Faction.Corporate, Faction.Tech },
            Umbrella.Enlightenment => new[] { Faction.Utopia, Faction.Mind },
            Umbrella.DigitalSociety => new[] { Faction.Online, Faction.Mainstream },
            _ => new Faction[0]
        };

        /// <summary>Where a subfaction stands in the menus: by faction, then in its faction's order; Neutral last.</summary>
        public static int Order(Faction faction) {
            var umbrella = Of(faction);
            int at = Subfactions(umbrella).ToList().IndexOf(faction);
            return umbrella == Umbrella.None || at < 0 ? 99 : All.ToList().IndexOf(umbrella) * 10 + at;
        }

        public static string Name(Umbrella umbrella) => umbrella switch {
            Umbrella.Establishment => "Establishment",
            Umbrella.Capital => "Capital",
            Umbrella.Enlightenment => "Enlightenment",
            Umbrella.DigitalSociety => "Digital Society",
            _ => "Neutral"
        };

        /// <summary>The factions of these cards, the one with the most cards first.</summary>
        public static List<Umbrella> Of(IEnumerable<CardDataScriptableObject> cards) =>
            cards.Where(c => c != null && c.faction != Faction.None).GroupBy(c => Of(c.faction)).OrderByDescending(g => g.Count())
                .ThenBy(g => Name(g.Key)).Select(g => g.Key).ToList();
    }
}
