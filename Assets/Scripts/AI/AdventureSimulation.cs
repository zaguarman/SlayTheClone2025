using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// Adventures played by the AI (the user, 2026-10-16: "run adventures with different factions and tell me which have more
    /// problems"). Each run starts with its faction's starter deck and 60 Health and walks the map at random along the paths;
    /// its fights are played by the real rules, the player's seat by the AI standing in for you, the enemy as the room sets
    /// it (deck, AI level, Health). Won, it takes the strongest card of its rewards (your faction's), and the Health left goes
    /// on; at a shop it buys the strongest card it can pay and a potion, then has its weakest taken out. At a rest site it
    /// sleeps below 40 Health and trains (upgrades its strongest card) above; a treasure room gives a relic, as an elite
    /// does; potions drop and go into its opening hands. Events are walked through: the simulation can't make their choices.
    /// Every run of a round is prepared on the main thread (decks are Unity assets), then their fights are played together on
    /// worker threads.
    /// </summary>
    public static partial class AITournament {
        private sealed class AdventureSim {
            public Faction Faction;
            public int Index;
            public System.Random Random;
            public AdventureMap Map;
            public MapNode Current, Fighting;
            public List<CardDataScriptableObject> Deck;
            public int Health = AdventureRun.StartingHealth, Money, FightsWon, Removals, Bought, Rests, Upgrades, PotionsUsed;
            public readonly List<Relic> Relics = new List<Relic>();
            public Faction BossFaction;
            public readonly List<Relic> EnemyRelics = new List<Relic>();
            public readonly List<CardDataScriptableObject> Potions = new List<CardDataScriptableObject>();
            public bool Over, Won, Stalled;
            public Faction FightingFaction, KilledBy;
            public DeckScriptableObject EnemyDeck;
            public readonly List<int> HealthAfterFights = new List<int>();
        }

        /// <summary>Plays <paramref name="runsPerFaction"/> adventures for each faction; returns the report.</summary>
        public static string RunAdventures(IReadOnlyList<Faction> factions, IReadOnlyList<CardDataScriptableObject> pool, int runsPerFaction,
            AIEntrant player, System.Action<float> progress = null, IReadOnlyList<CardDataScriptableObject> potionPool = null) {
            var cards = pool.Where(c => c != null).Distinct().ToList();
            potions = (potionPool ?? new List<CardDataScriptableObject>()).Where(p => p != null).ToList();
            upgradedMade.Clear();
            var tiers = AdventureDecks.Tiers(cards);
            var sims = new List<AdventureSim>();
            foreach (var faction in factions) {
                for (int i = 0; i < runsPerFaction; i++) {
                    int seed = 7919 * (int)faction + 104729 * i + 17;
                    var sim = new AdventureSim {
                        Faction = faction, Index = i, Random = new System.Random(seed), Map = AdventureMap.Generate(seed),
                        Deck = AdventureDecks.Starter(faction, cards).Cards.ToList()
                    };
                    var rivals = factions.Where(f => f != faction).ToList();
                    sim.BossFaction = rivals[sim.Random.Next(rivals.Count)]; // Known from the start, as the map shows it
                    GainRelic(sim); // The opening choice: it takes the relic
                    sims.Add(sim);
                }
            }
            var audit = new GameAudit();
            int rounds = 0, fights = 0;
            while (sims.Any(s => !s.Over) && !StopRequested) {
                rounds++;
                var games = new List<Game>();
                var playing = new List<AdventureSim>();
                var read = new List<DeckCards>();
                foreach (var sim in sims.Where(s => !s.Over)) {
                    WalkToFight(sim, factions, cards, tiers);
                    if (sim.Over) continue;
                    var mine = new DeckCards { Name = $"{sim.Faction} run {sim.Index}", Cards = DeckScriptableObject.Make("Run", sim.Deck).GetCardDataList() };
                    var theirs = new DeckCards { Name = sim.EnemyDeck.GetDeckName(), Cards = sim.EnemyDeck.GetCardDataList() };
                    read.Add(mine);
                    read.Add(theirs);
                    int enemyHealth = AdventureRun.EnemyHealth(sim.Fighting.Type, sim.FightsWon);
                    var kit = new DeckCards { Name = "Potions", Cards = sim.Potions.Select(p => p.ToCardData()).ToList() };
                    read.Add(kit);
                    games.Add(new Game(mine, player, theirs, new AIEntrant(AdventureMenu.LevelFor(sim.Fighting, 0)), sim.Random.Next(), sim.Health, enemyHealth,
                        sim.Relics.ToArray(), kit.Cards, sim.EnemyRelics.ToArray()));
                    playing.Add(sim);
                }
                if (games.Count == 0) break;
                int before = fights;
                var results = PlayAll(games, audit, null, done => progress?.Invoke(Mathf.Clamp01((before + done * games.Count) / (float)(sims.Count * 8))), aligned: true);
                fights += results.Count(r => r.Played);
                for (int i = 0; i < results.Length; i++) {
                    if (results[i].Played) AfterFight(playing[i], results[i], cards);
                }
                Release(read);
            }
            progress?.Invoke(1f);
            if (StopRequested) { // Stopped early: only the runs that finished are reported
                foreach (var unfinished in sims.Where(s => !s.Over)) if (unfinished.EnemyDeck != null) Object.DestroyImmediate(unfinished.EnemyDeck);
                sims.RemoveAll(s => !s.Over);
            }
            string report = AdventureReport(sims, factions, runsPerFaction, player, fights, audit, cards);
            foreach (var made in upgradedMade) Object.DestroyImmediate(made);
            upgradedMade.Clear();
            return report;
        }

        private static List<CardDataScriptableObject> potions = new List<CardDataScriptableObject>();
        private static readonly List<CardDataScriptableObject> upgradedMade = new List<CardDataScriptableObject>();

        private static void GainRelic(AdventureSim sim) {
            var left = Relics.All.Where(r => !sim.Relics.Contains(r)).ToList();
            if (left.Count > 0) sim.Relics.Add(left[sim.Random.Next(left.Count)]);
        }

        // A rest site: sleep when hurt, otherwise train its strongest card that can still be upgraded
        private static void Rest(AdventureSim sim) {
            var best = sim.Deck.Where(CardUpgrades.CanUpgrade).OrderByDescending(AdventureDecks.Power).FirstOrDefault();
            if (sim.Health < 40 || best == null) {
                sim.Health += AdventureRun.RestHeal;
                sim.Rests++;
                return;
            }
            var upgraded = CardUpgrades.Upgrade(best);
            upgradedMade.Add(upgraded);
            sim.Deck[sim.Deck.IndexOf(best)] = upgraded;
            sim.Upgrades++;
        }

        private static int Price(AdventureSim sim, int price) => sim.Relics.Contains(Relic.CreditCard) ? Mathf.RoundToInt(price * Relics.CreditDiscount) : price;

        // Walks on (rooms at random along the paths) up to the next fight, which is set up: its enemy deck and faction. Shops
        // buy and remove; the other rooms do nothing here
        private static void WalkToFight(AdventureSim sim, IReadOnlyList<Faction> factions, List<CardDataScriptableObject> pool, Dictionary<CardDataScriptableObject, AdventureDecks.Tier> tiers) {
            while (true) {
                var options = sim.Current == null ? sim.Map.Row(0).ToList() : sim.Current.Next;
                if (options.Count == 0) {
                    sim.Over = true;
                    return;
                }
                var room = options[sim.Random.Next(options.Count)];
                if (room.IsCombat) {
                    sim.Fighting = room;
                    var rivals = factions.Where(f => f != sim.Faction).ToList();
                    sim.FightingFaction = room.Type == RoomType.Boss ? sim.BossFaction : rivals[sim.Random.Next(rivals.Count)];
                    // An elite carries a relic, the boss two (as the adventure does)
                    sim.EnemyRelics.Clear();
                    var perks = AdventureRun.EnemyPerks.ToList();
                    for (int p = 0; p < (room.Type == RoomType.Boss ? 2 : room.Type == RoomType.Elite ? 1 : 0); p++) {
                        var perk = perks[sim.Random.Next(perks.Count)];
                        perks.Remove(perk);
                        sim.EnemyRelics.Add(perk);
                    }
                    if (sim.EnemyDeck != null) Object.DestroyImmediate(sim.EnemyDeck);
                    sim.EnemyDeck = AdventureDecks.Enemy(sim.FightingFaction, AdventureDecks.StrengthOf(room), pool, sim.Random, AdventureDecks.EnemyName(sim.FightingFaction, room));
                    return;
                }
                if (room.Type == RoomType.Shop) Shop(sim, pool, tiers);
                if (room.Type == RoomType.Rest) Rest(sim);
                if (room.Type == RoomType.Treasure) {
                    GainRelic(sim);
                    sim.Money += AdventureRun.TreasureMoney;
                }
                sim.Current = room;
            }
        }

        // The shop's five cards of the run's faction: the strongest it can pay, then the weakest card out of the deck if it still can
        private static void Shop(AdventureSim sim, List<CardDataScriptableObject> pool, Dictionary<CardDataScriptableObject, AdventureDecks.Tier> tiers) {
            var stock = AdventureDecks.Roll(sim.Faction, AdventureDecks.StrengthOf(sim.Current ?? sim.Map.Row(0).First()), pool, sim.Random, AdventureRun.ShopCards);
            var buy = stock.Where(c => Price(sim, AdventureRun.PriceOf(tiers[c])) <= sim.Money).OrderByDescending(AdventureDecks.Power).FirstOrDefault();
            if (buy != null && sim.Deck.Count < PlayerDecks.MaxDeckSize) {
                sim.Money -= Price(sim, AdventureRun.PriceOf(tiers[buy]));
                sim.Deck.Add(buy);
                sim.Bought++;
            }
            if (potions.Count > 0 && sim.Potions.Count < AdventureRun.MaxPotions && sim.Money >= Price(sim, AdventureRun.PotionPrice)) {
                sim.Money -= Price(sim, AdventureRun.PotionPrice);
                sim.Potions.Add(potions[sim.Random.Next(potions.Count)]);
            }
            // A relic, when there's money left for it
            if (sim.Money >= Price(sim, AdventureRun.RelicPrice) && Relics.All.Any(r => !sim.Relics.Contains(r))) {
                sim.Money -= Price(sim, AdventureRun.RelicPrice);
                GainRelic(sim);
            }
            int removal = Price(sim, AdventureRun.RemovalPrice + AdventureRun.RemovalRise * sim.Removals);
            if (sim.Money >= removal && sim.Deck.Count > AdventureRun.MinDeck) {
                sim.Money -= removal;
                sim.Removals++;
                sim.Deck.Remove(sim.Deck.OrderBy(AdventureDecks.Power).First());
            }
        }

        private static void AfterFight(AdventureSim sim, Result result, List<CardDataScriptableObject> pool) {
            var room = sim.Fighting;
            sim.Fighting = null;
            if (result.Winner != 1) {
                sim.Over = true;
                sim.Stalled = result.Winner == 0;
                sim.KilledBy = sim.FightingFaction;
                sim.Current = room; // Where it ended
                return;
            }
            sim.Current = room;
            sim.FightsWon++;
            sim.Health = Mathf.Max(1, result.Health1) + (sim.Relics.Contains(Relic.FirstAidKit) ? Relics.FirstAidHeal : 0);
            sim.HealthAfterFights.Add(sim.Health);
            // The potions used are gone; the rest are kept
            var left = result.PotionsLeft1.ToList();
            var kept = sim.Potions.Where(p => left.Remove(p.cardName)).ToList();
            sim.PotionsUsed += sim.Potions.Count - kept.Count;
            sim.Potions.Clear();
            sim.Potions.AddRange(kept);
            if (room.Type == RoomType.Boss) {
                sim.Won = sim.Over = true;
                return;
            }
            sim.Money += (room.Type == RoomType.Elite ? AdventureRun.ElitePurse : AdventureRun.FightPurse) + (sim.Relics.Contains(Relic.DoubleHeadedCoin) ? Relics.CoinPay : 0);
            if (room.Type == RoomType.Elite) GainRelic(sim);
            if (potions.Count > 0 && sim.Random.Next(100) < AdventureRun.PotionDropPercent && sim.Potions.Count < AdventureRun.MaxPotions)
                sim.Potions.Add(potions[sim.Random.Next(potions.Count)]);
            var offered = new HashSet<CardDataScriptableObject>();
            int choices = sim.Relics.Contains(Relic.Backpack) ? AdventureDecks.RewardChoices + 1 : AdventureDecks.RewardChoices;
            for (int pack = 0; pack < (room.Type == RoomType.Elite ? 2 : 1); pack++) {
                var cards = AdventureDecks.Reward(sim.Faction, AdventureDecks.StrengthOf(room), pool, sim.Random, offered, choices);
                foreach (var card in cards) offered.Add(card);
                var best = cards.OrderByDescending(AdventureDecks.Power).FirstOrDefault();
                if (best != null && sim.Deck.Count < PlayerDecks.MaxDeckSize) sim.Deck.Add(best);
            }
        }

        private static string AdventureReport(List<AdventureSim> sims, IReadOnlyList<Faction> factions, int runs, AIEntrant player, int fights, GameAudit audit,
            List<CardDataScriptableObject> pool) {
            var text = new StringBuilder();
            text.AppendLine("ADVENTURE SIMULATION");
            text.AppendLine();
            text.AppendLine($"{runs} runs a faction, {fights} fights in all. Each run: the faction's starter deck, 60 Health, rooms picked at random along");
            text.AppendLine($"the paths. You are played by the {player.Name} AI; the enemies as the rooms set them (Easy early, Medium, Hard elites,");
            text.AppendLine("Very Hard boss; Health 5, 10, 15... and 60 for the boss). Won, a run takes the strongest card offered (its own faction's);");
            text.AppendLine("shops buy the strongest card it can pay and a potion, then take out the weakest. Rest sites: sleep (+20) below 40");
            text.AppendLine("Health, otherwise upgrade the strongest card. Elites and treasure rooms give a relic; fights drop potions (40%),");
            text.AppendLine("which join the opening hand. Events are walked through. The opening choice: a relic. Shops sell a relic too (bought with");
            text.AppendLine("what's left); elites carry a relic, the boss two, and the boss's faction is known from the start.");
            text.AppendLine("A fight still going after 40 Days counts as lost (stalled).");
            text.AppendLine();
            text.AppendLine("1. BY FACTION, the most trouble first (bosses beaten, then fights won)");
            text.AppendLine();
            text.AppendLine($"{"Faction",-18}{"Boss beaten",12}{"Fights won (avg)",18}{"Died: fight",13}{"elite",7}{"boss",6}{"stalled",9}{"Deck at end",13}");
            foreach (var group in sims.GroupBy(s => s.Faction).OrderBy(g => g.Count(s => s.Won)).ThenBy(g => g.Average(s => s.FightsWon))) {
                var lost = group.Where(s => !s.Won).ToList();
                text.AppendLine($"{CardTextFormatter.FactionName(group.Key),-18}{group.Count(s => s.Won),5} of {group.Count(),-4}{group.Average(s => s.FightsWon),14:0.0}    "
                    + $"{lost.Count(s => s.Current?.Type == RoomType.Fight),9}{lost.Count(s => s.Current?.Type == RoomType.Elite),7}{lost.Count(s => s.Current?.Type == RoomType.Boss),6}"
                    + $"{group.Count(s => s.Stalled),9}{group.Average(s => s.Deck.Count),13:0.0}");
            }
            text.AppendLine();
            text.AppendLine("2. WHERE AND TO WHOM EACH FACTION LOSES");
            foreach (var group in sims.GroupBy(s => s.Faction).OrderBy(g => g.Count(s => s.Won)).ThenBy(g => g.Average(s => s.FightsWon))) {
                var lost = group.Where(s => !s.Won).ToList();
                text.AppendLine();
                text.AppendLine($"{CardTextFormatter.FactionName(group.Key)}");
                if (lost.Count == 0) {
                    text.AppendLine("    Never lost.");
                    continue;
                }
                string Band(MapNode n) => n == null ? "?" : n.Type == RoomType.Boss ? "the boss" : n.Row < 5 ? "rows 1-5" : n.Row < 10 ? "rows 6-10" : "rows 11-15";
                text.AppendLine("    Died in: " + string.Join(", ", lost.GroupBy(s => Band(s.Current)).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
                text.AppendLine("    Beaten by: " + string.Join(", ", lost.GroupBy(s => s.KilledBy).OrderByDescending(g => g.Count()).Select(g => $"{CardTextFormatter.FactionName(g.Key)} {g.Count()}")));
                var afterFirst = group.Where(s => s.HealthAfterFights.Count > 0).Select(s => s.HealthAfterFights[0]).ToList();
                if (afterFirst.Count > 0) text.AppendLine($"    Health after its first fight: {afterFirst.Average():0} on average (of 60); cards bought at shops: {group.Sum(s => s.Bought)}, removed: {group.Sum(s => s.Removals)}");
                text.AppendLine($"    Rests: {group.Sum(s => s.Rests)} slept, {group.Sum(s => s.Upgrades)} cards upgraded; relics {group.Average(s => s.Relics.Count):0.0} a run; potions used {group.Sum(s => s.PotionsUsed)}");
            }
            text.AppendLine();
            text.AppendLine("3. STARTER DECKS (12 cards: the plainest of the faction)");
            foreach (var faction in factions) {
                var first = AdventureDecks.Starter(faction, pool);
                text.AppendLine($"    {CardTextFormatter.FactionName(faction)}: {string.Join(", ", first.Cards.GroupBy(c => c.cardName).Select(g => $"{g.Key} x{g.Count()}"))}");
                Object.DestroyImmediate(first);
            }
            if (audit.Problems.Count > 0) {
                text.AppendLine();
                text.AppendLine("4. WHAT THE GAMES RAN INTO (the audit)");
                foreach (var problem in audit.Problems) text.AppendLine($"    {problem.Kind}: {problem.Message} ({problem.Count}x; first: {problem.FirstSeen})");
            }
            foreach (var sim in sims) if (sim.EnemyDeck != null) Object.DestroyImmediate(sim.EnemyDeck);
            return text.ToString();
        }
    }
}
