using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// The battlefields' size, form and position, as sliders (the user): select it with UI > Battlefield Tuning and drag
    /// them while the game plays; both mats, their slots and the chevron round them redraw as they change. Kept in
    /// Resources/BattlefieldTuning, so what's set there is what the game uses. UI > Battlefield Gallery fills the boards
    /// with cards to judge them. Lengths are in board units: a slot is 172 wide and 212 tall before Slot Scale.
    /// </summary>
    public class BattlefieldTuning : ScriptableObject {
        public const string ResourcePath = "BattlefieldTuning";

        [Header("Size")]
        [Tooltip("The slots and the cards on them, of a card in the hand (hovering enlarges)")]
        [Range(0.5f, 1.4f)] public float slotScale = 0.9f;
        [Tooltip("How far apart the formation's columns are, of the board's own spacing")]
        [Range(0.6f, 1.8f)] public float columnSpacing = 1f;

        [Header("Form: the formation")]
        [Tooltip("How far apart its rows are, up and down: the Vanguard nearest the middle of the table, Headquarters furthest (0: one straight row; negative: the other way round)")]
        [Range(-120f, 250f)] public float rowStep = 64f;

        [Header("Form: the mat's chevron round it")]
        [Tooltip("Room between the slots and the mat's edge, at the back and the wingtips")]
        [Range(0f, 150f)] public float matPadding = 30f;
        [Tooltip("Room round the Vanguard, at the chevron's nose")]
        [Range(0f, 100f)] public float noseRoom = 16f;
        [Tooltip("How wide the nose is, in Nose Rooms either side of the Vanguard (more: blunter)")]
        [Range(0f, 4f)] public float noseWidth = 1.6f;
        [Tooltip("The trailing edge's tuck behind the Flanks, in Mat Paddings (0: tight to the cards)")]
        [Range(0f, 2f)] public float tuckGap = 0.5f;

        [Header("Position")]
        [Tooltip("How far back from the middle of the table the formation sits: the two noses keep this far apart, each side")]
        [Range(-80f, 250f)] public float riverRoom = 26f;
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

        // A slider moved: the mats on the table take it at once
        private void OnValidate() => ApplyToTable();

        /// <summary>Lays both battlefields out again with the sliders as they are: mats, slots, markers, chevrons, the table round them.</summary>
        public static void ApplyToTable() {
            if (!Application.isPlaying) return;
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

        private void OnEnable() => originals = CreateInstance<BattlefieldTuning>();
        private void OnDisable() => DestroyImmediate(originals);

        public override void OnInspectorGUI() {
            var tuning = (BattlefieldTuning)target;
            UnityEditor.EditorGUILayout.HelpBox("Changes show on the table at once while playing, and they stay when you stop: " +
                "this is an asset, not the scene. Ctrl+Z undoes a change. UI > Battlefield Gallery fills the boards with cards.", UnityEditor.MessageType.Info);
            using (new UnityEditor.EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Restore the original values")) {
                    UnityEditor.Undo.RecordObject(tuning, "Restore the original battlefield values");
                    tuning.RestoreOriginals();
                    UnityEditor.EditorUtility.SetDirty(tuning);
                }
                if (GUILayout.Button("Remember these values")) tuning.Remember();
                using (new UnityEditor.EditorGUI.DisabledScope(!tuning.HasRemembered)) {
                    if (GUILayout.Button("Back to the remembered ones")) {
                        UnityEditor.Undo.RecordObject(tuning, "Back to the remembered battlefield values");
                        tuning.BackToRemembered();
                        UnityEditor.EditorUtility.SetDirty(tuning);
                    }
                }
            }
            UnityEditor.EditorGUILayout.Space();
            TuningInspector.DrawWithResets(serializedObject, originals);
        }
    }
#endif
}
