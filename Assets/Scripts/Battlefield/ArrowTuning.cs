using System.Linq;
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

        // Each tooltip says which arrows it changes and which Arrow Gallery set shows it (UI > Arrow Gallery)

        [Header("Band widths, where the band meets the head")]
        [Tooltip("Attack arrows: planned attacks, lone, merged and forked (gallery: Singles, Merging, Spray)")]
        [Range(0.04f, 0.5f)] public float attackWidth = 0.2f;
        [Tooltip("Damage arrows: a hit as it lands while the Day plays out, and other planned damage (gallery: Damage)")]
        [Range(0.04f, 0.5f)] public float damageWidth = 0.16f;
        [Tooltip("Move arrows: planned moves, and the creatures they push or swap (gallery: Moves)")]
        [Range(0.04f, 0.5f)] public float moveWidth = 0.14f;
        [Tooltip("The arrow that follows the pointer while you drag a creature (gallery: Drag)")]
        [Range(0.04f, 0.5f)] public float dragWidth = 0.18f;
        [Tooltip("Every arrow: the band at its tail, as a share of its width (gallery: any set)")]
        [Range(0.2f, 1.2f)] public float tailWidth = 0.7f;
        [Tooltip("Every arrow: the bright line round the band (gallery: any set)")]
        [Range(0f, 0.08f)] public float rimWidth = 0.022f;
        [Tooltip("Every arrow: the thin dark line outside it (gallery: any set)")]
        [Range(0f, 0.12f)] public float outlineWidth = 0.045f;

        [Header("Curvature: how far a lone arrow bows to its right, as a share of its length")]
        [Tooltip("A lone attack, not merged nor forked (gallery: Singles)")]
        [Range(0f, 0.4f)] public float attackBow = 0.08f;
        [Tooltip("Damage arrows (gallery: Damage)")]
        [Range(0f, 0.4f)] public float damageBow = 0.08f;
        [Tooltip("Move arrows (gallery: Moves)")]
        [Range(0f, 0.4f)] public float moveBow = 0.2f;
        [Tooltip("The drag arrow (gallery: Drag)")]
        [Range(0f, 0.4f)] public float dragBow = 0.1f;
        [Tooltip("Every lone arrow: scales the cap on how far it can bow; it shows on the long ones (gallery: Singles, Damage, Moves, Drag)")]
        [Range(0f, 3f)] public float maxBowScale = 1f;
        [Tooltip("Attacks, lone and merged, keep this far to the right of where they go, so opposite ones pass side by side; forks don't (gallery: Singles, Merging)")]
        [Range(0f, 0.6f)] public float attackLane = 0.22f;

        [Header("Start and end positions, along the arrow's way (both sides alike)")]
        [Tooltip("Attacks (lone, merged, forked): how far the tail starts forward of the attacker's center, toward the target (0.8: about the card's edge) (gallery: Singles, Merging, Spray)")]
        [Range(-1f, 2f)] public float attackStartY = 0.8f;
        [Tooltip("Attacks: how far the head stops back from the target's center, toward the attacker (0.99: the card's near edge) (gallery: Singles, Merging, Spray)")]
        [Range(-1f, 2f)] public float attackEndY = 0.99f;
        [Tooltip("Damage arrows: tail forward of the attacker's center (gallery: Damage)")]
        [Range(-1f, 2f)] public float damageStartY = 0.8f;
        [Tooltip("Damage arrows: head back from the target's center (gallery: Damage)")]
        [Range(-1f, 2f)] public float damageEndY = 0.99f;
        [Tooltip("Move arrows: tail forward of the moving creature's center (gallery: Moves)")]
        [Range(-1f, 2f)] public float moveStartY = 0.3f;
        [Tooltip("Move arrows: head back from the destination's center (gallery: Moves)")]
        [Range(-1f, 2f)] public float moveEndY = 0.42f;
        [Tooltip("The drag arrow: tail forward of the dragged creature's center (gallery: Drag)")]
        [Range(-1f, 2f)] public float dragStartY = 0.3f;
        [Tooltip("The drag arrow: head back from the pointer (0: right on it) (gallery: Drag)")]
        [Range(-1f, 2f)] public float dragEndY = 0f;
        [Tooltip("Every arrow: its tail moved sideways, to the right of where it's going (minus: to the left) (gallery: any set)")]
        [Range(-1f, 1f)] public float startX = 0f;
        [Tooltip("Every arrow: its head moved sideways, to the right of where it's going (minus: to the left) (gallery: any set)")]
        [Range(-1f, 1f)] public float endX = 0f;

        [Header("Transparency along the arrow, tail (left) to head (right)")]
        [Tooltip("Every arrow: the band's fill. Only the markers on top count (transparency); click under the bar to add one, up to 8. Black: see-through, white: solid (gallery: any set)")]
        public Gradient fillOpacity = Opacity((0f, 0.12f), (0.5f, 0.42f), (1f, 0.52f));
        [Tooltip("Every arrow: its bright rim and dark outline, the same way (gallery: any set)")]
        public Gradient edgeOpacity = Opacity((0f, 0.18f), (0.625f, 1f), (1f, 1f));
        [Tooltip("Every arrow: the head's fill (gallery: any set)")]
        [Range(0f, 1f)] public float headOpacity = 0.6f;

        [Header("Bones and bends")]
        [Tooltip("Every arrow: points the band is built from, per world unit. More: a smoother shape, round bends and corners without facets (gallery: any set, Merging's corners most)")]
        [Range(4f, 120f)] public float bonesPerUnit = 28.6f;
        [Tooltip("Lone arrows (attack, damage, move, drag): how far along the way the first bend sits, as a share of its length (a third: even) (gallery: Singles, Damage, Moves, Drag)")]
        [Range(0f, 1f)] public float singleFirstBend = 1f / 3f;
        [Tooltip("Lone arrows: ...and the last bend (never before the first) (gallery: Singles, Damage, Moves, Drag)")]
        [Range(0f, 1f)] public float singleLastBend = 2f / 3f;
        [Tooltip("Merged arrows (the trunk, and the routed branches when Direct Branches is off) and Spray's trunk: the first bend, as a share of the length (gallery: Merging, Spray)")]
        [UnityEngine.Serialization.FormerlySerializedAs("firstBend")]
        [Range(0f, 1f)] public float mergedFirstBend = 1f / 3f;
        [Tooltip("Merged arrows and Spray's trunk: ...and the last bend (never before the first). Spray's prongs have their own: Prong Leave and Prong Arrive (gallery: Merging, Spray)")]
        [UnityEngine.Serialization.FormerlySerializedAs("lastBend")]
        [Range(0f, 1f)] public float mergedLastBend = 2f / 3f;

        [Header("Sharpest curves: arrows routed into a shared one, like PCB tracks")]
        [Tooltip("Merged attacks: the branches' diagonal, its angle from straight ahead (45: a true diagonal; more: flatter, sharper turns) (gallery: Merging)")]
        [Range(15f, 80f)] public float routeAngle = 45f;
        [Tooltip("Merged attacks: how much each branch corner is rounded off (gallery: Merging)")]
        [Range(0.05f, 1.2f)] public float cornerRadius = 0.45f;
        [Tooltip("Every arrow, round its tightest bends: the band is never wider than this share of the bend's radius; it shows on merged branches (gallery: Merging)")]
        [Range(0.3f, 1f)] public float bendRoom = 0.9f;

        [Header("Fork (Spray and Pray): where the trunk splits into prongs")]
        [Tooltip("Forked attacks: where the trunk splits, as a share of the way to the nearest target (gallery: Spray)")]
        [Range(0.1f, 0.8f)] public float forkAt = 0.35f;
        [Tooltip("Forked attacks: the prongs' width, of the trunk's (gallery: Spray)")]
        [Range(0.3f, 1.2f)] public float prongWidth = 0.7f;
        [Tooltip("Forked attacks: how long a prong keeps going straight ahead before it bends (share of its rise) (gallery: Spray)")]
        [Range(0f, 1f)] public float prongLeave = 0.45f;
        [Tooltip("Forked attacks: how straight a prong comes into its target (share of its rise) (gallery: Spray)")]
        [Range(0f, 1f)] public float prongArrive = 0.3f;

        [Header("Head (every arrow)")]
        [Tooltip("Every arrow: the head's length, in band widths (gallery: any set)")]
        [Range(0.8f, 4f)] public float headLength = 1.9f;
        [Tooltip("Every arrow: the head's half width, in band widths (gallery: any set)")]
        [Range(0.6f, 3f)] public float headHalfWidth = 1.35f;
        [Tooltip("Every arrow: the longest a head gets, in world units (shows on wide bands: Merging's trunks, or a raised width)")]
        [Range(0.2f, 1.2f)] public float maxHeadLength = 0.62f;
        [Tooltip("Every arrow: the widest a head gets, in world units (shows on wide bands: Merging's trunks, or a raised width)")]
        [Range(0.15f, 1f)] public float maxHeadHalfWidth = 0.46f;
        [Tooltip("Every arrow: how far back the barbs reach, in head lengths (gallery: any set)")]
        [Range(0f, 0.8f)] public float barbSweep = 0.28f;
        [Tooltip("Every arrow: the swallowtail's depth at the tail, in half widths (gallery: any set)")]
        [Range(0f, 2f)] public float notchDepth = 0.9f;

        [Header("Neck: where several arrows merge into one trunk")]
        [Tooltip("Merged attacks: each branch runs straight from its creature to the meeting point, then turns up the trunk: no stub, no fixed angle. Where they meet: Trunk Share (height) and Meet Offset X (sideways). Off: branches take the routed diagonal (Route Angle, First and Last Bend) (gallery: Merging)")]
        public bool directBranches = false;
        [Tooltip("Merged attacks, Direct Branches: the meeting point moved sideways, to the right of the target (minus: left) (gallery: Merging)")]
        [Range(-2f, 2f)] public float meetOffsetX = 0f;
        [Tooltip("Merged attacks: the trunk widens this much for each arrow that joins, in band widths (gallery: Merging, 'All five' most)")]
        [Range(0f, 0.6f)] public float widthPerExtraBranch = 0.15f;
        [Tooltip("Merged attacks: ...up to this many band widths (gallery: Merging, 'All five')")]
        [Range(1f, 3f)] public float maxTrunkWidth = 1.35f;
        [Tooltip("Merged attacks: a branch's width as it flows into the trunk, of the band's (gallery: Merging)")]
        [Range(0.3f, 1.5f)] public float branchWidthAtMerge = 0.9f;
        [Tooltip("Merged attacks: the trunk's length, as a share of the way to the target. With Direct Branches it sets exactly where they meet (more: lower, nearer the attackers); routed, it's capped so the diagonals fit (gallery: Merging)")]
        [Range(0.15f, 0.9f)] public float trunkShare = 0.5f;

        // A gradient of transparency alone, these (position, opacity) markers along it; its colour stays white (the arrow's own)
        private static Gradient Opacity(params (float at, float alpha)[] markers) {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                markers.Select(m => new GradientAlphaKey(m.alpha, m.at)).ToArray());
            return gradient;
        }

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
