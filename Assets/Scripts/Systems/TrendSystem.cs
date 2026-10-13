using System.Linq;
using UnityEngine.Events;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The Trend (GDD 4.2 Trends): what the world is into right now, one rule for the whole board, set by a card for some
    /// Days until another Trend replaces it. Zeitgeist's signature: the mood of the times changes the rules for everyone.
    /// </summary>
    public interface ITrendSystem {
        Trend Current { get; }
        /// <summary>The last Day it's trending; null until another Trend replaces it.</summary>
        int? LastDay { get; }
        UnityEvent<Trend> OnTrendChanged { get; }
        /// <summary>Sets the Trend from this Day for this many Days, counting this one (0: until replaced). None clears it.</summary>
        /// <param name="setBy">Whose card set it: a Trend that takes a side (Freak Weather) strikes the other one.</param>
        void SetTrend(Trend trend, int days, int currentTurn, IPlayer setBy = null);
        /// <summary>Who set the current Trend, if anyone.</summary>
        IPlayer SetBy { get; }
        /// <summary>At Dusk, before the Trend can end: what it does each Dusk (Freak Weather's lightning).</summary>
        void QueueDuskEffects(IActionsQueue queue);
        /// <summary>At Dusk: the Trend ends after its last Day.</summary>
        void ProcessDusk(int endedTurn);
        /// <summary>Rush Hour: the slowest act first. Speed orders everything the other way round.</summary>
        bool SlowestFirst { get; }
        /// <summary>The cards the player draws on top of the Day's hand (Zeitgeist: whoever has fewer creatures draws 1 more).</summary>
        int ExtraDraws(IPlayer player);
    }

    public class TrendSystem : ITrendSystem {
        public Trend Current { get; private set; } = Trend.None;
        public int? LastDay { get; private set; }
        public IPlayer SetBy { get; private set; }
        public const int LightningDamage = 2;
        public UnityEvent<Trend> OnTrendChanged { get; } = new UnityEvent<Trend>();

        public void SetTrend(Trend trend, int days, int currentTurn, IPlayer setBy = null) {
            LastDay = trend != Trend.None && days > 0 ? currentTurn + days - 1 : null;
            SetBy = trend != Trend.None ? setBy : null;
            if (Current == trend) return; // The same Trend again: only its Days start over
            Current = trend;
            Log($"Now trending: {NameOf(trend)}" + (LastDay != null ? $" until the end of Day {LastDay}" : ""), LogTag.Effects);
            OnTrendChanged.Invoke(trend);
        }

        public void ProcessDusk(int endedTurn) {
            if (Current != Trend.None && LastDay != null && endedTurn >= LastDay) SetTrend(Trend.None, 0, endedTurn);
        }

        public bool SlowestFirst => Current == Trend.RushHour;

        // Freak Weather: lightning strikes the toughest creature (the most Health; ties at random) on the other side of
        // whoever set it. Nobody's attack: nothing answers it or redirects it
        public void QueueDuskEffects(IActionsQueue queue) {
            if (Current != Trend.FreakWeather || SetBy?.Opponent == null || queue == null) return;
            var row = SetBy.Opponent.Battlefield.Select(s => s.OccupyingCreature).Where(c => c != null && !c.IsDead).ToList();
            if (row.Count == 0) return;
            int top = row.Max(c => c.Health);
            var tied = row.Where(c => c.Health == top).ToList();
            var struck = tied[GameRandom.Next(tied.Count)];
            Log($"Freak Weather: lightning strikes {struck.Name}", LogTag.Effects);
            queue.AddAction(new DamageCreatureAction(struck, LightningDamage));
        }

        public int ExtraDraws(IPlayer player) {
            if (Current != Trend.Zeitgeist || player?.Opponent == null) return 0;
            return CreaturesOf(player) < CreaturesOf(player.Opponent) ? 1 : 0;
        }

        private static int CreaturesOf(IPlayer player) => player.Battlefield.Count(slot => slot.OccupyingCreature != null && !slot.OccupyingCreature.IsDead);

        /// <summary>The Trend as cards print it, a keyword whose tooltip gives its rule.</summary>
        public static string NameOf(Trend trend) => trend switch {
            Trend.RushHour => "Rush Hour",
            Trend.FreakWeather => "Freak Weather",
            Trend.None => "Nothing",
            _ => trend.ToString()
        };

        /// <summary>What the Trend does, in a few words.</summary>
        public static string RuleOf(Trend trend) => trend switch {
            Trend.Zeitgeist => "each Day, whoever has fewer creatures draws 1 more card",
            Trend.RushHour => "the slowest act first",
            Trend.FreakWeather => $"each Dusk, lightning strikes the toughest creature on the other side of whoever set it, for {LightningDamage} damage",
            _ => ""
        };
    }
}
