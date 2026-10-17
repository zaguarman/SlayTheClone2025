using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// The shuffled tournament (the user, 2026-10-17: "a tournament with all cards being shuffled, mix and match all factions,
    /// and find out what strategies have the most potential"): random decks drawn from the whole card pool, every card in at
    /// least two of them, half the decks from two factions and half from anywhere, played round robin by the Very Hard AI.
    /// The report ranks the cards by how the games they were played in ended, the roles (support, logistics, accelerators,
    /// removal, set-up...) by the same, the faction pairs, and what each faction's pool lacks (a part of AITournament).
    /// </summary>
    public static partial class AITournament {
        public const int ShuffledDecks = 40, ShuffledDeckSize = 30, ShuffledSeed = 20261017;

        /// <summary>Every card's numbers from the last shuffled run (Resources/ShuffledScores.json), for the deck proposals.</summary>
        [System.Serializable]
        public class ShuffledScores {
            [System.Serializable]
            public class Entry {
                public string card;
                public string faction;
                public bool creature;
                public float winRate; // Of the games its side played it in
                public int games;
                public float impact;  // A game it was played in
                public string roles;  // The roles it plays, comma-separated
            }
            public string writtenUtc;
            public int gamesPlayed;
            public List<Entry> cards = new List<Entry>();
        }

        public static ShuffledScores LastShuffledScores { get; private set; }

        /// <summary>The roles a card plays, read from what it does. A card can play several; a creature with nothing printed is a Body.</summary>
        public static IReadOnlyList<string> RolesOf(CardDataScriptableObject card) {
            var roles = new List<string>();
            var data = card.ToCardData();
            try {
                if (card is CreatureCardScriptableObject creature) {
                    if (creature.health >= 6) roles.Add("Wall (6+ Health)");
                    if (creature.attack >= 4) roles.Add("Beater (4+ Attack)");
                    if (creature.ability != CreatureAbility.None) roles.Add("Ability creature");
                    if (creature.turnsUntilOperative >= 2) roles.Add("Slow deploy (2+)");
                    if (creature.movement != MovementKeyword.None) roles.Add("Mobile (Leapfrog/Swap)");
                }
                if (card.prophecy > 0) roles.Add("Set-up (Prophecy)");
                foreach (var action in (data.effects ?? new List<CardEffect>()).SelectMany(e => e.actions)) {
                    bool enemies = action.targetType is TargetType.EnemyCreatures or TargetType.Enemy or TargetType.AllCreatures;
                    bool friends = action.targetType is TargetType.FriendlyCreatures or TargetType.Self or TargetType.Player;
                    switch (action.actionType) {
                        case ActionType.Draw or ActionType.SendFor or ActionType.Rush or ActionType.Revive or ActionType.Resurrect: roles.Add("Accelerator (draw, send for, rush)"); break;
                        case ActionType.Summon or ActionType.SummonDecoys: roles.Add("Swarm (summons)"); break;
                        case ActionType.Heal or ActionType.Armor or ActionType.Cleanse or ActionType.Grace: roles.Add("Support (heal, armor, cleanse)"); break;
                        case ActionType.ModifyStat when action.value > 0 && friends: roles.Add("Support (buffs)"); break;
                        case ActionType.ModifyStat when action.value > 0: roles.Add("Support (buffs)"); break;
                        case ActionType.ModifyStat: roles.Add("Debuff"); break;
                        case ActionType.Push or ActionType.Pull or ActionType.Shuffle or ActionType.Spin or ActionType.SwapPositions or ActionType.ReturnToHand or ActionType.Vanish or ActionType.FakeDeath:
                            roles.Add("Logistics (moves, returns)"); break;
                        case ActionType.Damage when action.targetType == TargetType.Enemy: roles.Add("Face damage"); break;
                        case ActionType.Damage when action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets): roles.Add("Board damage"); break;
                        case ActionType.Damage or ActionType.Destroy or ActionType.Attack: roles.Add("Removal"); break;
                        case ActionType.ApplyStatus or ActionType.Stun or ActionType.DelayDeployment or ActionType.Seal or ActionType.CutDraws or ActionType.PassHarmfulStatuses:
                            roles.Add(enemies ? "Debuff" : "Support (statuses on yours)"); break;
                        case ActionType.PlaceSlotEffect or ActionType.SetTrend or ActionType.CreateWatchdog or ActionType.SwapSlotEffects or ActionType.CleanseSlot:
                            roles.Add("Set-up (slots, Trends, Watchdogs)"); break;
                        case ActionType.Reveal or ActionType.WipeWatchdogs: roles.Add("Intel"); break;
                        case ActionType.CopyStats or ActionType.StealStatChanges or ActionType.TakeControl or ActionType.Mimic or ActionType.TradeStats or ActionType.SwapStats
                            or ActionType.CopyStatuses or ActionType.StealGoodStatuses or ActionType.Funnel or ActionType.Consolidate or ActionType.GrantAbility or ActionType.SwapAbilities
                            or ActionType.DoubleDown or ActionType.SplitPain or ActionType.Defect or ActionType.Equip:
                            roles.Add("Trickery (copies, steals, trades)"); break;
                        case ActionType.EndDay or ActionType.Earthquake: roles.Add("Disruption"); break;
                    }
                }
            }
            finally {
                UnityEngine.Object.DestroyImmediate(data);
            }
            if (roles.Count == 0) roles.Add(card is CreatureCardScriptableObject ? "Body (plain creature)" : "Other spell");
            return roles.Distinct().ToList();
        }

        /// <summary>Builds the shuffled decks and plays them; returns the report (see the class summary).</summary>
        public static string RunShuffled(IReadOnlyList<CardDataScriptableObject> pool, System.Action<float> progress = null,
            int deckCount = ShuffledDecks, int deckSize = ShuffledDeckSize) {
            var random = new System.Random(ShuffledSeed);
            // Fake faces (what a decoy shows the enemy) and tokens are never dealt
            var faces = new HashSet<CardDataScriptableObject>(pool.OfType<CreatureCardScriptableObject>().Where(c => c.disguise != null).Select(c => (CardDataScriptableObject)c.disguise));
            var usable = pool.Where(c => c != null && !(c is CreatureCardScriptableObject { token: true }) && !faces.Contains(c)).OrderBy(c => c.cardName).ToList();
            var creatures = usable.Where(c => c is CreatureCardScriptableObject).ToList();
            var spells = usable.Where(c => !(c is CreatureCardScriptableObject)).ToList();
            var factions = usable.Select(c => c.faction).Where(f => f != Faction.None).Distinct().OrderBy(f => f).ToList();

            // The decks: half from two factions (their own cards, half each), half from anywhere; 16 creatures and 14 spells,
            // two copies at most, every card of the pool in at least two decks
            var lists = new List<(string name, List<CardDataScriptableObject> cards, Faction? a, Faction? b)>();
            List<CardDataScriptableObject> Pick(IEnumerable<CardDataScriptableObject> from, int count, List<CardDataScriptableObject> into) {
                var candidates = from.ToList();
                for (int i = 0; i < count && candidates.Count > 0; i++) {
                    var card = candidates[random.Next(candidates.Count)];
                    into.Add(card);
                    if (into.Count(c => c == card) >= 2) candidates.Remove(card);
                }
                return into;
            }
            int creaturesEach = (int)System.Math.Round(deckSize * 0.53), spellsEach = deckSize - creaturesEach;
            for (int i = 0; i < deckCount; i++) {
                var cards = new List<CardDataScriptableObject>();
                if (i % 2 == 0 && factions.Count >= 2) {
                    var a = factions[random.Next(factions.Count)];
                    var b = factions.Where(f => f != a).ElementAt(random.Next(factions.Count - 1));
                    bool Of(CardDataScriptableObject c) => c.faction == a || c.faction == b || c.faction == Faction.None;
                    Pick(creatures.Where(Of), creaturesEach, cards);
                    Pick(spells.Where(Of), spellsEach, cards);
                    lists.Add(($"Shuffle {i + 1} ({CardTextFormatter.FactionName(a)} + {CardTextFormatter.FactionName(b)})", cards, a, b));
                }
                else {
                    Pick(creatures, creaturesEach, cards);
                    Pick(spells, spellsEach, cards);
                    lists.Add(($"Shuffle {i + 1} (anything)", cards, null, null));
                }
            }
            // Coverage: a card in fewer than two decks takes the place of the most-dealt card of its kind in a deck without it
            var dealt = usable.ToDictionary(c => c, c => lists.Count(l => l.cards.Contains(c)));
            foreach (var card in usable.Where(c => dealt[c] < 2).ToList()) {
                while (dealt[card] < 2) {
                    var deck = lists.Where(l => !l.cards.Contains(card)).OrderBy(_ => random.Next()).First();
                    bool creature = card is CreatureCardScriptableObject;
                    var swapped = deck.cards.Where(c => (c is CreatureCardScriptableObject) == creature && dealt[c] > 2).OrderByDescending(c => dealt[c]).FirstOrDefault()
                                  ?? deck.cards.First(c => (c is CreatureCardScriptableObject) == creature);
                    deck.cards[deck.cards.IndexOf(swapped)] = card;
                    dealt[swapped]--;
                    dealt[card]++;
                }
            }

            var audit = new GameAudit();
            var ledger = new CardLedger();
            var hard = new AIEntrant(AILevel.VeryHard);
            var decks = lists.Select(l => new DeckCards { Name = l.name, Cards = l.cards.Select(c => c.ToCardData()).ToList() }).ToList();
            Result[] results;
            try {
                int seed = 70000;
                var games = (from first in decks from second in decks where second != first select new Game(first, hard, second, hard, seed++)).ToList();
                results = PlayAll(games, audit, ledger, progress);
            }
            finally {
                Release(decks);
            }
            Problems = audit.Problems;
            var played = results.Where(r => r.Played).ToList();
            LastShuffledScores = new ShuffledScores { writtenUtc = System.DateTime.UtcNow.ToString("u"), gamesPlayed = played.Count };
            foreach (var card in usable) {
                var record = ledger.Cards.FirstOrDefault(r => r.Name == card.cardName);
                LastShuffledScores.cards.Add(new ShuffledScores.Entry {
                    card = card.cardName, faction = card.faction.ToString(), creature = card is CreatureCardScriptableObject,
                    winRate = record?.WinRate ?? 0f, games = record?.Games ?? 0, impact = record?.ImpactPerGame ?? 0f, roles = string.Join(", ", RolesOf(card))
                });
            }
            return ShuffledReport(lists.Select(l => (l.name, l.cards, l.a, l.b)).ToList(), usable, played, ledger) + "\n" + audit.Report();
        }

        private static string ShuffledReport(List<(string name, List<CardDataScriptableObject> cards, Faction? a, Faction? b)> lists,
            List<CardDataScriptableObject> usable, List<Result> results, CardLedger ledger) {
            var text = new StringBuilder();
            text.AppendLine("SHUFFLED TOURNAMENT (every card in play)");
            text.AppendLine($"Run {System.DateTime.Now:yyyy-MM-dd HH:mm} at commit {Commit()}: tag it study-{System.DateTime.Now:yyyy-MM-dd}.");
            text.AppendLine($"{results.Count} games: {lists.Count} random decks of {lists[0].cards.Count} cards drawn from the whole pool ({usable.Count} cards, each in at least two decks), " +
                            "half the decks from two factions, half from anywhere, every deck against every other in both seats, the Very Hard AI on both sides. " +
                            $"Called off after {MaxDays} Days. Decks this random don't play like built ones: read the numbers as potential, not as balance.");
            text.AppendLine();

            float Share(string deck) => DeckWinShare(results, deck);
            var roleOf = usable.ToDictionary(c => c.cardName, c => RolesOf(c));
            var byName = usable.ToDictionary(c => c.cardName, c => c);

            // 1. Decks
            text.AppendLine("1. THE DECKS, by win share, with their factions and what they're made of");
            foreach (var deck in lists.OrderByDescending(l => Share(l.name))) {
                var cardsOf = deck.cards;
                var factionsOf = cardsOf.Where(c => c.faction != Faction.None).GroupBy(c => c.faction).OrderByDescending(g => g.Count()).Take(3)
                    .Select(g => $"{CardTextFormatter.FactionName(g.Key)} {g.Count()}");
                var rolesOf = cardsOf.SelectMany(c => roleOf[c.cardName]).GroupBy(r => r).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{RoleShort(g.Key)} {g.Count()}");
                text.AppendLine($"  {deck.name,-52} {Share(deck.name),4:0%}   {string.Join(", ", factionsOf)};  {string.Join(", ", rolesOf)}");
            }
            text.AppendLine($"  Two-faction decks win {lists.Where(l => l.a != null).Average(l => Share(l.name)):0%} of their games on average, the anything-goes decks {lists.Where(l => l.a == null).Average(l => Share(l.name)):0%}.");
            text.AppendLine();

            // 2. Cards: the games they were played in. No mirror games here, so no edge: win share and impact
            var played = ledger.Cards.Where(c => c.Games >= 8).ToList();
            text.AppendLine("2. CARDS: how the games where their side played them ended (8+ games), and their impact a game");
            text.AppendLine("   Win%: games won by the side that played it. Impact: all it dealt, set up and supplied (see the balance report), a game it was played in.");
            void Table(string title, IEnumerable<CardLedger.Record> rows) {
                text.AppendLine(title);
                text.AppendLine($"    {"Card",-40} {"Faction",-15} {"Win%",5} {"Games",5} {"Impact/g",9} {"Plays/g",8}  Roles");
                foreach (var c in rows) {
                    text.AppendLine($"    {c.Name,-40} {CardTextFormatter.FactionName(c.Faction),-15} {c.WinRate,5:0%} {c.Games,5} {c.ImpactPerGame,9:0.0} {c.PlaysPerGame,8:0.0}  {(roleOf.TryGetValue(c.Name, out var r) ? string.Join(", ", r.Select(Short)) : "")}");
                }
                text.AppendLine();
            }
            Table("  Spells with the most potential (win share)", played.Where(c => !c.IsCreature).OrderByDescending(c => c.WinRate).ThenByDescending(c => c.ImpactPerGame).Take(20));
            Table("  Creatures with the most potential (win share)", played.Where(c => c.IsCreature).OrderByDescending(c => c.WinRate).ThenByDescending(c => c.ImpactPerGame).Take(20));
            Table("  The most impact a game, either kind", played.OrderByDescending(c => c.ImpactPerGame).Take(15));
            Table("  The least potential (the games they're played in are lost)", played.OrderBy(c => c.WinRate).ThenBy(c => c.ImpactPerGame).Take(20));
            var unplayed = usable.Where(c => !ledger.Cards.Any(r => r.Name == c.cardName && r.Plays > 0)).Select(c => c.cardName).OrderBy(n => n).ToList();
            text.AppendLine($"  Never played, dealt and all ({unplayed.Count}; the AI found no use for them, or they can't be played as dealt): {string.Join(", ", unplayed)}");
            text.AppendLine();

            // 3. Roles
            text.AppendLine("3. ROLES: the potential of each kind of card");
            text.AppendLine("   Cards: average win share of the cards playing the role (8+ games each). Decks: the win share of the decks richest in it (top third by count) against the poorest (bottom third).");
            var allRoles = roleOf.Values.SelectMany(r => r).Distinct().OrderBy(r => r).ToList();
            text.AppendLine($"    {"Role",-36} {"Cards",5} {"Win%",5} {"Impact/g",9}   {"Rich decks",10} {"Poor decks",10}");
            foreach (var role in allRoles) {
                var cards = played.Where(c => roleOf.TryGetValue(c.Name, out var r) && r.Contains(role)).ToList();
                var counts = lists.Select(l => (l.name, count: l.cards.Count(c => roleOf[c.cardName].Contains(role)))).OrderByDescending(x => x.count).ToList();
                int third = System.Math.Max(1, counts.Count / 3);
                float rich = counts.Take(third).Average(x => Share(x.name)), poor = counts.Skip(counts.Count - third).Average(x => Share(x.name));
                text.AppendLine($"    {role,-36} {cards.Count,5} {(cards.Count == 0 ? 0 : cards.Average(c => c.WinRate)),5:0%} {(cards.Count == 0 ? 0 : cards.Average(c => c.ImpactPerGame)),9:0.0}   {rich,10:0%} {poor,10:0%}");
            }
            text.AppendLine();

            // 4. Factions and pairs
            text.AppendLine("4. FACTIONS: the potential of each faction's cards, and of the pairs");
            text.AppendLine($"    {"Faction",-15} {"Cards",5} {"Played",6} {"Win%",5} {"Impact/g",9}   Strongest roles (its cards' win share)   Weakest");
            foreach (var faction in usable.Select(c => c.faction).Distinct().OrderBy(f => CardTextFormatter.FactionName(f))) {
                var mine = played.Where(c => c.Faction == faction).ToList();
                var roleShares = mine.SelectMany(c => roleOf[c.Name].Select(r => (r, c.WinRate))).GroupBy(x => x.r).Where(g => g.Count() >= 2)
                    .Select(g => (role: g.Key, share: g.Average(x => x.WinRate), n: g.Count())).OrderByDescending(x => x.share).ToList();
                string best = string.Join(", ", roleShares.Take(2).Select(x => $"{RoleShort(x.role)} {x.share:0%}"));
                string worst = string.Join(", ", roleShares.Skip(System.Math.Max(0, roleShares.Count - 2)).Select(x => $"{RoleShort(x.role)} {x.share:0%}"));
                text.AppendLine($"    {CardTextFormatter.FactionName(faction),-15} {usable.Count(c => c.faction == faction),5} {mine.Count,6} {(mine.Count == 0 ? 0 : mine.Average(c => c.WinRate)),5:0%} {(mine.Count == 0 ? 0 : mine.Average(c => c.ImpactPerGame)),9:0.0}   {best,-40} {worst}");
            }
            text.AppendLine();
            text.AppendLine("  Pairs: the two-faction decks' win share (one deck each; a hint, not a verdict)");
            foreach (var deck in lists.Where(l => l.a != null).OrderByDescending(l => Share(l.name))) {
                text.AppendLine($"    {CardTextFormatter.FactionName(deck.a.Value) + " + " + CardTextFormatter.FactionName(deck.b.Value),-34} {Share(deck.name),4:0%}");
            }
            text.AppendLine();

            // 5. What each faction's pool holds, by role, against the pool's average: what it lacks
            text.AppendLine("5. WHAT EACH FACTION LACKS: its share of each role against the whole pool's (- below, + above, in points)");
            var coreRoles = new[] { "Accelerator (draw, send for, rush)", "Support (heal, armor, cleanse)", "Support (buffs)", "Logistics (moves, returns)", "Removal", "Board damage",
                "Face damage", "Debuff", "Set-up (slots, Trends, Watchdogs)", "Set-up (Prophecy)", "Intel", "Swarm (summons)", "Trickery (copies, steals, trades)", "Wall (6+ Health)", "Beater (4+ Attack)", "Ability creature" };
            float PoolShare(IEnumerable<CardDataScriptableObject> cards, string role) {
                var list = cards.ToList();
                return list.Count == 0 ? 0 : list.Count(c => roleOf[c.cardName].Contains(role)) / (float)list.Count;
            }
            text.AppendLine($"    {"Role",-36} " + string.Join(" ", usable.Select(c => c.faction).Where(f => f != Faction.None).Distinct().OrderBy(f => CardTextFormatter.FactionName(f)).Select(f => $"{Abbrev(f),6}")));
            var factionList = usable.Select(c => c.faction).Where(f => f != Faction.None).Distinct().OrderBy(f => CardTextFormatter.FactionName(f)).ToList();
            foreach (var role in coreRoles) {
                float all = PoolShare(usable, role);
                text.AppendLine($"    {role,-36} " + string.Join(" ", factionList.Select(f => $"{(PoolShare(usable.Where(c => c.faction == f), role) - all) * 100,+6:0;-0;0}")));
            }
            text.AppendLine("    (Abbreviations: " + string.Join(", ", factionList.Select(f => $"{Abbrev(f)} {CardTextFormatter.FactionName(f)}")) + ")");
            text.AppendLine();
            return text.ToString();
        }

        private static string RoleShort(string role) => role.Contains(" (") ? role.Substring(0, role.IndexOf(" (")) : role;

        private static string Abbrev(Faction faction) => faction switch {
            Faction.Online => "Online", Faction.Offline => "Offline", Faction.ShadowOps => "Shadow", Faction.Statecraft => "State", Faction.Corporate => "Corp",
            _ => CardTextFormatter.FactionName(faction).Length <= 6 ? CardTextFormatter.FactionName(faction) : CardTextFormatter.FactionName(faction).Substring(0, 6)
        };
    }
}
