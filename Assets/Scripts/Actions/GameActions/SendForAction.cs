using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Send For (every faction has one, each its own way: Shadow Ops.' logistics, Myth's rituals, Tech's scraping...): a card
    /// from your deck joins your next hand, so a plan can be lined up for the Day it needs it. The player picks it when the
    /// card is played (see <see cref="IPlayer.SendForPicks"/>); with no pick, the best one is sent for. Some look only at
    /// the top of the deck (scouting), some only take creatures or spells.
    /// </summary>
    public class SendForAction : IGameAction {
        public IPlayer Player { get; }
        public ICard Source { get; } // The card that sends for it: its pick is kept under it
        public SendForFilter Filter { get; }
        public int Top { get; } // Only the top this many cards of the deck (0: the whole deck)

        public SendForAction(IPlayer player, ICard source, SendForFilter filter, int top) {
            Player = player;
            Source = source;
            Filter = filter;
            Top = top;
            Log($"Created {this}", LogTag.Actions | LogTag.Cards);
        }

        public override string ToString() => $"SendForAction: {Player?.Name} sends for a card ({Filter}{(Top > 0 ? $", top {Top}" : "")})";
    }

    public class SendForActionExecutor : ActionExecutor<SendForAction> {
        protected override void ExecuteAction(SendForAction sending, ActionExecutionContext context) {
            var player = sending.Player;
            if (player == null) return;
            var choices = SendFor.Choices(player, sending.Filter, sending.Top);
            player.SendForPicks.TryGetValue(sending.Source, out var picked);
            player.SendForPicks.Remove(sending.Source);
            var card = picked != null && choices.Contains(picked) ? picked : SendFor.Best(player, choices);
            if (card == null || !player.Deck.RemoveCard(card)) {
                Log($"{player.Name} sends for a card, but there's none to send for", LogTag.Cards);
                return;
            }
            player.ComingBack.Add(card);
            Log($"{player.Name} sends for {card.Name}: it joins the next hand", LogTag.Cards | LogTag.Actions);
            context.GameMediator.NotifyHandStateChanged(player);
        }
    }

    public static class SendFor {
        /// <summary>The Send For on this card, if it has one (a spell's, or a creature's Arrival).</summary>
        public static EffectAction Of(ICard card) =>
            card?.Effects?.SelectMany(e => e.actions).FirstOrDefault(a => a.actionType == ActionType.SendFor);

        /// <summary>What it can send for now: the deck (or its top cards), of the kind it takes.</summary>
        public static List<ICard> Choices(IPlayer player, SendForFilter filter, int top) {
            var deck = player?.Deck.GetDeckPreview() ?? new List<ICard>();
            return (top > 0 ? deck.Take(top) : deck).Where(c => filter switch {
                SendForFilter.Creature => c is ICreature,
                SendForFilter.Spell => c is Spell,
                _ => true
            }).ToList();
        }

        /// <summary>
        /// The pick for a player who didn't pick (and the AI's): a creature when its side has fewer than the other, else the
        /// spell that does the most to the other side, else the strongest creature.
        /// </summary>
        public static ICard Best(IPlayer player, List<ICard> choices) {
            if (choices.Count == 0) return null;
            var creatures = choices.OfType<ICreature>().OrderByDescending(c => c.Attack + c.Health - c.TurnsUntilOperative).ToList();
            var spells = choices.OfType<Spell>().Where(s => Of(s) == null).OrderByDescending(SpellPunch).ToList();
            bool outnumbered = EffectResolver.CreatureCount(player) < EffectResolver.CreatureCount(player.Opponent);
            if (outnumbered && creatures.Count > 0) return creatures[0];
            if (spells.Count > 0 && SpellPunch(spells[0]) > 0) return spells[0];
            return creatures.FirstOrDefault() ?? (ICard)spells.FirstOrDefault() ?? choices[0];
        }

        // Damage and destruction aimed at the other side: what a spell does to them
        private static int SpellPunch(Spell spell) => spell.Effects.SelectMany(e => e.actions).Sum(a => a.actionType switch {
            ActionType.Destroy => 6,
            ActionType.Damage when a.targetType is TargetType.EnemyCreatures or TargetType.Enemy =>
                a.value * (a.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets) ? 3 : 1),
            ActionType.TakeControl => 5,
            _ => 0
        });
    }
}
