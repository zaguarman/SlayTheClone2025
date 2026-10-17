using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// Ascension (the user, 2026-10-16, from Slay the Spire): harder runs, unlocked one level at a time by beating the boss
    /// at the level below, for each faction on its own. Each level keeps the ones below it:
    /// 1. Elites have a stage more Health. 2. Every enemy AI plays a level better. 3. You start with 50 Health.
    /// 4. Rest sites heal 15. 5. The boss has 80 Health.
    /// The level a faction has unlocked is kept between sessions (PlayerPrefs).
    /// </summary>
    public static class Ascension {
        public const int Max = 5;
        public const int StartingHealth = 50, RestHeal = 15, BossHealth = 80;

        public static readonly string[] Levels = {
            "No Ascension: the plain run.",
            "Elites have a stage more Health.",
            "Every enemy AI plays a level better.",
            "You start with 50 Health.",
            "Rest sites heal 15.",
            "The boss has 80 Health."
        };

        private static string Key(Faction faction) => $"Ascension.{faction}";

        /// <summary>The highest level this faction may play: 0 until its boss is beaten.</summary>
        public static int Unlocked(Faction faction) => Mathf.Clamp(PlayerPrefs.GetInt(Key(faction), 0), 0, Max);

        /// <summary>A run won at this level opens the next one, for that faction.</summary>
        public static bool UnlockAfterWin(Faction faction, int level) {
            if (level >= Max || Unlocked(faction) > level) return false;
            PlayerPrefs.SetInt(Key(faction), level + 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>What a level adds, with the ones below it, one line each.</summary>
        public static IEnumerable<string> Rules(int level) => Enumerable.Range(1, Mathf.Clamp(level, 0, Max)).Select(i => $"{i}. {Levels[i]}");

        public static int EliteStages(int level) => level >= 1 ? 1 : 0;
        public static int AIBonus(int level) => level >= 2 ? 1 : 0;
        public static int StartHealth(int level) => level >= 3 ? StartingHealth : AdventureRun.StartingHealth;
        public static int RestHealing(int level) => level >= 4 ? RestHeal : AdventureRun.RestHeal;
        public static int BossHealthAt(int level) => level >= 5 ? BossHealth : AdventureRun.BossHealth;
    }
}
