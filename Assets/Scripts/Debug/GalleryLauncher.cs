#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Starts a gallery (<see cref="ArrowGallery"/>, <see cref="RouletteGallery"/>) on its own (the user): presses Play if the
    /// editor isn't playing, waits until the game is fully up (its players dealt, the table built), then runs it. The wait
    /// survives entering Play mode (it's kept in the editor's session); stopping Play, or no game after a while (another
    /// scene open), drops it.
    /// </summary>
    [InitializeOnLoad]
    public static class GalleryLauncher {
        public const string Arrows = "Arrow Gallery", Roulette = "Roulette Gallery", Battlefields = "Battlefield Gallery";
        private const string PendingKey = "SlayTheClone.GalleryPending";
        private const string SinceKey = "SlayTheClone.GalleryPendingSince";
        private const double GiveUpSeconds = 30.0;
        private const float SettleSeconds = 0.5f; // Once the game is up: its first frames, the hands dealt

        static GalleryLauncher() => EditorApplication.update += Update;

        /// <summary>Runs the gallery now if the game is ready, or once it is (pressing Play first if needed).</summary>
        public static void Start(string gallery) {
            if (Application.isPlaying && GameReady()) {
                Run(gallery);
                return;
            }
            SessionState.SetString(PendingKey, gallery);
            SessionState.SetFloat(SinceKey, (float)EditorApplication.timeSinceStartup);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        /// <summary>The gallery waiting for the game to start, if any.</summary>
        public static string Waiting => SessionState.GetString(PendingKey, "");

        public static void Cancel() => SessionState.EraseString(PendingKey);

        private static void Update() {
            string gallery = Waiting;
            if (gallery.Length == 0) return;
            if (!Application.isPlaying) {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) Cancel(); // Play was stopped before it started
                return;
            }
            if (GameReady()) {
                Cancel();
                Run(gallery);
                return;
            }
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(SinceKey, 0f) > GiveUpSeconds) {
                Cancel();
                Debug.LogWarning($"{gallery}: no game started. Open the game scene, then start it again.");
            }
        }

        private static bool GameReady() {
            var game = Object.FindFirstObjectByType<GameManager>();
            return game != null && game.Player1 != null && game.Player2 != null && game.TurnManager != null && game.ActionsQueue != null
                   && Object.FindFirstObjectByType<GameReferences>() != null && Time.timeSinceLevelLoad > SettleSeconds;
        }

        private static void Run(string gallery) {
            if (gallery == Arrows) ArrowGallery.Run();
            else if (gallery == Roulette) RouletteGallery.Run();
            else if (gallery == Battlefields) BattlefieldGallery.Run();
        }
    }
}
#endif
