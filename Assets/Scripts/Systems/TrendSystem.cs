using System.Linq;
using UnityEngine.Events;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The Trend (GDD 4.2 Trends): what the world is into right now, one rule for the whole board, set by a card for some
    /// Days until another Trend replaces it. Zeitgeist's signature: the mood of the times changes the rules for everyone.
    /// Like the weather in a Pokémon battle, a Trend is the same for both sides: it favours whoever is built for it, and it
    /// can turn against whoever set it.
    /// </summary>
    public interface ITrendSystem {
        Trend Current { get; }
        /// <summary>The last Day it's trending; null until another Trend replaces it.</summary>
        int? LastDay { get; }
        UnityEvent<Trend> OnTrendChanged { get; }
        /// <summary>Sets the Trend from this Day for this many Days, counting this one (0: until replaced). None clears it.</summary>
        /// <param name="setBy">Whose card set it (Volcano Lair starts with its side).</param>
        void SetTrend(Trend trend, int days, int currentTurn, IPlayer setBy = null);
        /// <summary>Who set the current Trend, if anyone.</summary>
        IPlayer SetBy { get; }
        /// <summary>At Dusk, before the Trend can end: what it does each Dusk (Survival of the Fittest culls the weakest).</summary>
        void QueueDuskEffects(IActionsQueue queue);
        /// <summary>When it's set, and at each Dawn while it trends: what it does each Day (Volcano Lair hides the boss).</summary>
        void QueueDayEffects(IActionsQueue queue);
        /// <summary>At Dusk: the Trend ends after its last Day.</summary>
        void ProcessDusk(int endedTurn);
        /// <summary>Rush Hour: the slowest act first. Speed orders everything the other way round.</summary>
        bool SlowestFirst { get; }
        /// <summary>The cards the player draws on top of the Day's hand (Zeitgeist: whoever has fewer creatures draws 1 more).</summary>
        int ExtraDraws(IPlayer player);
        /// <summary>Casino: the Day's weather, spun at each Dawn while it trends (None otherwise).</summary>
        CasinoRoll Roll { get; }
    }

    public class TrendSystem : ITrendSystem {
        public Trend Current { get; private set; } = Trend.None;
        public int? LastDay { get; private set; }
        public IPlayer SetBy { get; private set; }
        public const int CullDamage = 3; // Survival of the Fittest
        public CasinoRoll Roll { get; private set; } = CasinoRoll.None;
        public UnityEvent<Trend> OnTrendChanged { get; } = new UnityEvent<Trend>();

        public void SetTrend(Trend trend, int days, int currentTurn, IPlayer setBy = null) {
            LastDay = trend != Trend.None && days > 0 ? currentTurn + days - 1 : null;
            SetBy = trend != Trend.None ? setBy : null;
            if (Current == trend) return; // The same Trend again: only its Days start over
            Current = trend;
            if (trend != Trend.Casino) Roll = CasinoRoll.None;
            Log($"Now trending: {NameOf(trend)}" + (LastDay != null ? $" until the end of Day {LastDay}" : ""), LogTag.Effects);
            OnTrendChanged.Invoke(trend);
        }

        public void ProcessDusk(int endedTurn) {
            if (Current != Trend.None && LastDay != null && endedTurn >= LastDay) SetTrend(Trend.None, 0, endedTurn);
        }

        public bool SlowestFirst => Current == Trend.RushHour || (Current == Trend.Casino && Roll == CasinoRoll.RushHour);

        // Survival of the Fittest: nature culls the weakest creature on the board (the least Health; ties at random), whichever
        // side it's on. Nobody's attack: nothing answers it or redirects it
        public void QueueDuskEffects(IActionsQueue queue) {
            if (Current == Trend.Casino) {
                QueueCasinoDusk(queue);
                return;
            }
            if (Current != Trend.SurvivalOfTheFittest || SetBy?.Opponent == null || queue == null) return;
            var row = SetBy.Battlefield.Concat(SetBy.Opponent.Battlefield).Select(s => s.OccupyingCreature).Where(c => c != null && !c.IsDead).ToList();
            if (row.Count == 0) return;
            int least = row.Min(c => c.Health);
            var tied = row.Where(c => c.Health == least).ToList();
            var struck = tied[GameRandom.Next(tied.Count)];
            Log($"Survival of the Fittest: {struck.Name} is culled", LogTag.Effects);
            queue.AddAction(new DamageCreatureAction(struck, CullDamage));
        }

        // Volcano Lair: on each side, the strongest creature (the most Attack) hides in a lair: Untouchable this Day, as long
        // as another creature of its side stands to guard it. Every villain gets one. Checked when it's set and at each Dawn
        public void QueueDayEffects(IActionsQueue queue) {
            if (Current == Trend.Casino) {
                SpinTheWheel(queue);
                return;
            }
            if (Current != Trend.VolcanoLair || SetBy?.Opponent == null || queue == null) return;
            foreach (var player in new[] { SetBy, SetBy.Opponent }) {
                var side = player.Battlefield.Select(s => s.OccupyingCreature).Where(c => c != null && !c.IsDead).ToList();
                if (side.Count < 2) continue; // No henchmen: no lair
                var boss = EffectResolver.Strongest(side);
                Log($"Volcano Lair: {boss.Name} hides in {player.Name}'s lair", LogTag.Effects);
                queue.AddAction(new ApplyStatusEffectAction(boss, StatusEffectType.Untouchable, 1, 0, controller: player));
            }
        }

        public int ExtraDraws(IPlayer player) {
            if (Current == Trend.Casino) return Roll == CasinoRoll.Windfall ? 1 : 0;
            if (Current != Trend.Zeitgeist || player?.Opponent == null) return 0;
            return CreaturesOf(player) < CreaturesOf(player.Opponent) ? 1 : 0;
        }

        // Casino: when it's set and at each Dawn, the house's wheel spins the Day's weather (never the same twice running),
        // for everyone: Heatwave lifts every creature on the board for the Day, Tailwind half of them at random, Rain and Hail wait for Dusk
        private void SpinTheWheel(IActionsQueue queue) {
            var outcomes = System.Enum.GetValues(typeof(CasinoRoll)).Cast<CasinoRoll>().Where(r => r != CasinoRoll.None && r != Roll).ToList();
            Roll = outcomes[GameRandom.Next(outcomes.Count)];
            Log($"Casino: the wheel stops on {NameOf(Roll)}", LogTag.Effects);
            OnTrendChanged.Invoke(Current); // The banner shows the Day's weather
            if (queue == null || SetBy?.Opponent == null || (Roll != CasinoRoll.Heatwave && Roll != CasinoRoll.Tailwind)) return;
            var lifted = BoardCreatures().ToList();
            // Tailwind blows across the board at random (the user: +1 Speed for everyone changed nobody's order): half the
            // creatures, either side, rounded up
            if (Roll == CasinoRoll.Tailwind) lifted = lifted.OrderBy(_ => GameRandom.Next(int.MaxValue)).Take((lifted.Count + 1) / 2).ToList();
            foreach (var creature in lifted) {
                queue.AddAction(new ModifyAction(creature, 1, Roll == CasinoRoll.Heatwave, false, Roll == CasinoRoll.Tailwind, durationInTurns: 1));
            }
        }

        private void QueueCasinoDusk(IActionsQueue queue) {
            if (queue == null || SetBy?.Opponent == null) return;
            foreach (var creature in BoardCreatures()) {
                if (Roll == CasinoRoll.Rain) queue.AddAction(new HealCreatureAction(creature, 1));
                else if (Roll == CasinoRoll.Hail) queue.AddAction(new DamageCreatureAction(creature, 1));
            }
        }

        private System.Collections.Generic.List<ICreature> BoardCreatures() =>
            SetBy.Battlefield.Concat(SetBy.Opponent.Battlefield).Select(s => s.OccupyingCreature).Where(c => c != null && !c.IsDead).ToList();

        /// <summary>The Casino's weather as the banner and the rules name it.</summary>
        public static string NameOf(CasinoRoll roll) => roll switch {
            CasinoRoll.RushHour => "Rush Hour",
            _ => roll.ToString()
        };

        /// <summary>What the Casino's weather does that Day.</summary>
        public static string RuleOf(CasinoRoll roll) => roll switch {
            CasinoRoll.Heatwave => "every creature +1 Attack this Day",
            CasinoRoll.Rain => "at Dusk every creature heals 1",
            CasinoRoll.Hail => "at Dusk every creature takes 1 damage",
            CasinoRoll.Tailwind => "half the creatures on the board, at random, +1 Speed this Day",
            CasinoRoll.RushHour => "the slowest act first this Day",
            CasinoRoll.Windfall => "both players draw 1 more card next Day",
            _ => ""
        };

        private static int CreaturesOf(IPlayer player) => player.Battlefield.Count(slot => slot.OccupyingCreature != null && !slot.OccupyingCreature.IsDead);

        /// <summary>The Trend as cards print it, a keyword whose tooltip gives its rule.</summary>
        public static string NameOf(Trend trend) => trend switch {
            Trend.RushHour => "Rush Hour",
            Trend.SurvivalOfTheFittest => "Survival of the Fittest",
            Trend.VolcanoLair => "Volcano Lair",
            Trend.Casino => "Casino",
            Trend.None => "Nothing",
            _ => trend.ToString()
        };

        /// <summary>What the Trend does, in a few words.</summary>
        public static string RuleOf(Trend trend) => trend switch {
            Trend.Zeitgeist => "each Day, whoever has fewer creatures draws 1 more card",
            Trend.RushHour => "the slowest act first",
            Trend.SurvivalOfTheFittest => $"each Dusk, the creature with the least Health on the board, either side, takes {CullDamage} damage",
            Trend.VolcanoLair => "each Day, on each side, the strongest creature hides in a lair: it's Untouchable that Day, as long as another creature of its side stands",
            Trend.Casino => "each Dawn the house's wheel spins the Day's weather, the same for both sides: Heatwave (every creature +1 Attack), Rain (at Dusk every creature heals 1), Hail (at Dusk every creature takes 1 damage), Tailwind (half the creatures on the board, at random, +1 Speed), Rush Hour (the slowest act first) or Windfall (both players draw 1 more card next Day)",
            _ => ""
        };
    }
}
