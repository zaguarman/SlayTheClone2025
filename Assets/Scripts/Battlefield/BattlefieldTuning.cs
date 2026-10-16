using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// The battlefields' size, form and position, as sliders (the user): select it with UI > Battlefield Tuning and drag
    /// them while the game plays; both mats, their slots and the chevron round them redraw as they change. Kept in
    /// Resources/BattlefieldTuning, so what's set there is what the game uses. UI > Battlefield Gallery fills the boards
    /// with cards to judge them. Every length is in board units, the same for all of them (the user): a nose and a tuck
    /// of the same width are the same size. A slot is 172 wide and 212 tall before Slot Scale.
    /// </summary>
    public class BattlefieldTuning : ScriptableObject {
        public const string ResourcePath = "BattlefieldTuning";

        [Header("Size")]
        [Tooltip("The slots and the cards on them, of a card in the hand (hovering enlarges)")]
        [Range(0.5f, 1.4f)] public float slotScale = 1.2f;
        [Tooltip("How far apart the formation's columns are, of the board's own spacing")]
        [Range(0.6f, 1.8f)] public float columnSpacing = 1.05f;

        [Header("Size: the slots, and the cards in them")]
        [Tooltip("A slot's width, of a card's (its frame and marker; the chevron keeps clear of it)")]
        [Range(0.6f, 1.6f)] public float slotWidth = 1.2f;
        [Tooltip("A slot's height, of a card's")]
        [Range(0.6f, 1.6f)] public float slotHeight = 1.2f;
        [Tooltip("A card in its slot, of the most that fits it (1: as big as it can be, up to the slot's markers)")]
        [Range(0.5f, 1.1f)] public float cardScale = 1f;

        [Header("Form: the formation")]
        [Tooltip("How far apart its rows are, up and down: the Vanguard nearest the middle of the table, Headquarters furthest (0: one straight row; negative: the other way round)")]
        [Range(-120f, 250f)] public float rowStep = 80f;

        [Header("Form: the mat's chevron round it")]
        [Tooltip("Room between the slots and the mat's edge, at the back and the wingtips")]
        [Range(0f, 150f)] public float matPadding = 30f;
        [Tooltip("Room round the Vanguard, at the chevron's nose")]
        [Range(0f, 100f)] public float noseRoom = 16f;
        [Tooltip("The nose's flat over the Vanguard, its whole length (as the Tuck Width: equal numbers, equal flats; small: a point)")]
        [Range(1f, 800f)] public float noseWidth = 200f;
        [Tooltip("The tucked-in flat behind the Flanks, its whole length (as the Nose Width; small: a point)")]
        [Range(1f, 1200f)] public float tuckWidth = 170f;
        [Tooltip("How far behind the Flanks' cards the tucked-in flat sits (negative: over them); only the flat moves, the wings stay")]
        [Range(-300f, 300f)] public float tuckGap = 0f;

        [Header("Form: the wing's shape")]
        [Tooltip("The wingtips reach this much further out, past Headquarters")]
        [Range(-60f, 200f)] public float tipOut = -2f;
        [Tooltip("A wingtip's front corner (on the leading edge) moves this much toward the middle of the table")]
        [Range(-100f, 200f)] public float tipFront = -5f;
        [Tooltip("A wingtip's back corner moves this much away from the middle")]
        [Range(-100f, 200f)] public float tipBack = 10f;
        [Tooltip("The wing's flap: its straight back edge, level, reaching this far in from the wingtip")]
        [Range(0f, 700f)] public float wingSize = 260f;

        [Header("Form: corner rounding, each corner its own (looks only: the shape the game uses stays the same)")]
        [Tooltip("The nose's two corners, over the Vanguard: the rounding's radius")]
        [Range(0f, 120f)] public float noseRounding = 0f;
        [Tooltip("The wingtips' front corners")]
        [Range(0f, 120f)] public float tipFrontRounding = 10f;
        [Tooltip("The wingtips' back corners")]
        [Range(0f, 120f)] public float tipBackRounding = 10f;
        [Tooltip("The corners tucked under Headquarters")]
        [Range(0f, 120f)] public float tuckRounding = 0f;
        [Tooltip("The two corners of the flat stretch behind the Flanks")]
        [Range(0f, 120f)] public float flatRounding = 0f;

        /// <summary>Each of the wing's ten corners' rounding, in its order round the edge (see ChevronGraphic.Around).</summary>
        public float[] Roundings => new[] { noseRounding, tipFrontRounding, tipBackRounding, tuckRounding, flatRounding, flatRounding, tuckRounding, tipBackRounding, tipFrontRounding, noseRounding };

        [Header("Position")]
        [Tooltip("How far back from the middle of the table the formation sits: the two noses keep this far apart, each side")]
        [Range(-80f, 250f)] public float riverRoom = 90f;
        [Tooltip("Moves both formations sideways on their mats (negative: left)")]
        [Range(-400f, 400f)] public float sideways = 0f;

        private static BattlefieldTuning current;

        /// <summary>The tuning in use: Resources/BattlefieldTuning, or the defaults when there's none.</summary>
        public static BattlefieldTuning Current {
            get {
                if (current == null) current = Resources.Load<BattlefieldTuning>(ResourcePath);
                if (current == null) current = CreateInstance<BattlefieldTuning>();
                return current;
            }
        }

        /// <summary>Whether both hands are hidden, to see the battlefields whole while tuning them (the user).</summary>
        public static bool HandsHidden { get; private set; }

        /// <summary>Hides both hands (or shows them again): they fade out and let the pointer through. Play mode only.</summary>
        public static void SetHandsHidden(bool hidden) {
            HandsHidden = hidden && Application.isPlaying;
            if (!Application.isPlaying) return;
            foreach (var hand in Object.FindObjectsByType<HandUI>(FindObjectsSortMode.None)) hand.RefreshVisibility(); // The enemy's stays hidden under Fog of War
        }

        // A slider moved while playing: the mats on the table take it (just after: not inside OnValidate, where Unity
        // won't have layouts redone). Without playing the inspector redraws the scene's table (see the editor below)
        private void OnValidate() {
            if (!Application.isPlaying) return;
#if UNITY_EDITOR
            if (applyPending) return;
            applyPending = true;
            UnityEditor.EditorApplication.delayCall += () => {
                applyPending = false;
                ApplyToTable();
            };
#else
            ApplyToTable();
#endif
        }

        private static bool applyPending;

        /// <summary>
        /// Lays both battlefields out again with the sliders as they are: mats, slots, markers, chevrons, the table round
        /// them. Without playing, in the editor, the open scene's table is drawn again with them (and the scene is to save).
        /// </summary>
        public static void ApplyToTable() {
            if (!Application.isPlaying) {
#if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) UIThemeBaker.RedressOpenTable();
#endif
                return;
            }
            foreach (var board in Object.FindObjectsByType<BattlefieldUI>(FindObjectsSortMode.None)) board.Retune();
            foreach (var skin in Object.FindObjectsByType<BoardSkin>(FindObjectsSortMode.None)) skin.RefreshLayout();
        }

        /// <summary>The values it shipped with (the ones written above), kept whatever the sliders say.</summary>
        public void RestoreOriginals() {
            var originals = CreateInstance<BattlefieldTuning>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(originals), this);
            DestroyImmediate(originals);
            ApplyToTable();
        }

#if UNITY_EDITOR
        private const string RememberedKey = "SlayTheClone.BattlefieldTuning.Remembered";

        /// <summary>A snapshot of the sliders, kept in the editor's settings: a set to come back to after experimenting.</summary>
        public void Remember() => UnityEditor.EditorPrefs.SetString(RememberedKey, JsonUtility.ToJson(this));
        public bool HasRemembered => UnityEditor.EditorPrefs.HasKey(RememberedKey);
        public void BackToRemembered() {
            if (!HasRemembered) return;
            JsonUtility.FromJsonOverwrite(UnityEditor.EditorPrefs.GetString(RememberedKey), this);
            ApplyToTable();
        }

        /// <summary>Selects the tuning asset (made with the defaults if there's none) so its sliders show in the Inspector.</summary>
        [UnityEditor.MenuItem("UI/Battlefield Tuning")]
        private static void Select() {
            const string path = "Assets/Resources/" + ResourcePath + ".asset";
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<BattlefieldTuning>(path);
            if (asset == null) {
                asset = CreateInstance<BattlefieldTuning>();
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
    /// The sliders, each with its own reset, with a safety net: back to the original values at any time, or to a set
    /// remembered earlier. What's changed while playing stays (it's an asset, not the scene); Ctrl+Z undoes a change.
    /// </summary>
    [UnityEditor.CustomEditor(typeof(BattlefieldTuning))]
    public class BattlefieldTuningEditor : UnityEditor.Editor {
        private BattlefieldTuning originals;

        private void OnEnable() {
            originals = CreateInstance<BattlefieldTuning>();
            UnityEditor.Undo.undoRedoPerformed += Redraw;
        }

        private void OnDisable() {
            UnityEditor.Undo.undoRedoPerformed -= Redraw;
            DestroyImmediate(originals);
        }

        // Without playing, the scene's table is drawn again once the sliders change (once per editor tick, while dragging)
        private bool redrawPending;
        private void Redraw() {
            if (Application.isPlaying || redrawPending) return;
            redrawPending = true;
            UnityEditor.EditorApplication.delayCall += () => {
                redrawPending = false;
                BattlefieldTuning.ApplyToTable();
            };
        }

        public override void OnInspectorGUI() {
            var tuning = (BattlefieldTuning)target;
            UnityEditor.EditorGUILayout.HelpBox("Changes show on the table at once while playing, and they stay when you stop: " +
                "this is an asset, not the scene. Ctrl+Z undoes a change. UI > Battlefield Gallery fills the boards with cards.", UnityEditor.MessageType.Info);
            using (new UnityEditor.EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Restore the original values")) {
                    UnityEditor.Undo.RecordObject(tuning, "Restore the original battlefield values");
                    tuning.RestoreOriginals();
                    UnityEditor.EditorUtility.SetDirty(tuning);
                    Redraw();
                }
                if (GUILayout.Button("Remember these values")) tuning.Remember();
                using (new UnityEditor.EditorGUI.DisabledScope(!tuning.HasRemembered)) {
                    if (GUILayout.Button("Back to the remembered ones")) {
                        UnityEditor.Undo.RecordObject(tuning, "Back to the remembered battlefield values");
                        tuning.BackToRemembered();
                        UnityEditor.EditorUtility.SetDirty(tuning);
                        Redraw();
                    }
                }
            }
            using (new UnityEditor.EditorGUI.DisabledScope(!Application.isPlaying)) {
                if (GUILayout.Button(BattlefieldTuning.HandsHidden ? "Show the hands" : "Hide the hands (to see the battlefields whole)")) {
                    BattlefieldTuning.SetHandsHidden(!BattlefieldTuning.HandsHidden);
                }
            }
            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUI.BeginChangeCheck();
            TuningInspector.DrawWithResets(serializedObject, originals);
            if (UnityEditor.EditorGUI.EndChangeCheck()) Redraw();
        }
    }
#endif
}
