#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// The game's playback speed while it plays in the editor (UI > Playback): slow the Day down to watch what resolves, or
    /// speed it up to get through turns. It sets the time scale, so everything timed by the game follows it; the pause menu
    /// keeps it (it comes back on resume). Also shows how long the game has run, in game time and real time, and the Day.
    /// </summary>
    public class PlaybackWindow : EditorWindow {
        private const float MinSpeed = 0.1f, MaxSpeed = 5f;
        private static readonly float[] Presets = { 0.25f, 0.5f, 1f, 2f, 4f };

        [MenuItem("UI/Playback")]
        private static void Open() {
            var window = GetWindow<PlaybackWindow>("Playback");
            window.minSize = new Vector2(280f, 140f);
        }

        // Repaint while playing so the times tick
        private void OnInspectorUpdate() {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI() {
            if (!Application.isPlaying) {
                EditorGUILayout.HelpBox("Press Play: the speed applies to the game while it runs (it's back to normal each time Play starts).", MessageType.Info);
                return;
            }

            bool paused = Time.timeScale == 0f;
            using (new EditorGUI.DisabledScope(paused)) {
                float speed = EditorGUILayout.Slider("Speed", paused ? 1f : Time.timeScale, MinSpeed, MaxSpeed);
                if (!paused && !Mathf.Approximately(speed, Time.timeScale)) Time.timeScale = speed;
                using (new EditorGUILayout.HorizontalScope()) {
                    foreach (float preset in Presets) {
                        if (GUILayout.Button($"{preset:0.##}x")) Time.timeScale = preset;
                    }
                }
            }
            if (paused) EditorGUILayout.HelpBox("The game is paused (Esc): the speed comes back when it resumes.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Game time", Clock(Time.time));
            EditorGUILayout.LabelField("Real time", Clock(Time.realtimeSinceStartup));
            var game = FindFirstObjectByType<GameManager>();
            if (game?.TurnManager != null) {
                EditorGUILayout.LabelField("Day", $"{game.TurnManager.TurnNumber}{(game.TurnManager.IsResolving ? " (playing out)" : "")}");
            }
        }

        private static string Clock(float seconds) => $"{(int)(seconds / 60f)}:{seconds % 60f:00.0}";
    }
}
#endif
