using System;
using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The decks of an adventure (the user, 2026-10-15). You start, as in Slay the Spire, with a small plain deck of your
    /// faction: simple creatures and simple spells, no big strategy. The enemies get worse cards early and better ones as
    /// you climb: the first fights are made of Tier C and D cards, and many of them are clumsy on top (too many cards to
    /// find a plan, too many or too few creatures, a little of everything); later decks keep their best cards in several
    /// copies and the right mix. After each fight won you may take one of three cards from the deck you beat.
    /// A card's tier comes from a rough power score read off the card (its stats, effects and costs), ranked against every
    /// card on offer of its kind (creatures and spells apart): S the top tenth, then A, B, C, and D the bottom fifth.
    /// </summary>
    public static class AdventureDecks {
        public enum Tier { D, C, B, A, S }

        public const int StarterSize = 12;
        public const int RewardChoices = 3;
        private const int BloatedMost = 40; // A clumsy enemy deck: 30 to 40 cards

        // Share of the ranked cards in each tier, from the bottom: D 20%, C 25%, B 25%, A 20%, S 10%
        private static readonly float[] TierTops = { 0.2f, 0.45f, 0.7f, 0.9f, 1f };

        #region Power and tiers
        /// <summary>A rough worth of the card: a creature's body and effects, a spell's effects, less its costs.</summary>
        public static float Power(CardDataScriptableObject card) {
            if (card == null) return 0f;
            var data = card.ToCardData();
            float power = 0f;
            if (card is CreatureCardScriptableObject creature) {
                power += creature.attack * 1.0f + creature.health * 0.7f + (creature.speed - 1) * 0.8f - creature.turnsUntilOperative * 0.8f;
                if (creature.ability != CreatureAbility.None) power += 1.5f;
                if (creature.movement != MovementKeyword.None) power += 0.5f;
            }
            foreach (var effect in data.effects ?? new List<CardEffect>()) {
                float sum = effect.actions.Sum(ActionPower);
                // A creature's effect that repeats (each Dawn, each Dusk) is worth more than one that happens once
                if (card is CreatureCardScriptableObject && effect.trigger is EffectTrigger.StartOfTurn or EffectTrigger.EndOfTurn) sum *= 1.6f;
                power += data.gamble ? sum / Math.Max(1, data.effects.Count) : sum;
            }
            power -= card.discardCost * 1.2f + card.sacrifice * 2.5f + card.doomCost * 0.5f + (card.singleUse ? 1f : 0f);
            return power;
        }

        private static float ActionPower(EffectAction action) {
            bool area = action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets);
            bool enemy = action.targetType is TargetType.Enemy or TargetType.EnemyCreatures;
            float spread = area ? 2.2f : 1f;
            return action.actionType switch {
                ActionType.Damage => action.value * spread * (enemy || action.targetType == TargetType.AllCreatures ? 1f : -0.6f),
                ActionType.Destroy => 5f,
                ActionType.Heal => action.value * 0.5f * spread,
                ActionType.Armor => action.value * 0.7f * spread,
                ActionType.Draw => Math.Max(1, action.value) * 1.5f,
                ActionType.ModifyStat => Math.Abs(action.value) * ((action.modifyAttack ? 1f : 0f) + (action.modifyHealth ? 0.7f : 0f) + (action.modifySpeed ? 0.8f : 0f)) * spread,
                ActionType.Stun => 2.5f,
                ActionType.TakeControl => 5f,
                ActionType.Summon or ActionType.SummonDecoys => 3f,
                ActionType.CreateWatchdog => 2.5f * Math.Max(1, action.watchdog.charges),
                _ => 2f
            };
        }

        /// <summary>
        /// Every card's tier, ranked by power against the others of the pool of its kind: creatures and spells apart, as a
        /// creature's body would otherwise lift every creature above every spell.
        /// </summary>
        public static Dictionary<CardDataScriptableObject, Tier> Tiers(IEnumerable<CardDataScriptableObject> pool) {
            var tiers = new Dictionary<CardDataScriptableObject, Tier>();
            foreach (var kind in pool.Where(c => c != null).Distinct().GroupBy(c => c is CreatureCardScriptableObject)) {
                var ranked = kind.OrderBy(Power).ThenBy(c => c.cardName).ToList();
                for (int i = 0; i < ranked.Count; i++) {
                    float share = (i + 0.5f) / ranked.Count;
                    int tier = 0;
                    while (tier < TierTops.Length - 1 && share > TierTops[tier]) tier++;
                    tiers[ranked[i]] = (Tier)tier;
                }
            }
            return tiers;
        }
        #endregion

        #region Your starter deck
        // How much there is to a card to learn: abilities, extra effects, costs and twists
        private static int Complexity(CardDataScriptableObject card) {
            var actions = card.ToCardData().effects?.SelectMany(e => e.actions).ToList() ?? new List<EffectAction>();
            int score = actions.Count + card.discardCost + card.sacrifice + card.doomCost + card.prophecy + (card.gamble ? 2 : 0) + (card.singleUse ? 1 : 0);
            score += actions.Count(a => a.targetModifier != TargetModifier.None || a.requires != TargetRequisite.None);
            score += actions.Count(a => a.actionType is not (ActionType.Damage or ActionType.Heal or ActionType.Armor or ActionType.ModifyStat or ActionType.Draw));
            if (card is CreatureCardScriptableObject creature) {
                score += (creature.ability != CreatureAbility.None ? 2 : 0) + (creature.movement != MovementKeyword.None ? 1 : 0)
                    + (creature.gift || creature.disguise != null ? 2 : 0) + (creature.turnsUntilOperative > 1 ? 1 : 0);
            }
            else {
                score = Math.Max(0, score - 1); // A spell's one effect is the spell
            }
            return score;
        }

        private static bool Helps(CardDataScriptableObject spell) =>
            spell.ToCardData().effects?.SelectMany(e => e.actions).All(a => a.targetType is TargetType.FriendlyCreatures or TargetType.Player or TargetType.Self) == true;

        private static bool Hits(CardDataScriptableObject spell) =>
            spell.ToCardData().effects?.SelectMany(e => e.actions).Any(a => a.actionType == ActionType.Damage && a.targetType is TargetType.Enemy or TargetType.EnemyCreatures) == true;

        /// <summary>
        /// Your first deck, Slay the Spire style: 12 plain cards of your faction, two simple creatures (4 and 3 copies) and
        /// two simple spells, one that hits (3 copies) and one that helps (2). The plainest the faction has, of middling strength.
        /// </summary>
        public static DeckScriptableObject Starter(Faction faction, IEnumerable<CardDataScriptableObject> pool) {
            var cards = pool.Where(c => c != null && c.faction == faction).Distinct().ToList();
            var tiers = Tiers(pool);
            float Middling(CardDataScriptableObject c) => Math.Abs((int)tiers[c] - (int)Tier.C);
            // Nothing of Tier A or S in a starter (a text-less 6/6 is plain but no starter card), when the faction has enough below
            bool Modest(CardDataScriptableObject c) => tiers[c] <= Tier.B;
            var plainCreatures = cards.OfType<CreatureCardScriptableObject>().Where(c => !c.token).Cast<CardDataScriptableObject>().ToList();
            if (plainCreatures.Count(Modest) >= 2) plainCreatures = plainCreatures.Where(Modest).ToList();
            var creatures = plainCreatures.OrderBy(Complexity).ThenBy(Middling).ThenBy(c => c.cardName).Take(2).ToList();
            var spells = cards.Where(c => c is not CreatureCardScriptableObject).OrderBy(c => Modest(c) ? 0 : 1).ThenBy(Complexity).ThenBy(Middling).ThenBy(c => c.cardName).ToList();
            var strike = spells.FirstOrDefault(Hits);
            // The one that helps helps your side: aimed at your creatures or you (not a curse on theirs, like Memento Mori)
            var defend = spells.FirstOrDefault(s => s != strike && Helps(s)) ?? spells.FirstOrDefault(s => s != strike && !Hits(s));

            var deck = new List<CardDataScriptableObject>();
            if (creatures.Count > 0) deck.AddRange(Enumerable.Repeat(creatures[0], 4));
            if (creatures.Count > 1) deck.AddRange(Enumerable.Repeat(creatures[1], 3));
            if (strike != null) deck.AddRange(Enumerable.Repeat(strike, 3));
            if (defend != null) deck.AddRange(Enumerable.Repeat(defend, 2));
            // A faction short of plain cards fills up with its plainest others
            foreach (var extra in cards.OrderBy(Complexity).ThenBy(c => c.cardName)) {
                if (deck.Count >= StarterSize) break;
                if (!deck.Contains(extra)) deck.Add(extra);
            }
            while (deck.Count < StarterSize && deck.Count > 0) deck.Add(deck[deck.Count % Math.Min(deck.Count, 4)]);
            return DeckScriptableObject.Make($"{CardTextFormatter.FactionName(faction)} Starter", deck.Take(StarterSize));
        }
        #endregion

        #region The enemies' decks
        /// <summary>
        /// How strong a fight room's deck is, 0 to 1: the row it's on (the first fights the weakest), more for an elite,
        /// the most for the boss.
        /// </summary>
        public static float StrengthOf(MapNode room) => room.Type switch {
            RoomType.Boss => 1f,
            RoomType.Elite => Math.Min(0.95f, 0.3f + room.Row / (float)(AdventureMap.Rows - 1)),
            _ => room.Row / (float)(AdventureMap.Rows - 1) * 0.85f
        };

        /// <summary>
        /// A deck of this faction as strong as asked (0: Tier C/D cards, badly put together; 1: Tier A/S, tight). Weak decks
        /// are clumsy as well as weak: some are bloated (30 to 40 cards, every card once, nothing to build momentum on), and
        /// their mix of creatures and spells is off. Strong ones carry their best cards in several copies and about 3
        /// creatures to 2 spells. At most 40 cards; copies aren't limited.
        /// </summary>
        public static DeckScriptableObject Enemy(Faction faction, float strength, IEnumerable<CardDataScriptableObject> pool, Random random, string name) {
            var tiers = Tiers(pool);
            var cards = tiers.Keys.Where(c => c.faction == faction).ToList();
            var creatures = cards.Where(c => c is CreatureCardScriptableObject).ToList();
            var spells = cards.Where(c => c is not CreatureCardScriptableObject).ToList();
            float clumsy = 1f - strength;

            bool bloated = clumsy > 0.5f && random.NextDouble() < 0.4;
            int size = bloated ? random.Next(30, BloatedMost + 1) : random.Next(18, 23);
            // About 3 creatures to 2 spells when well built; off by up to a third either way when clumsy
            float creatureShare = 0.6f + (float)(random.NextDouble() * 2 - 1) * 0.35f * clumsy;
            if (clumsy > 0.5f && random.NextDouble() < 0.5) creatureShare = random.NextDouble() < 0.5 ? 0.25f + 0.1f * (float)random.NextDouble() : 0.85f + 0.1f * (float)random.NextDouble();
            int creatureCount = Math.Max(2, Math.Min(size - 1, (int)Math.Round(size * creatureShare)));
            if (spells.Count == 0) creatureCount = size;
            if (creatures.Count == 0) creatureCount = 0;

            // How many different cards: the clumsy ones a little of everything, the strong ones their best few, several times each
            float variety = bloated ? 0.85f : 0.7f - 0.35f * strength;
            float target = 0.3f + strength * 3.6f; // The tier aimed at: C/D first, A/S at the top
            var deck = new List<CardDataScriptableObject>();
            deck.AddRange(Pick(creatures, creatureCount, variety, target, tiers, random));
            deck.AddRange(Pick(spells, size - creatureCount, variety, target, tiers, random));
            return DeckScriptableObject.Make(name, deck.OrderBy(c => c is CreatureCardScriptableObject ? 0 : 1).ThenBy(c => c.cardName).Take(PlayerDecks.MaxDeckSize));
        }

        // Count cards of the given kind: a set of different ones near the tier aimed at, then copies of them round and round
        private static List<CardDataScriptableObject> Pick(List<CardDataScriptableObject> from, int count, float variety, float target,
            Dictionary<CardDataScriptableObject, Tier> tiers, Random random) {
            var picked = new List<CardDataScriptableObject>();
            if (count <= 0 || from.Count == 0) return picked;
            // Only cards within about a tier of the one aimed at; short of them, fewer different cards and more copies
            var near = from.Where(c => Math.Abs((int)tiers[c] - target) <= 1.2f).ToList();
            if (near.Count == 0) near = from.OrderBy(c => Math.Abs((int)tiers[c] - target)).Take(1).ToList();
            int different = Math.Max(1, Math.Min(near.Count, (int)Math.Round(count * variety)));
            var chosen = near.OrderBy(c => Math.Abs((int)tiers[c] - target) + random.NextDouble() * 1.2).Take(different).ToList();
            for (int i = 0; picked.Count < count; i++) picked.Add(chosen[i % chosen.Count]);
            return picked;
        }

        /// <summary>The name of a fight room's enemy: green at first, then regulars, an elite, the boss.</summary>
        public static string EnemyName(Faction faction, MapNode room) {
            string who = CardTextFormatter.FactionName(faction);
            return room.Type switch {
                RoomType.Boss => $"{who} Mastermind",
                RoomType.Elite => $"{who} Elite",
                _ => room.Row < 4 ? $"{who} Rookies" : room.Row < 9 ? $"{who} Regulars" : $"{who} Veterans"
            };
        }
        #endregion

        /// <summary>After a fight won: three different cards of your own faction (the user, 2026-10-16: only your deck's faction drops), to take one of or none.</summary>
        public static List<CardDataScriptableObject> Reward(Faction faction, float strength, IEnumerable<CardDataScriptableObject> pool, Random random,
            ICollection<CardDataScriptableObject> skip = null, int choices = RewardChoices) => Roll(faction, strength, pool, random, choices, skip);

        // The odds of each tier, D to S, at the bottom of the map and at the top (Slay the Spire's rarities, the user: random,
        // a high tier unlikely early): in between they blend by the room's strength
        private static readonly float[] OddsEarly = { 35, 38, 20, 6, 1 }, OddsLate = { 10, 20, 32, 24, 14 };

        /// <summary>
        /// Different cards of the faction, each one's tier rolled by the odds for the room's strength (0 the bottom, 1 the boss),
        /// then a card of that tier picked at random (the nearest tier that has one left, if it hasn't).
        /// </summary>
        public static List<CardDataScriptableObject> Roll(Faction faction, float strength, IEnumerable<CardDataScriptableObject> pool, Random random, int count,
            ICollection<CardDataScriptableObject> skip = null) {
            var cards = (pool ?? Enumerable.Empty<CardDataScriptableObject>()).Where(c => c != null && !(c is CreatureCardScriptableObject { token: true })).Distinct().ToList();
            var tiers = Tiers(cards);
            var left = cards.Where(c => c.faction == faction && (skip == null || !skip.Contains(c))).ToList();
            var picked = new List<CardDataScriptableObject>();
            float t = Math.Clamp(strength, 0f, 1f);
            var odds = OddsEarly.Select((early, i) => early + (OddsLate[i] - early) * t).ToArray();
            while (picked.Count < count && left.Count > 0) {
                double roll = random.NextDouble() * odds.Sum();
                int tier = 0;
                for (; tier < odds.Length - 1 && roll >= odds[tier]; tier++) roll -= odds[tier];
                var card = left.OrderBy(c => Math.Abs((int)tiers[c] - tier)).ThenBy(_ => random.Next()).First();
                picked.Add(card);
                left.Remove(card);
            }
            return picked;
        }
    }
}
