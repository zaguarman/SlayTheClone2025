using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Every measurement of the battle arrows (see <see cref="ArrowIndicator"/>), as sliders: select it with UI > Arrow
    /// Tuning and drag them while the game plays, the arrows on the table redraw as they change. Kept in
    /// Resources/ArrowTuning, so what's set there is what the game uses; Reset (the asset's menu) brings the defaults back.
    /// Lengths are in world units: a card on the board is about 1.6 wide and 1.96 tall, the rows 1.26 apart.
    /// </summary>
    public class ArrowTuning : ScriptableObject {
        public const string ResourcePath = "ArrowTuning";

        [Header("Band widths, where the band meets the head")]
        [Range(0.04f, 0.5f)] public float attackWidth = 0.2f;
        [Range(0.04f, 0.5f)] public float damageWidth = 0.16f;
        [Range(0.04f, 0.5f)] public float spellWidth = 0.17f;
        [Range(0.04f, 0.5f)] public float moveWidth = 0.14f;
        [Range(0.04f, 0.5f)] public float dragWidth = 0.18f;
        [Tooltip("The band at its tail, as a share of its width")]
        [Range(0.2f, 1.2f)] public float tailWidth = 0.7f;
        [Tooltip("The bright line round the band")]
        [Range(0f, 0.08f)] public float rimWidth = 0.022f;
        [Tooltip("The thin dark line outside it")]
        [Range(0f, 0.12f)] public float outlineWidth = 0.045f;

        [Header("Curvature: how far a lone arrow bows to its right, as a share of its length")]
        [Range(0f, 0.4f)] public float attackBow = 0.08f;
        [Range(0f, 0.4f)] public float damageBow = 0.08f;
        [Range(0f, 0.4f)] public float spellBow = 0.1f;
        [Range(0f, 0.4f)] public float moveBow = 0.2f;
        [Range(0f, 0.4f)] public float dragBow = 0.1f;
        [Tooltip("Scales every arrow's cap on how far it can bow")]
        [Range(0f, 3f)] public float maxBowScale = 1f;
        [Tooltip("Attacks keep this far to the right of where they go, so opposite ones pass side by side")]
        [Range(0f, 0.6f)] public float attackLane = 0.22f;

        [Header("Sharpest curves: arrows routed into a shared one, like PCB tracks")]
        [Tooltip("The diagonal's angle from straight ahead (45: a true diagonal; more: flatter, sharper turns)")]
        [Range(15f, 80f)] public float routeAngle = 45f;
        [Tooltip("How much each corner is rounded off")]
        [Range(0.05f, 1.2f)] public float cornerRadius = 0.45f;
        [Tooltip("Round a tight bend the band is never wider than this share of its radius")]
        [Range(0.3f, 1f)] public float bendRoom = 0.9f;

        [Header("Fork (Spray and Pray): where the trunk splits into prongs")]
        [Tooltip("As a share of the way to the nearest target")]
        [Range(0.1f, 0.8f)] public float forkAt = 0.35f;
        [Tooltip("The prongs' width, of the trunk's")]
        [Range(0.3f, 1.2f)] public float prongWidth = 0.7f;
        [Tooltip("How long a prong keeps going straight ahead before it bends (share of its rise)")]
        [Range(0f, 1f)] public float prongLeave = 0.45f;
        [Tooltip("How straight it comes into its target (share of its rise)")]
        [Range(0f, 1f)] public float prongArrive = 0.3f;

        [Header("Head")]
        [Tooltip("Its length, in band widths")]
        [Range(0.8f, 4f)] public float headLength = 1.9f;
        [Tooltip("Its half width, in band widths")]
        [Range(0.6f, 3f)] public float headHalfWidth = 1.35f;
        [Range(0.2f, 1.2f)] public float maxHeadLength = 0.62f;
        [Range(0.15f, 1f)] public float maxHeadHalfWidth = 0.46f;
        [Tooltip("How far back the barbs reach, in head lengths")]
        [Range(0f, 0.8f)] public float barbSweep = 0.28f;
        [Tooltip("The swallowtail's depth at the tail, in half widths")]
        [Range(0f, 2f)] public float notchDepth = 0.9f;

        [Header("Neck: where several arrows merge into one trunk")]
        [Tooltip("The trunk widens this much for each arrow that joins (in band widths)")]
        [Range(0f, 0.6f)] public float widthPerExtraBranch = 0.15f;
        [Tooltip("...up to this many band widths")]
        [Range(1f, 3f)] public float maxTrunkWidth = 1.35f;
        [Tooltip("A branch's width as it flows into the trunk, of the band's")]
        [Range(0.3f, 1.5f)] public float branchWidthAtMerge = 0.9f;
        [Tooltip("The trunk's length, as a share of the way to the target")]
        [Range(0.15f, 0.9f)] public float trunkShare = 0.5f;

        private static ArrowTuning current;

        /// <summary>The tuning in use: Resources/ArrowTuning, or the defaults when there's none.</summary>
        public static ArrowTuning Current {
            get {
                if (current == null) current = Resources.Load<ArrowTuning>(ResourcePath);
                if (current == null) current = CreateInstance<ArrowTuning>();
                return current;
            }
        }

        // A slider moved: the arrows on the table take it at once
        private void OnValidate() => ArrowIndicator.RedrawAll();

        /// <summary>The values it shipped with (the ones written above), kept whatever the sliders say.</summary>
        public void RestoreOriginals() {
            var originals = CreateInstance<ArrowTuning>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(originals), this);
            DestroyImmediate(originals);
            ArrowIndicator.RedrawAll();
        }

#if UNITY_EDITOR
        private const string RememberedKey = "SlayTheClone.ArrowTuning.Remembered";

        /// <summary>A snapshot of the sliders, kept in the editor's settings: a set to come back to after experimenting.</summary>
        public void Remember() => UnityEditor.EditorPrefs.SetString(RememberedKey, JsonUtility.ToJson(this));
        public bool HasRemembered => UnityEditor.EditorPrefs.HasKey(RememberedKey);
        public void BackToRemembered() {
            if (!HasRemembered) return;
            JsonUtility.FromJsonOverwrite(UnityEditor.EditorPrefs.GetString(RememberedKey), this);
            ArrowIndicator.RedrawAll();
        }

        /// <summary>Selects the tuning asset (made with the defaults if there's none) so its sliders show in the Inspector.</summary>
        [UnityEditor.MenuItem("UI/Arrow Tuning")]
        private static void Select() {
            const string path = "Assets/Resources/" + ResourcePath + ".asset";
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ArrowTuning>(path);
            if (asset == null) {
                asset = CreateInstance<ArrowTuning>();
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
    [UnityEditor.CustomEditor(typeof(ArrowTuning))]
    public class ArrowTuningEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var tuning = (ArrowTuning)target;
            UnityEditor.EditorGUILayout.HelpBox("Changes show on the table at once while playing, and they stay when you stop: " +
                "this is an asset, not the scene. Ctrl+Z undoes a change.", UnityEditor.MessageType.Info);
            using (new UnityEditor.EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Restore the original values")) {
                    UnityEditor.Undo.RecordObject(tuning, "Restore the original arrow values");
                    tuning.RestoreOriginals();
                    UnityEditor.EditorUtility.SetDirty(tuning);
                }
                if (GUILayout.Button("Remember these values")) tuning.Remember();
                using (new UnityEditor.EditorGUI.DisabledScope(!tuning.HasRemembered)) {
                    if (GUILayout.Button("Back to the remembered ones")) {
                        UnityEditor.Undo.RecordObject(tuning, "Back to the remembered arrow values");
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
