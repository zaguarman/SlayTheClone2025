using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// The first run (the user, 2026-10-17: "a Slay the Spire tutorial run handcrafted for new players using the seed
    /// generation"): an adventure on a fixed map seed with a fixed faction, whose first fight is played with a tutor on screen
    /// (<see cref="TutorialOverlay"/>): place a creature, end the Day, attack, read the queue, play a spell. When that fight is
    /// won, a screen says which cards the player "unlocked", for looks only: every card stays available all the time while the
    /// game is being tested (the user). Then the run goes on as any adventure.
    /// </summary>
    public static class Tutorial {
        public const int Seed = 20261017;
        public const Umbrella Faction = Umbrella.DigitalSociety; // Cheap creatures that act at once: the simplest board to learn on
        /// <summary>The cards the first win "unlocks" (for looks, see the class note).</summary>
        public static readonly string[] Unlocks = { "Clickbait", "Dogpile", "Karen" };
        private const string DoneKey = "TutorialDone";

        /// <summary>A tutorial run is under way: its first fight gets the tutor, its first win the unlocks.</summary>
        public static bool Active { get; private set; }
        /// <summary>The tutored fight was won: the unlocks screen is due.</summary>
        public static bool ShowsUnlocks { get; private set; }
        public static bool Done => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        /// <summary>Starts the run: the fixed seed and faction, the usual starter, no opening event; the first fight is next.</summary>
        public static void Begin(IReadOnlyList<CardDataScriptableObject> pool, IReadOnlyList<CardDataScriptableObject> potions) {
            AdventureRun.Start(Faction, AdventureDecks.Starter(Faction, pool), Seed, pool, potions);
            AdventureRun.LeaveEvent(); // No Orientation Day: straight to the first room
            Active = true;
            ShowsUnlocks = false;
        }

        /// <summary>The tutored fight ended: won, the unlocks are shown next; lost, the tutor is over and the run goes on.</summary>
        public static void FightEnded(bool won) {
            if (!Active) return;
            ShowsUnlocks = won;
            if (!won) End();
        }

        /// <summary>The unlocks were seen (or the run was left): the tutorial is over, and remembered as done.</summary>
        public static void End() {
            Active = false;
            ShowsUnlocks = false;
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Forgets that the tutorial was done (tests; a fresh start).</summary>
        public static void Forget() {
            Active = ShowsUnlocks = false;
            PlayerPrefs.DeleteKey(DoneKey);
        }

        // The tutor joins the game scene of the tutorial's first fight
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Watch() => SceneManager.sceneLoaded += (scene, mode) => {
            if (!Active || ShowsUnlocks || AdventureRun.Fighting == null || AdventureRun.FightsWon > 0) return;
            if (Object.FindFirstObjectByType<GameManager>() == null) return;
            if (Object.FindFirstObjectByType<TutorialOverlay>() == null) new GameObject("Tutorial").AddComponent<TutorialOverlay>();
        };
    }
}
