using System.Collections.Generic;
using System.Linq;
using System.Text;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// What each card did in AI games, for balance: how often it was played and by whom, what it dealt (damage to the other
    /// player and their creatures, kills), and how the games where it was played ended. Damage and kills are credited to the
    /// card that caused them: the attacking or dying creature, or the spell being resolved.
    /// </summary>
    public sealed class CardLedger {
        public sealed class Record {
            public string Name { get; internal set; }
            public Faction Faction { get; internal set; }
            public bool IsCreature { get; internal set; }
            public int Plays { get; internal set; }
            /// <summary>Games its side played it at least once, and how those ended for that side.</summary>
            public int Games { get; internal set; }
            public int Wins { get; internal set; }
            public int Losses { get; internal set; }
            /// <summary>Mirror games (the same deck and AI on both sides) where its side played it more often than the other.</summary>
            public int EdgeWins { get; internal set; }
            public int EdgeLosses { get; internal set; }
            public int FaceDamage { get; internal set; }
            public int CreatureDamage { get; internal set; }
            public int Kills { get; internal set; }
            public int FriendlyDamage { get; internal set; }

            public float WinRate => Wins + Losses == 0 ? 0 : Wins / (float)(Wins + Losses);
            public int EdgeGames => EdgeWins + EdgeLosses;
            public float Edge => EdgeGames == 0 ? 0.5f : EdgeWins / (float)EdgeGames;
            public float PlaysPerGame => Games == 0 ? 0 : Plays / (float)Games;
            public float FacePerPlay => Plays == 0 ? 0 : FaceDamage / (float)Plays;
            public float KillsPerPlay => Plays == 0 ? 0 : Kills / (float)Plays;
        }

        /// <summary>One side of one game: who played, with what deck, how it ended, and the cards it played how often.</summary>
        public sealed class Entry {
            public string AI { get; internal set; }
            public string Deck { get; internal set; }
            public bool Won { get; internal set; }
            public bool Decided { get; internal set; }
            public Dictionary<string, int> Plays { get; } = new Dictionary<string, int>();
        }

        private readonly object gate = new object();
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<string, Record> cards = new Dictionary<string, Record>();
        private readonly Dictionary<(string ai, string card), Record> byAI = new Dictionary<(string, string), Record>();
        public IReadOnlyList<Entry> Entries => entries;
        public IReadOnlyCollection<Record> Cards => cards.Values;
        public Record ForAI(string ai, string card) => byAI.TryGetValue((ai, card), out var record) ? record : null;

        /// <summary>Starts tallying one game, on the thread that plays it; its numbers join the others when it ends.</summary>
        internal Watch Begin(GameSession session, GameMediator mediator, AIEntrant first, AIEntrant second, bool mirror, string deck1, string deck2) =>
            new Watch(this, session, mediator, first, second, mirror, deck1, deck2);

        private static Record Get<TKey>(Dictionary<TKey, Record> table, TKey key, string name, Faction faction, bool isCreature) {
            if (!table.TryGetValue(key, out var record)) {
                table[key] = record = new Record { Name = name, Faction = faction, IsCreature = isCreature };
            }
            return record;
        }

        // One game's numbers join the totals: games end on many threads at once
        private void Merge(Dictionary<string, Record> gameCards, Dictionary<(string, string), Record> gameByAI, List<Entry> gameEntries) {
            lock (gate) {
                foreach (var pair in gameCards) Add(Get(cards, pair.Key, pair.Value.Name, pair.Value.Faction, pair.Value.IsCreature), pair.Value);
                foreach (var pair in gameByAI) Add(Get(byAI, pair.Key, pair.Value.Name, pair.Value.Faction, pair.Value.IsCreature), pair.Value);
                entries.AddRange(gameEntries);
            }
        }

        private static void Add(Record total, Record game) {
            total.Plays += game.Plays;
            total.Games += game.Games;
            total.Wins += game.Wins;
            total.Losses += game.Losses;
            total.EdgeWins += game.EdgeWins;
            total.EdgeLosses += game.EdgeLosses;
            total.FaceDamage += game.FaceDamage;
            total.CreatureDamage += game.CreatureDamage;
            total.Kills += game.Kills;
            total.FriendlyDamage += game.FriendlyDamage;
        }

        /// <summary>One game being tallied: what was played, and what each card dealt.</summary>
        internal sealed class Watch {
            private readonly CardLedger ledger;
            private readonly GameSession game;
            private GameMediator mediator;
            private readonly AIEntrant[] seats;
            private readonly string[] deckNames;
            private readonly bool sameOnBothSides;
            private readonly Dictionary<ICard, int>[] plays = { new Dictionary<ICard, int>(), new Dictionary<ICard, int>() };
            private readonly Dictionary<string, Record> cards = new Dictionary<string, Record>();
            private readonly Dictionary<(string ai, string card), Record> byAI = new Dictionary<(string, string), Record>();
            private (ICard card, IPlayer owner) context; // The card whose action is resolving: a spell, a summon, an attack
            private (ICard card, IPlayer owner) credit;  // The card the action resolving now is credited to

            public Watch(CardLedger ledger, GameSession session, GameMediator gameMediator, AIEntrant first, AIEntrant second, bool mirror, string deck1, string deck2) {
                this.ledger = ledger;
                game = session;
                mediator = gameMediator;
                seats = new[] { first, second };
                deckNames = new[] { deck1, deck2 };
                sameOnBothSides = mirror && first.Name == second.Name;
                game.ActionsQueue.OnActionExecuting.AddListener(OnExecuting);
                mediator.PlayerDamaged += OnPlayerDamaged;
                mediator.CreatureDamaged += OnCreatureDamaged;
                mediator.CreatureDied += OnCreatureDied;
            }

            public void Detach() {
                if (mediator == null) return;
                game.ActionsQueue.OnActionExecuting.RemoveListener(OnExecuting);
                mediator.PlayerDamaged -= OnPlayerDamaged;
                mediator.CreatureDamaged -= OnCreatureDamaged;
                mediator.CreatureDied -= OnCreatureDied;
                mediator = null;
            }

            private int SideOf(IPlayer player) => player == game.Player1 ? 0 : 1;

            private void OnExecuting(IGameAction action) {
                credit = default;
                switch (action) {
                    case PlayCardAction play:
                        var counts = plays[SideOf(play.GetOwner())];
                        counts[play.GetCard()] = (counts.TryGetValue(play.GetCard(), out int n) ? n : 0) + 1;
                        context = (play.GetCard(), play.GetOwner());
                        break;
                    case PlaySpellAction spell: context = (spell.GetSpell(), spell.GetOwner()); break;
                    case SummonCreatureAction summon: context = (summon.GetCreature(), summon.GetOwner()); break;
                    case BattlefieldCombatAction attack: context = (attack.GetAttacker(), Whose(attack.GetAttacker())); break;
                    case UseAbilityAction use: context = (use.Creature, Whose(use.Creature)); break;
                    case DamageCreatureAction hit: credit = hit.GetAttacker() != null ? (hit.GetAttacker(), Whose(hit.GetAttacker())) : SpellContext(); break;
                    case DamagePlayerAction hit: credit = hit.GetAttacker() != null ? (hit.GetAttacker(), Whose(hit.GetAttacker())) : SpellContext(); break;
                    case DestroyCreatureAction destroy: credit = destroy.GetKiller() != null ? (destroy.GetKiller(), Whose(destroy.GetKiller())) : SpellContext(); break;
                    case DiscardHandAction or DrawCardsAction: context = default; break; // The Day is over
                }
            }

            // A Gift on the other side still works for the player whose card it is
            private static IPlayer Whose(ICreature creature) => creature == null ? null : creature.DeckOwner ?? creature.Owner;

            // Damage nobody dealt by hand (a spell's effects) is the spell's; other sources (Burn at Dusk) aren't credited
            private (ICard, IPlayer) SpellContext() => context.card is Spell ? context : default;

            private void OnPlayerDamaged(IPlayer player, int amount) {
                if (credit.card == null) return;
                foreach (var record in RecordsOf(credit)) {
                    if (player != credit.owner) record.FaceDamage += amount;
                    else record.FriendlyDamage += amount;
                }
            }

            private void OnCreatureDamaged(ICreature creature, int amount) {
                if (credit.card == null || creature == credit.card) return;
                foreach (var record in RecordsOf(credit)) {
                    if (Whose(creature) != credit.owner) record.CreatureDamage += amount;
                    else record.FriendlyDamage += amount;
                }
            }

            private void OnCreatureDied(ICreature creature) {
                if (credit.card == null || creature == credit.card || Whose(creature) == credit.owner) return;
                foreach (var record in RecordsOf(credit)) record.Kills++;
            }

            private IEnumerable<Record> RecordsOf((ICard card, IPlayer owner) source) {
                if (source.owner == null) yield break;
                yield return Get(cards, source.card.Name, source.card);
                yield return Get(byAI, (seats[SideOf(source.owner)].Name, source.card.Name), source.card);
            }

            private static Record Get<TKey>(Dictionary<TKey, Record> table, TKey key, ICard card) =>
                CardLedger.Get(table, key, card.Name, card.SourceData?.faction ?? Faction.None, card is ICreature);

            public void End(int winner) {
                var entries = new List<Entry>();
                for (int side = 0; side < 2; side++) {
                    bool won = winner == side + 1, lost = winner != 0 && !won;
                    var entry = new Entry { AI = seats[side].Name, Deck = deckNames[side], Won = won, Decided = winner != 0 };
                    foreach (var group in plays[side].GroupBy(p => p.Key.Name)) entry.Plays[group.Key] = group.Sum(p => p.Value);
                    entries.Add(entry);
                    foreach (var (card, count) in plays[side].GroupBy(p => p.Key.Name).Select(g => (g.First().Key, g.Sum(p => p.Value)))) {
                        foreach (var record in new[] { Get(cards, card.Name, card), Get(byAI, (seats[side].Name, card.Name), card) }) {
                            record.Plays += count;
                            record.Games++;
                            if (won) record.Wins++;
                            if (lost) record.Losses++;
                        }
                    }
                }
                // The same deck and AI on both sides: the side that played a card more often, and won or lost
                if (sameOnBothSides && winner != 0) {
                    var names = plays.SelectMany(p => p.Keys).GroupBy(c => c.Name).Select(g => g.First()).ToList();
                    foreach (var card in names) {
                        int first = plays[0].Where(p => p.Key.Name == card.Name).Sum(p => p.Value);
                        int second = plays[1].Where(p => p.Key.Name == card.Name).Sum(p => p.Value);
                        if (first == second) continue;
                        var record = Get(cards, card.Name, card);
                        if ((first > second) == (winner == 1)) record.EdgeWins++;
                        else record.EdgeLosses++;
                    }
                }
                ledger.Merge(cards, byAI, entries);
            }
        }
    }

    public static partial class AITournament {
        /// <summary>
        /// The balance study, three runs of AI games:
        /// <list type="number">
        /// <item>Cards: each deck against itself with the Very Hard AI on both sides, so only luck and the cards played differ.</item>
        /// <item>Strategies: the Hard styles (and Medium) against each other, each deck against itself.</item>
        /// <item>Decks: every deck against every other, the Very Hard AI on both sides, both seats.</item>
        /// </list>
        /// Returns the report.
        /// </summary>
        public static string RunBalance(IReadOnlyList<DeckScriptableObject> decks, System.Action<float> progress = null) {
            var audit = new GameAudit();
            var cardLedger = new CardLedger();
            var styleLedger = new CardLedger();
            var deckLedger = new CardLedger();
            var hard = new AIEntrant(AILevel.VeryHard); // The strongest AI: it plays the cards that pay off later too
            const int CardGamesPerDeck = 100, DeckGamesPerPairing = 3;
            var cards = Read(decks);
            List<Result> cardResults, styleResults, deckResults;
            try {
                int seed = 50000;
                var mirrors = cards.SelectMany(deck => Enumerable.Range(0, CardGamesPerDeck).Select(_ => new Game(deck, hard, deck, hard, seed++))).ToList();
                cardResults = PlayAll(mirrors, audit, cardLedger, p => progress?.Invoke(0.25f * p)).ToList();

                int styleSeed = 1000;
                styleResults = PlayAll(PairingGames(Styles, cards, 2, ref styleSeed), audit, styleLedger, p => progress?.Invoke(0.25f + 0.5f * p)).ToList();

                var versus = (from first in cards from second in cards where second != first
                              from game in Enumerable.Range(0, DeckGamesPerPairing) select (first, second)).ToList();
                var deckGames = versus.Select(v => new Game(v.first, hard, v.second, hard, seed++)).ToList();
                deckResults = PlayAll(deckGames, audit, deckLedger, p => progress?.Invoke(0.75f + 0.25f * p)).ToList();
            }
            finally {
                Release(cards);
            }
            Problems = audit.Problems;
            return BalanceReport(decks, cardResults, cardLedger, styleResults, styleLedger, deckResults, deckLedger) + "\n" + audit.Report();
        }

        private static string BalanceReport(IReadOnlyList<DeckScriptableObject> decks, List<Result> cardResults, CardLedger cardLedger,
            List<Result> styleResults, CardLedger styleLedger, List<Result> deckResults, CardLedger deckLedger) {
            var text = new StringBuilder();
            int games = cardResults.Count + styleResults.Count + deckResults.Count;
            text.AppendLine("BALANCE REPORT (AI games)");
            text.AppendLine($"{games} games: {cardResults.Count} mirror games of the Very Hard AI (cards), {styleResults.Count} between the AI styles " +
                            $"(strategies), {deckResults.Count} deck against deck (decks). Called off after {MaxDays} Days.");
            text.AppendLine();

            // Strategies
            text.AppendLine("1. STRATEGIES: the AI styles, each deck against itself");
            foreach (var entrant in Styles.OrderByDescending(e => WinShare(styleResults, e.Name))) {
                string about = entrant.Level == AILevel.Hard ? $"  {entrant.Style.Description}" : "";
                text.AppendLine($"  {entrant.Name,-16} {WinShare(styleResults, entrant.Name),4:0%}{about}");
            }
            text.AppendLine();

            // Decks and factions
            text.AppendLine("2. DECKS: every deck against every other, Very Hard AI on both sides (win share of its games; draws count as not won)");
            var deckNames = decks.Select(d => d.GetDeckName()).ToList();
            float DeckShare(string deck) {
                var played = deckResults.Where(r => r.Deck == deck || r.Deck2 == deck).ToList();
                int won = played.Count(r => (r.Winner == 1 && r.Deck == deck) || (r.Winner == 2 && r.Deck2 == deck));
                return played.Count == 0 ? 0 : won / (float)played.Count;
            }
            foreach (var deck in deckNames.OrderByDescending(DeckShare)) {
                var played = deckResults.Where(r => r.Deck == deck || r.Deck2 == deck).ToList();
                var beaten = deckNames.Where(o => o != deck).Select(o => (o, Share: PairShare(deckResults, deck, o))).OrderBy(p => p.Share).ToList();
                text.AppendLine($"  {deck,-46} {DeckShare(deck),4:0%}   worst against {beaten.First().o} ({beaten.First().Share:0%}), best against {beaten.Last().o} ({beaten.Last().Share:0%})");
            }
            text.AppendLine();
            text.AppendLine("  What the decks are made of, and their cards that hit hardest in these games (damage and kills a game they're played in)");
            foreach (var deck in decks.OrderByDescending(d => DeckShare(d.GetDeckName()))) {
                var cardsOfDeck = deck.GetCardDataList();
                var creatures = cardsOfDeck.OfType<CreatureData>().ToList();
                string profile = creatures.Count == 0 ? "no creatures" :
                    $"{creatures.Count} creatures (avg {creatures.Average(c => c.attack):0.0}/{creatures.Average(c => c.health):0.0}, speed {creatures.Average(c => c.speed):0.0}, " +
                    $"deploy {creatures.Average(c => c.turnsUntilOperative):0.0}), {cardsOfDeck.Count - creatures.Count} spells";
                var names = new HashSet<string>(cardsOfDeck.Select(c => c.cardName));
                var hitters = deckLedger.Cards.Where(c => names.Contains(c.Name) && c.Games >= 5)
                    .OrderByDescending(c => (c.FaceDamage + c.CreatureDamage) / (float)c.Games).Take(3)
                    .Select(c => $"{c.Name} {(c.FaceDamage + c.CreatureDamage) / (float)c.Games:0.0} dmg, {c.Kills / (float)c.Games:0.0} kills");
                text.AppendLine($"    {ShortDeck(deck.GetDeckName()),-10} {profile}");
                text.AppendLine($"    {"",-10} {string.Join("; ", hitters)}");
            }
            text.AppendLine($"  First seat: Player 1 won {deckResults.Count(r => r.Winner == 1)}, Player 2 {deckResults.Count(r => r.Winner == 2)}, draws {deckResults.Count(r => r.Winner == 0)}; {deckResults.Average(r => r.Days):0.0} Days on average");
            text.AppendLine();

            // Cards
            var playable = cardLedger.Cards.Where(c => c.Games >= 20).ToList();
            text.AppendLine("3. CARDS (mirror games: same deck, same Very Hard AI on both sides)");
            text.AppendLine("   Edge: when one side played the card more often than the other, how often that side won (50% = no effect).");
            text.AppendLine("   Win%: games won by the side that played it. Per play: damage to the other player, kills. Plays a game: how often it's played when it is.");
            text.AppendLine();
            void Table(string title, IEnumerable<CardLedger.Record> rows) {
                text.AppendLine(title);
                text.AppendLine($"    {"Card",-40} {"Faction",-15} {"Edge",5} {"(of)",5} {"Win%",5} {"Face/pl",8} {"Kills/pl",9} {"Plays/g",8}");
                foreach (var c in rows) {
                    text.AppendLine($"    {c.Name,-40} {CardTextFormatter.FactionName(c.Faction),-15} {c.Edge,5:0%} {c.EdgeGames,5} {c.WinRate,5:0%} {c.FacePerPlay,8:0.0} {c.KillsPerPlay,9:0.00} {c.PlaysPerGame,8:0.0}");
                }
                text.AppendLine();
            }
            var withEdge = playable.Where(c => c.EdgeGames >= 60).ToList();
            text.AppendLine("   Creatures and spells are ranked apart: a side whose creatures keep dying plays more of them, so creatures show less edge.");
            text.AppendLine("   For creatures, what they deal over their life (per play) says more.");
            text.AppendLine();
            var spells = withEdge.Where(c => !c.IsCreature).ToList();
            var creatureCards = playable.Where(c => c.IsCreature).ToList();
            Table("  Spells, strongest: the most edge", spells.OrderByDescending(c => c.Edge).Take(12));
            Table("  Spells, weakest: the least edge (playing them more didn't help, or they're played when losing)", spells.OrderBy(c => c.Edge).Take(8));
            Table("  Creatures, most value: damage and kills over their life, per play", creatureCards
                .OrderByDescending(c => c.FacePerPlay + c.CreatureDamage / (float)System.Math.Max(1, c.Plays) / 3f + c.KillsPerPlay * 2f).Take(12));
            Table("  Creatures, least value", creatureCards
                .OrderBy(c => c.FacePerPlay + c.CreatureDamage / (float)System.Math.Max(1, c.Plays) / 3f + c.KillsPerPlay * 2f).Take(8));
            Table("  Most damage to the other player per play", playable.OrderByDescending(c => c.FacePerPlay).Take(10));
            Table("  Most kills per play", playable.OrderByDescending(c => c.KillsPerPlay).Take(10));
            text.AppendLine("  By faction: average edge of its cards (cards with 60+ edge games)");
            foreach (var faction in withEdge.GroupBy(c => c.Faction).OrderByDescending(g => g.Average(c => c.Edge))) {
                var best = faction.OrderByDescending(c => c.Edge).First();
                text.AppendLine($"    {CardTextFormatter.FactionName(faction.Key),-15} {faction.Average(c => c.Edge):0%}   best {best.Name} ({best.Edge:0%})");
            }
            text.AppendLine();

            // Cards per strategy
            text.AppendLine("4. WHAT EACH STRATEGY LEANS ON (style games)");
            text.AppendLine("   Signature: the cards it plays most often compared with the plain Hard AI, with the same deck (x times as often).");
            text.AppendLine("   Lift: in the games it played the card more than usual, how much more it won than its usual with that deck (15+ such games).");
            var decided = styleLedger.Entries.Where(e => e.Decided).ToList();
            float Baseline(string ai, string deck) {
                var games = decided.Where(e => e.AI == ai && e.Deck == deck).ToList();
                return games.Count == 0 ? 0.5f : games.Count(e => e.Won) / (float)games.Count;
            }
            float PlaysPerGame(string ai, string deck, string card) {
                var games = styleLedger.Entries.Where(e => e.AI == ai && e.Deck == deck).ToList();
                return games.Count == 0 ? 0 : games.Sum(e => e.Plays.TryGetValue(card, out int n) ? n : 0) / (float)games.Count;
            }
            string plainHard = new AIEntrant(AILevel.Hard).Name;
            foreach (var entrant in Styles.Where(e => e.Level == AILevel.Hard).OrderByDescending(e => WinShare(styleResults, e.Name))) {
                var mine = styleLedger.Entries.Where(e => e.AI == entrant.Name).ToList();
                var pairs = mine.SelectMany(e => e.Plays.Keys.Select(card => (e.Deck, card))).Distinct().ToList();
                var signature = entrant.Name == plainHard ? new List<(string card, string Deck, float Ratio)>() : pairs
                    .Select(x => (x.card, x.Deck, Ratio: (PlaysPerGame(entrant.Name, x.Deck, x.card) + 0.1f) / (PlaysPerGame(plainHard, x.Deck, x.card) + 0.1f)))
                    .Where(x => x.Ratio >= 1.3f).OrderByDescending(x => x.Ratio).Take(5).ToList();
                // In its games with that deck where it played the card more than its median: did it win more than usual?
                var lifts = pairs.Select(x => {
                    var games = mine.Where(e => e.Decided && e.Deck == x.Deck).ToList();
                    if (games.Count == 0) return (x.card, x.Deck, Lift: 0f, Games: 0);
                    var counts = games.Select(e => e.Plays.TryGetValue(x.card, out int n) ? n : 0).OrderBy(n => n).ToList();
                    int median = counts[counts.Count / 2];
                    var heavy = games.Where(e => (e.Plays.TryGetValue(x.card, out int n) ? n : 0) > median).ToList();
                    float lift = heavy.Count == 0 ? 0 : heavy.Count(e => e.Won) / (float)heavy.Count - Baseline(entrant.Name, x.Deck);
                    return (x.card, x.Deck, Lift: lift, Games: heavy.Count);
                }).Where(x => x.Games >= 15).OrderByDescending(x => x.Lift).Take(5).ToList();
                text.AppendLine($"  {entrant.Name}: {entrant.Style.Description}");
                text.AppendLine($"    signature: {(signature.Count == 0 ? "-" : string.Join(", ", signature.Select(x => $"{x.card} x{x.Ratio:0.0}")))}");
                text.AppendLine($"    lift:      {(lifts.Count == 0 ? "-" : string.Join(", ", lifts.Select(x => $"{x.card} {x.Lift:+0%;-0%} ({ShortDeck(x.Deck)})")))}");
            }
            text.AppendLine();

            text.AppendLine("5. DECK AGAINST DECK: the row deck's wins out of 6 against the column deck");
            text.AppendLine("  " + new string(' ', 16) + string.Join("", deckNames.Select(d => $"{ShortDeck(d),10}")));
            foreach (var row in deckNames) {
                text.AppendLine($"  {ShortDeck(row),-16}" + string.Join("", deckNames.Select(column => {
                    if (row == column) return $"{"-",10}";
                    var played = deckResults.Where(r => (r.Deck == row && r.Deck2 == column) || (r.Deck == column && r.Deck2 == row)).ToList();
                    return $"{played.Count(r => (r.Winner == 1 && r.Deck == row) || (r.Winner == 2 && r.Deck2 == row)),10}";
                })));
            }
            text.AppendLine();

            // Every card, so a new or changed one can be looked up whatever its rank
            text.AppendLine("6. EVERY CARD (mirror games; deck-against-deck damage and kills a game it's played in)");
            text.AppendLine($"    {"Card",-40} {"Faction",-15} {"Edge",5} {"(of)",5} {"Plays/g",8} {"Face/pl",8} {"Kills/pl",9} {"Dmg/g vs",9} {"Kills/g vs",11}");
            foreach (var c in cardLedger.Cards.OrderBy(c => c.Faction).ThenBy(c => c.Name)) {
                var versus = deckLedger.Cards.FirstOrDefault(d => d.Name == c.Name);
                string dmg = versus == null || versus.Games == 0 ? "-" : $"{(versus.FaceDamage + versus.CreatureDamage) / (float)versus.Games:0.0}";
                string kills = versus == null || versus.Games == 0 ? "-" : $"{versus.Kills / (float)versus.Games:0.0}";
                string edge = c.EdgeGames == 0 ? "-" : $"{c.Edge:0%}";
                text.AppendLine($"    {c.Name,-40} {CardTextFormatter.FactionName(c.Faction),-15} {edge,5} {c.EdgeGames,5} {c.PlaysPerGame,8:0.0} {c.FacePerPlay,8:0.0} {c.KillsPerPlay,9:0.00} {dmg,9} {kills,11}");
            }
            return text.ToString();
        }

        // The faction of a one-faction deck; a two-faction deck goes by its own name
        private static string ShortDeck(string deck) {
            int dash = deck.IndexOf(" - ");
            string name = dash < 0 ? deck : deck.Substring(0, dash).Contains("&") ? deck.Substring(dash + 3) : deck.Substring(0, dash);
            return name.Length > 9 ? name.Substring(0, 8) + "." : name;
        }

        private static float PairShare(List<Result> results, string deck, string other) {
            var played = results.Where(r => (r.Deck == deck && r.Deck2 == other) || (r.Deck == other && r.Deck2 == deck)).ToList();
            int won = played.Count(r => (r.Winner == 1 && r.Deck == deck) || (r.Winner == 2 && r.Deck2 == deck));
            return played.Count == 0 ? 0 : won / (float)played.Count;
        }

        // Its share of the games it won, out of those that someone won
        private static float WinRate(List<Result> results, string name) {
            var decided = results.Where(r => (r.First.Name == name || r.Second.Name == name) && r.Winner != 0).ToList();
            return decided.Count == 0 ? 0 : decided.Count(r => r.WinnerName == name) / (float)decided.Count;
        }
    }
}
