using DG.Tweening;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Every measurement of the card roulette, as sliders: the wheel of cards that turns when you scroll over a creature with
    /// cards tucked under it (see <see cref="Tooltip"/>). Select it with UI > Roulette Tuning and drag them while the game
    /// plays; the wheel showing redraws as they change. Kept in Resources/RouletteTuning, so what's set there is what the
    /// game uses. UI > Roulette Gallery turns the wheel on its own, to judge them.
    /// </summary>
    public class RouletteTuning : ScriptableObject {
        public const string ResourcePath = "RouletteTuning";

        [Header("The wheel")]
        [Tooltip("Degrees between two cards on the wheel")]
        [Range(5f, 90f)] public float angle = 32f;
        [Tooltip("The wheel's radius, in preview heights: how far below the shown card its center is (bigger: a flatter arc)")]
        [Range(0.3f, 5f)] public float radius = 1.6f;
        [Tooltip("Cards shown faint on the wheel each side of the shown one")]
        [Range(0, 6)] public int reach = 3;

        [Header("A turn")]
        [Tooltip("Seconds a turn of the wheel takes (one scroll notch)")]
        [Range(0.05f, 1.5f)] public float seconds = 0.28f;
        [Tooltip("How the turn eases in and out")]
        public Ease ease = Ease.OutCubic;

        [Header("The cards waiting on the wheel")]
        [Tooltip("The next card to come (on the left): its opacity")]
        [Range(0f, 1f)] public float nextAlpha = 0.55f;
        [Tooltip("Each card further ahead: this much fainter than the one before")]
        [Range(0f, 0.5f)] public float aheadFade = 0.15f;
        [Tooltip("The card just gone by (on the right): its opacity")]
        [Range(0f, 1f)] public float lastAlpha = 0.4f;
        [Tooltip("Each card further back: its opacity times this")]
        [Range(0f, 1f)] public float behindFade = 0.4f;

        [Header("The preview")]
        [Tooltip("The enlarged card, in card sizes (always well bigger than the card as shown)")]
        [Range(1f, 3.5f)] public float previewScale = 2f;

        /// <summary>How faint a card is on the wheel, this many places ahead of the shown one (or behind it).</summary>
        public float AlphaAt(int distance, bool ahead) =>
            ahead ? Mathf.Max(0f, nextAlpha - aheadFade * (distance - 1)) : lastAlpha * Mathf.Pow(behindFade, distance - 1);

        private static RouletteTuning current;

        /// <summary>The tuning in use: Resources/RouletteTuning, or the defaults when there's none.</summary>
        public static RouletteTuning Current {
            get {
                if (current == null) current = Resources.Load<RouletteTuning>(ResourcePath);
                if (current == null) current = CreateInstance<RouletteTuning>();
                return current;
            }
        }

        // A slider moved: the wheel showing takes it at once
        private void OnValidate() => Tooltip.Existing?.RedrawRoulette();

        /// <summary>The values it shipped with (the ones written above), kept whatever the sliders say.</summary>
        public void RestoreOriginals() {
            var originals = CreateInstance<RouletteTuning>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(originals), this);
            DestroyImmediate(originals);
            Tooltip.Existing?.RedrawRoulette();
        }

#if UNITY_EDITOR
        private const string RememberedKey = "SlayTheClone.RouletteTuning.Remembered";

        /// <summary>A snapshot of the sliders, kept in the editor's settings: a set to come back to after experimenting.</summary>
        public void Remember() => UnityEditor.EditorPrefs.SetString(RememberedKey, JsonUtility.ToJson(this));
        public bool HasRemembered => UnityEditor.EditorPrefs.HasKey(RememberedKey);
        public void BackToRemembered() {
            if (!HasRemembered) return;
            JsonUtility.FromJsonOverwrite(UnityEditor.EditorPrefs.GetString(RememberedKey), this);
            Tooltip.Existing?.RedrawRoulette();
        }

        /// <summary>Selects the tuning asset (made with the defaults if there's none) so its sliders show in the Inspector.</summary>
        [UnityEditor.MenuItem("UI/Roulette Tuning")]
        private static void Select() {
            const string path = "Assets/Resources/" + ResourcePath + ".asset";
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<RouletteTuning>(path);
            if (asset == null) {
                asset = CreateInstance<RouletteTuning>();
                UnityEditor.AssetDatabase.CreateAsset(asset, path);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            current = asset;
            UnityEditor.Selection.activeObject = asset;
            UnityEditor.EditorGUIUtility.PingObject(asset);
        }
#endif
    }
#if UNITY_EDITOR
    /// <summary>
    /// The sliders, with a safety net: back to the original values at any time, or to a set remembered earlier. What's
    /// changed while playing stays (it's an asset, not the scene), and every change can be undone (Ctrl+Z).
    /// </summary>
    [UnityEditor.CustomEditor(typeof(RouletteTuning))]
    public class RouletteTuningEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var tuning = (RouletteTuning)target;
            UnityEditor.EditorGUILayout.HelpBox("Changes show on the wheel at once while playing, and they stay when you stop: " +
                "this is an asset, not the scene. Ctrl+Z undoes a change. UI > Roulette Gallery turns the wheel for you.", UnityEditor.MessageType.Info);
            using (new UnityEditor.EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Restore the original values")) {
                    UnityEditor.Undo.RecordObject(tuning, "Restore the original roulette values");
                    tuning.RestoreOriginals();
                    UnityEditor.EditorUtility.SetDirty(tuning);
                }
                if (GUILayout.Button("Remember these values")) tuning.Remember();
                using (new UnityEditor.EditorGUI.DisabledScope(!tuning.HasRemembered)) {
                    if (GUILayout.Button("Back to the remembered ones")) {
                        UnityEditor.Undo.RecordObject(tuning, "Back to the remembered roulette values");
                        tuning.BackToRemembered();
                        UnityEditor.EditorUtility.SetDirty(tuning);
                    }
                }
            }
            UnityEditor.EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
#endif
}
