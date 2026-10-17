using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// The balance study's summary (the user, 2026-10-16: "assess what strategies, combination of abilities and cards are
    /// bringing the most and are the most winning ones"), from the deck-against-deck games: factions (their one-faction
    /// decks), strategies (each deck's tag: Aggro, Control, Midrange, Engine, Swarm, Combo), the mechanics on the cards
    /// (abilities, movement, Homing, costs, what their effects do) and the pairs of cards that win together.
    /// </summary>
    public static partial class AITournament {
        private static float DeckWinShare(List<Result> results, string deck) {
            var played = results.Where(r => r.Deck == deck || r.Deck2 == deck).ToList();
            int won = played.Count(r => (r.Winner == 1 && r.Deck == deck) || (r.Winner == 2 && r.Deck2 == deck));
            return played.Count == 0 ? 0 : won / (float)played.Count;
        }

        // The mechanics a card carries, as the report names them
        private static IEnumerable<string> MechanicsOf(CardDataScriptableObject card) {
            if (card is CreatureCardScriptableObject creature) {
                if (creature.ability != CreatureAbility.None) yield return "Ability: " + Abilities.NameOf(creature.ability);
                if (creature.movement.HasFlag(MovementKeyword.Leapfrog)) yield return "Leapfrog";
                if (creature.movement.HasFlag(MovementKeyword.Swap)) yield return "Swap";
                if (creature.gift) yield return "Gift";
                yield return creature.turnsUntilOperative == 0 ? "Creature, ready at once" : "Creature, deploys " + creature.turnsUntilOperative;
                if (creature.speed >= 3) yield return "Fast creature (Speed 3+)";
                if (creature.health >= 7) yield return "Big body (7+ Health)";
            }
            if (card.homing) yield return "Homing";
            if (card.singleUse) yield return "Single Use";
            if (card.sacrifice > 0) yield return "Sacrifice cost";
            if (card.discardCost > 0) yield return "Discard cost";
            if (card.doomCost > 0) yield return "Doom cost";
            if (card.prophecy > 0) yield return "Prophecy";
            if (card.gamble) yield return "Gamble";
            var data = card.ToCardData();
            try {
                foreach (var effect in data.effects ?? new List<CardEffect>()) {
                    string when = effect.trigger switch {
                        EffectTrigger.OnPlay when card is CreatureCardScriptableObject => "Arrival",
                        EffectTrigger.OnDeath => "Last Will",
                        EffectTrigger.StartOfTurn => "Dawn",
                        EffectTrigger.EndOfTurn => "Dusk",
                        EffectTrigger.OnDamage => "Hurt",
                        _ => null
                    };
                    if (when != null) yield return "Trigger: " + when;
                    foreach (var action in effect.actions) {
                        string what = action.actionType switch {
                            ActionType.Damage when action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets) => "Board damage",
                            ActionType.Damage => "Targeted damage",
                            ActionType.Destroy => "Destroy",
                            ActionType.Draw => "Card draw",
                            ActionType.SendFor => "Send for a card",
                            ActionType.Heal => "Healing",
                            ActionType.Armor => "Armor",
                            ActionType.Stun => "Exhaust",
                            ActionType.ModifyStat when action.value > 0 => "Buffs",
                            ActionType.ModifyStat => "Debuffs",
                            ActionType.ApplyStatus => "Status: " + action.statusEffectToApply,
                            ActionType.Summon or ActionType.SummonDecoys => "Summon tokens",
                            ActionType.TakeControl => "Take control",
                            ActionType.CreateWatchdog => "Watchdog",
                            ActionType.PlaceSlotEffect => "Slot effect",
                            ActionType.SetTrend => "Trend",
                            ActionType.SwapPositions => "Swap positions",
                            ActionType.ReturnToHand => "Back to the hand",
                            _ => null
                        };
                        if (what != null) yield return what;
                    }
                }
            }
            finally {
                Object.DestroyImmediate(data);
            }
        }

        internal static string StrategyReport(IReadOnlyList<DeckScriptableObject> decks, List<Result> deckResults, CardLedger deckLedger) {
            var text = new StringBuilder();
            text.AppendLine();
            text.AppendLine("6. SUMMARY: FACTIONS, STRATEGIES, MECHANICS AND COMBINATIONS (deck against deck, Very Hard AI on both sides)");
            text.AppendLine();
            var shares = decks.ToDictionary(d => d.GetDeckName(), d => DeckWinShare(deckResults, d.GetDeckName()));

            text.AppendLine("  Factions: the average win share of their one-faction decks (each faction has two: its best, and a second strategy),");
            text.AppendLine("  then of the two-faction decks they're part of");
            var single = decks.Where(d => DeckCatalog.FactionsOf(d).Count == 1).ToList();
            var pairs = decks.Where(d => DeckCatalog.FactionsOf(d).Count == 2).ToList();
            // The four factions (2026-10-17): a deck of one of them, whatever its subfactions, and the decks of two
            var ofOne = decks.Where(d => DeckCatalog.UmbrellasOf(d).Count == 1).ToList();
            var ofTwo = decks.Where(d => DeckCatalog.UmbrellasOf(d).Count == 2).ToList();
            foreach (var umbrella in ofOne.GroupBy(d => DeckCatalog.UmbrellasOf(d)[0]).OrderByDescending(g => g.Average(d => shares[d.GetDeckName()]))) {
                var withOthers = ofTwo.Where(d => DeckCatalog.UmbrellasOf(d).Contains(umbrella.Key)).ToList();
                string paired = withOthers.Count == 0 ? "-" : $"{withOthers.Average(d => shares[d.GetDeckName()]):0%}";
                text.AppendLine($"    {Umbrellas.Name(umbrella.Key),-16} {umbrella.Average(d => shares[d.GetDeckName()]),4:0%}   ({umbrella.Count()} decks);  in pairs {paired}");
            }
            text.AppendLine("  Subfactions:");
            foreach (var faction in single.GroupBy(DeckCatalog.FactionOf).OrderByDescending(g => g.Average(d => shares[d.GetDeckName()]))) {
                var withOthers = pairs.Where(d => DeckCatalog.FactionsOf(d).Contains(faction.Key)).ToList();
                string own = string.Join(", ", faction.OrderByDescending(d => shares[d.GetDeckName()]).Select(d => $"{d.Strategy} {shares[d.GetDeckName()]:0%}"));
                string paired = withOthers.Count == 0 ? "-" : $"{withOthers.Average(d => shares[d.GetDeckName()]):0%}";
                text.AppendLine($"    {CardTextFormatter.FactionName(faction.Key),-16} {faction.Average(d => shares[d.GetDeckName()]),4:0%}   ({own});  in pairs {paired}");
            }
            text.AppendLine();

            text.AppendLine("  Strategies: the average win share of the decks playing each (every faction's decks together)");
            foreach (var strategy in decks.GroupBy(d => string.IsNullOrEmpty(d.Strategy) ? "-" : d.Strategy).OrderByDescending(g => g.Average(d => shares[d.GetDeckName()]))) {
                var best = strategy.OrderByDescending(d => shares[d.GetDeckName()]).First();
                text.AppendLine($"    {strategy.Key,-10} {strategy.Average(d => shares[d.GetDeckName()]),4:0%}   {strategy.Count()} decks; best {best.GetDeckName()} ({shares[best.GetDeckName()]:0%})");
            }
            text.AppendLine("    Strategy against strategy (rows' win share against columns):");
            var tags = decks.Select(d => d.Strategy).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s).ToList();
            var strategyOf = decks.ToDictionary(d => d.GetDeckName(), d => d.Strategy);
            text.AppendLine("    " + new string(' ', 10) + string.Concat(tags.Select(t => $"{t,10}")));
            foreach (var row in tags) {
                var line = new StringBuilder($"    {row,-10}");
                foreach (var column in tags) {
                    var games = deckResults.Where(r => strategyOf.TryGetValue(r.Deck, out var a) && strategyOf.TryGetValue(r.Deck2, out var b)
                                                       && ((a == row && b == column) || (a == column && b == row)) && r.Deck != r.Deck2).ToList();
                    int won = games.Count(r => (r.Winner == 1 && strategyOf[r.Deck] == row) || (r.Winner == 2 && strategyOf[r.Deck2] == row));
                    int asRow = games.Count(r => strategyOf[r.Deck] == row || strategyOf[r.Deck2] == row);
                    line.Append(row == column || asRow == 0 ? $"{"-",10}" : $"{won / (float)asRow,10:0%}");
                }
                text.AppendLine(line.ToString());
            }
            text.AppendLine();

            // Mechanics: every card's games (its side played it), grouped by what it carries
            var cardsByName = decks.SelectMany(d => d.Cards).Where(c => c != null).GroupBy(c => c.cardName).ToDictionary(g => g.Key, g => g.First());
            var mechanicOf = cardsByName.ToDictionary(p => p.Key, p => MechanicsOf(p.Value).Distinct().ToList());
            var mechanics = new Dictionary<string, (int games, int wins, float impact, HashSet<string> cards)>();
            foreach (var record in deckLedger.Cards.Where(c => c.Games > 0 && mechanicOf.ContainsKey(c.Name))) {
                foreach (var mechanic in mechanicOf[record.Name]) {
                    var (games, wins, impact, cards) = mechanics.TryGetValue(mechanic, out var m) ? m : (0, 0, 0f, new HashSet<string>());
                    cards.Add(record.Name);
                    mechanics[mechanic] = (games + record.Wins + record.Losses, wins + record.Wins, impact + record.Impact, cards);
                }
            }
            text.AppendLine("  Mechanics: the cards carrying each, in the games their side played them (win share of those games; impact a game,");
            text.AppendLine("  damage, kills and what they set up and supplied). 50% means no edge. At least 3 cards and 60 games.");
            text.AppendLine($"    {"Mechanic",-34} {"Win%",5} {"Impact/g",9} {"Cards",6} {"Games",6}");
            foreach (var pair in mechanics.Where(m => m.Value.cards.Count >= 3 && m.Value.games >= 60).OrderByDescending(m => m.Value.wins / (float)m.Value.games)) {
                text.AppendLine($"    {pair.Key,-34} {pair.Value.wins / (float)pair.Value.games,5:0%} {pair.Value.impact / pair.Value.games,9:0.0} {pair.Value.cards.Count,6} {pair.Value.games,6}");
            }
            text.AppendLine();

            // Combinations: two cards played by the same side in a game, against how that deck does without them together
            text.AppendLine("  Combinations: two cards its side played in the same game, and how much more that side won than its deck usually");
            text.AppendLine("  does (lift). At least 25 such games; the cards of one deck only, since pairs only meet in a deck.");
            var entries = deckLedger.Entries.Where(e => e.Decided).ToList();
            var deckWin = entries.GroupBy(e => e.Deck).ToDictionary(g => g.Key, g => g.Count(e => e.Won) / (float)g.Count());
            var together = new Dictionary<(string, string), (int games, int wins, float expected, string deck)>();
            foreach (var entry in entries) {
                var played = entry.Plays.Where(p => p.Value > 0).Select(p => p.Key).OrderBy(n => n).ToList();
                for (int i = 0; i < played.Count; i++) {
                    for (int j = i + 1; j < played.Count; j++) {
                        var key = (played[i], played[j]);
                        var (games, wins, expected, deck) = together.TryGetValue(key, out var t) ? t : (0, 0, 0f, entry.Deck);
                        together[key] = (games + 1, wins + (entry.Won ? 1 : 0), expected + deckWin[entry.Deck], deck);
                    }
                }
            }
            var ranked = together.Where(t => t.Value.games >= 25).Select(t => (t.Key, t.Value.games, Win: t.Value.wins / (float)t.Value.games,
                Lift: (t.Value.wins - t.Value.expected) / t.Value.games, t.Value.deck)).ToList();
            text.AppendLine("    Each deck's best pair (the most lift), the decks by win share:");
            foreach (var p in ranked.GroupBy(p => p.deck).Select(g => g.OrderByDescending(p => p.Lift).First()).OrderByDescending(p => shares.TryGetValue(p.deck, out var w) ? w : 0))
                text.AppendLine($"      {p.Key.Item1 + " + " + p.Key.Item2,-62} {p.Win,5:0%} won   lift {p.Lift,4:+0%;-0%}   {p.games,4} games   ({p.deck})");
            text.AppendLine("    The pairs that add the most to their deck (lift):");
            foreach (var p in ranked.OrderByDescending(p => p.Lift).Take(12))
                text.AppendLine($"      {p.Key.Item1 + " + " + p.Key.Item2,-62} {p.Win,5:0%} won   lift {p.Lift,4:+0%;-0%}   {p.games,4} games   ({p.deck})");
            text.AppendLine("    The pairs that drag their deck down:");
            foreach (var p in ranked.OrderBy(p => p.Lift).Take(8))
                text.AppendLine($"      {p.Key.Item1 + " + " + p.Key.Item2,-62} {p.Win,5:0%} won   lift {p.Lift,4:+0%;-0%}   {p.games,4} games   ({p.deck})");
            text.AppendLine();

            text.AppendLine("  The cards that bring the most (impact a game, deck against deck; 15+ games), and their win share");
            foreach (var c in deckLedger.Cards.Where(c => c.Games >= 15).OrderByDescending(c => c.ImpactPerGame).Take(15))
                text.AppendLine($"    {c.Name,-40} {CardTextFormatter.FactionName(c.Faction),-15} impact {c.ImpactPerGame,5:0.0}   won {c.WinRate,4:0%}");
            return text.ToString();
        }
    }
}
