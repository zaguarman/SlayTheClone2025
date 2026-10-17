using DG.Tweening;
using UnityEngine;

namespace Zeitgeist {
    /// <summary>
    /// Every measurement of the card roulette, as sliders: the wheel of cards that turns when you scroll over a creature with
    /// cards tucked under it (see <see cref="Tooltip"/>). Select it with UI > Roulette Tuning and drag them while the game
    /// plays; the wheel showing redraws as they change. Kept in Resources/RouletteTuning, so what's set there is what the
    /// game uses. UI > Roulette Gallery turns the wheel on its own, to judge them.
    /// Every position is in preview heights (the enlarged card's height on screen), so it holds at any screen size; each
    /// slider has its own reset button.
    /// </summary>
    public class RouletteTuning : ScriptableObject {
        public const string ResourcePath = "RouletteTuning";

        [Header("The wheel")]
        [Tooltip("Degrees between two cards on the wheel")]
        [Range(5f, 90f)] public float angle = 25f;
        [Tooltip("The wheel's radius, in preview heights: how far below the shown card its center is (bigger: a flatter arc)")]
        [Range(0.3f, 5f)] public float radius = 1.45f;
        [Tooltip("Cards shown faint on the wheel each side of the shown one")]
        [Range(0, 6)] public int reach = 2;

        [Header("A turn")]
        [Tooltip("Seconds a turn of the wheel takes (one scroll notch)")]
        [Range(0.05f, 1.5f)] public float seconds = 0.28f;
        [Tooltip("How the turn eases in and out")]
        public Ease ease = Ease.OutCubic;

        [Header("The shown card: moves it alone, the waiting cards stay where they are")]
        [Tooltip("Its size, of the preview's (Preview Scale): bigger or smaller than the waiting cards would make it, while they and the wheel stay as they are")]
        [Range(0.5f, 2f)] public float shownScale = 1f;
        [Tooltip("Moves the shown card sideways, in preview heights (negative: left); the waiting cards stay")]
        [Range(-1f, 1f)] public float shownSideways = 0f;
        [Tooltip("Moves the shown card up, in preview heights (negative: down); the waiting cards stay")]
        [Range(-1f, 1f)] public float shownUp = -0.32f;

        [Header("The cards waiting on the wheel: where they sit")]
        [Tooltip("Their size, of the shown card's")]
        [Range(0.3f, 1.3f)] public float waitingScale = 1f;
        [Tooltip("How far out to the sides they move, in preview heights per place on the wheel (negative: in)")]
        [Range(-1f, 1f)] public float waitingOut = 0.06f;
        [Tooltip("Moves them all up by the same amount, in preview heights (negative: down)")]
        [Range(-1f, 1f)] public float waitingLift = 0f;
        [Tooltip("Moves them up more the further out they are, in preview heights per place on the wheel (negative: down): bends the arc")]
        [Range(-1f, 1f)] public float waitingUp = 0.1f;
        [Tooltip("How much they lean with the wheel: 1 follows it, 0 stays upright")]
        [Range(0f, 1.5f)] public float waitingTilt = 0.5f;

        [Header("The cards waiting on the wheel: how faint")]
        [Tooltip("Both sides alike, in even steps: each card a place further out is Fade Step fainter (0.3: 70%, then 40%, then 10%...). Off: the four sliders below")]
        public bool mirrorFade = true;
        [Tooltip("Mirror Fade: how much fainter each card is than the one before it, both sides (0.3: 30% each place)")]
        [Range(0f, 1f)] public float fadeStep = 0.47f;
        [Tooltip("The next card to come (on the left): its opacity")]
        [Range(0f, 1f)] public float nextAlpha = 0.265f;
        [Tooltip("Each card further ahead: its opacity times this (1: all as clear as the next one)")]
        [Range(0f, 1f)] public float aheadFade = 0.354f;
        [Tooltip("The card just gone by (on the right): its opacity")]
        [Range(0f, 1f)] public float lastAlpha = 0.46f;
        [Tooltip("Each card further back: its opacity times this (1: all as clear as the last one)")]
        [Range(0f, 1f)] public float behindFade = 0.372f;

        [Header("The preview (any enlarged card on the table)")]
        [Tooltip("The enlarged card, in card sizes (always well bigger than the card as shown)")]
        [Range(1f, 3.5f)] public float previewScale = 2f;
        [Tooltip("Between the card and its preview, in preview heights (negative: overlapping)")]
        [Range(-0.3f, 0.3f)] public float previewGap = 0.02f;
        [Tooltip("Moves it sideways, in preview heights (negative: left)")]
        [Range(-1f, 1f)] public float previewSideways = 0f;
        [Tooltip("Moves it away from the card, in preview heights (negative: toward it)")]
        [Range(-1f, 1f)] public float previewAway = 0f;

        [Header("A board card lifted (the user, 2026-10-17: hovered, the card rises over its own slot, bigger, and follows the pointer a little)")]
        [Tooltip("How much bigger than the card in its slot")]
        [Range(1f, 2.5f)] public float liftScale = 1.5f;
        [Tooltip("How far up it rises, in heights of the card in its slot")]
        [Range(0f, 1f)] public float liftUp = 0.25f;
        [Tooltip("How much of the pointer's distance from the card's middle it follows (0: it stays put)")]
        [Range(0f, 0.5f)] public float liftFollow = 0.12f;
        [Tooltip("The furthest it strays after the pointer, in heights of the card in its slot")]
        [Range(0f, 1f)] public float liftReach = 0.25f;

        [Header("The details box beside the preview")]
        [Tooltip("Its width, in screen pixels")]
        [Range(150f, 600f)] public float detailsWidth = 269f;
        [Tooltip("Its text size")]
        [Range(10f, 30f)] public float detailsFontSize = 16f;
        [Tooltip("Between the preview and the box, in preview heights (negative: overlapping)")]
        [Range(-0.3f, 0.3f)] public float detailsGap = 0.02f;
        [Tooltip("Moves it down from the preview's top, in preview heights (negative: up)")]
        [Range(-1f, 1f)] public float detailsDown = 0f;

        /// <summary>How faint a card is on the wheel, this many places ahead of the shown one (or behind it).</summary>
        public float AlphaAt(int distance, bool ahead) =>
            mirrorFade ? Mathf.Max(0f, 1f - fadeStep * distance)
            : ahead ? nextAlpha * Mathf.Pow(aheadFade, distance - 1) : lastAlpha * Mathf.Pow(behindFade, distance - 1);

        // The sliders that place the shown card: back to these, it sits where it started, right by its card
        public static readonly string[] Placing = { nameof(previewGap), nameof(previewSideways), nameof(previewAway), nameof(shownSideways), nameof(shownUp) };

        /// <summary>A waiting card's size, of the shown card's: Waiting Scale, against Shown Scale.</summary>
        public float WaitingOfShown => waitingScale / Mathf.Max(0.01f, shownScale);

        // The shown card's nudge from its place on the wheel (sideways is to the right of up)
        public Vector3 ShownOffset(Vector3 up, float previewHeight) => (new Vector3(up.y, -up.x, 0f) * shownSideways + up * shownUp) * previewHeight;

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
        // Asked for, not done here: inside the Inspector, the screen it'd measure is the Inspector's (the roulette slid left)
        private void OnValidate() => Tooltip.Existing?.RequestRedraw();

        /// <summary>The values it shipped with (the ones written above), kept whatever the sliders say.</summary>
        public void RestoreOriginals() {
            var originals = CreateInstance<RouletteTuning>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(originals), this);
            DestroyImmediate(originals);
            Tooltip.Existing?.RequestRedraw();
        }

#if UNITY_EDITOR
        private const string RememberedKey = "Zeitgeist.RouletteTuning.Remembered";

        /// <summary>A snapshot of the sliders, kept in the editor's settings: a set to come back to after experimenting.</summary>
        public void Remember() => UnityEditor.EditorPrefs.SetString(RememberedKey, JsonUtility.ToJson(this));
        public bool HasRemembered => UnityEditor.EditorPrefs.HasKey(RememberedKey);
        public void BackToRemembered() {
            if (!HasRemembered) return;
            JsonUtility.FromJsonOverwrite(UnityEditor.EditorPrefs.GetString(RememberedKey), this);
            Tooltip.Existing?.RequestRedraw();
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
        private RouletteTuning originals; // The values it shipped with, for each slider's reset button

        private void OnEnable() => originals = CreateInstance<RouletteTuning>();
        private void OnDisable() => DestroyImmediate(originals);

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
            if (GUILayout.Button("Center the shown card: back where it started, right by its card")) {
                // Only the sliders that place it: the wheel, the fades and the rest stay as tuned
                serializedObject.Update();
                var shipped = new UnityEditor.SerializedObject(originals);
                foreach (string field in RouletteTuning.Placing) serializedObject.CopyFromSerializedProperty(shipped.FindProperty(field));
                serializedObject.ApplyModifiedProperties();
            }
            UnityEditor.EditorGUILayout.Space();
            TuningInspector.DrawWithResets(serializedObject, originals);
        }
    }
#endif
}
