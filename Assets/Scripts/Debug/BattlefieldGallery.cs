using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// The battlefields set out to judge the <see cref="BattlefieldTuning"/> sliders: shots of the boards empty, full, half
    /// full, and seen from the other seat (S), one at a time; Back and Next step through them (UI > Battlefield Gallery).
    /// It fills both rows with plain creatures: use a game you don't mind losing.
    /// </summary>
    public class BattlefieldGallery : MonoBehaviour {
        public static BattlefieldGallery Instance { get; private set; }
        public static bool Running => Instance != null;

        private static readonly (string Name, int Yours, int Theirs, bool OtherSeat, bool Deploying)[] Shots = {
            ("Both rows full", 5, 5, false, false), ("Both rows empty", 0, 0, false, false), ("Your row full, theirs empty", 5, 0, false, false),
            ("The Vanguards and Flanks only", 3, 3, false, false), ("Both rows full, from the other seat (S)", 5, 5, true, false),
            ("Still deploying, all but the Vanguards", 5, 5, false, true)
        };

        public int Index { get; private set; }
        public int Count => Shots.Length;
        public string Current => Shots[Index].Name;

        private GameManager game;
        private TMPro.TextMeshProUGUI label;
        private readonly List<ICreature> placed = new List<ICreature>();
        private bool seatBefore;

        /// <summary>Starts it on the table.</summary>
        public static void Run() {
            if (Running || !Application.isPlaying) return;
            new GameObject("BattlefieldGallery").AddComponent<BattlefieldGallery>();
        }

        public static void Stop() {
            if (Instance != null) Destroy(Instance.gameObject);
        }

        /// <summary>Goes this many shots on (back when negative).</summary>
        public void Jump(int by) {
            Index = ((Index + by) % Shots.Length + Shots.Length) % Shots.Length;
            Show();
        }

        private void Awake() => Instance = this;

        private void Start() {
            game = FindFirstObjectByType<GameManager>();
            var references = FindFirstObjectByType<GameReferences>();
            if (game == null || game.Player1 == null || references == null) {
                Debug.LogWarning("Battlefield Gallery: start a game first (Play in the game scene)");
                Destroy(gameObject);
                return;
            }
            MatchSetup.AI = AISeats.None; // Nobody plans anything meanwhile
            seatBefore = FogOfWar.PlaysAsPlayer1;
            foreach (var play in game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>().ToList()) game.ActionsQueue.CancelPlay(play.GetCard());
            label = ArrowGallery.GalleryLabel(references.Player1References.BattlefieldUI);
            Show();
        }

        private void OnDestroy() {
            if (Instance == this) Instance = null;
            if (label != null) Destroy(label.gameObject);
            BattlefieldTuning.SetHandsHidden(false);
            if (game != null) FogOfWar.PlaysAsPlayer1 = seatBefore;
        }

        // The shot's creatures, the Vanguard first, then the Flanks, then Headquarters, on each side
        private void Show() {
            var shot = Shots[Index];
            label.text = shot.Name;
            FogOfWar.PlaysAsPlayer1 = shot.OtherSeat ? !seatBefore : seatBefore;
            foreach (var side in new[] { game.Player1, game.Player2 }) {
                foreach (var slot in side.Battlefield) {
                    if (slot.OccupyingCreature != null) game.ActionsQueue.AddAction(new DestroyCreatureAction(slot.OccupyingCreature, unconditional: true));
                }
            }
            game.ActionsQueue.ResolveActions();
            // Deploying: the Flanks for a Day more, Headquarters for two (an hourglass over the art, the card faded)
            int Days(int slot) => shot.Deploying ? Formation.Depth(slot) : 0;
            for (int i = 0; i < shot.Yours; i++) ArrowGallery.Summon(game, game.Player1, i, $"Yours {i}", Days(i));
            for (int i = 0; i < shot.Theirs; i++) ArrowGallery.Summon(game, game.Player2, i, $"Theirs {i}", Days(i));
        }
    }

#if UNITY_EDITOR
    /// <summary>The gallery's controls (UI > Battlefield Gallery): back, next, stop; the sliders are in UI > Battlefield Tuning.</summary>
    public class BattlefieldGalleryWindow : UnityEditor.EditorWindow {
        // Opening it starts it: Play is pressed if needed, and it begins once the game is up
        [UnityEditor.MenuItem("UI/Battlefield Gallery")]
        private static void Open() {
            var window = GetWindow<BattlefieldGalleryWindow>("Battlefield Gallery");
            window.minSize = new Vector2(300f, 160f);
            GalleryLauncher.Start(GalleryLauncher.Battlefields);
        }

        private void OnInspectorUpdate() {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI() {
            var gallery = BattlefieldGallery.Instance;
            if (gallery == null) {
                if (GalleryLauncher.Waiting == GalleryLauncher.Battlefields) {
                    UnityEditor.EditorGUILayout.HelpBox("Starting the game: the gallery begins once it's up.", UnityEditor.MessageType.Info);
                    if (GUILayout.Button("Cancel")) GalleryLauncher.Cancel();
                    return;
                }
                if (GUILayout.Button("Start", GUILayout.Height(28f))) GalleryLauncher.Start(GalleryLauncher.Battlefields);
                UnityEditor.EditorGUILayout.HelpBox("Start presses Play if needed (the game scene must be open). It fills the board with test creatures: use a game you don't mind losing.", UnityEditor.MessageType.None);
                return;
            }
            UnityEditor.EditorGUILayout.LabelField($"{gallery.Index + 1} / {gallery.Count}", gallery.Current);
            using (new UnityEditor.EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("< Back")) gallery.Jump(-1);
                if (GUILayout.Button("Next >")) gallery.Jump(1);
            }
            if (GUILayout.Button(BattlefieldTuning.HandsHidden ? "Show the hands" : "Hide the hands")) BattlefieldTuning.SetHandsHidden(!BattlefieldTuning.HandsHidden);
            if (GUILayout.Button("Battlefield Tuning")) UnityEditor.EditorApplication.ExecuteMenuItem("UI/Battlefield Tuning");
            if (GUILayout.Button("Stop")) BattlefieldGallery.Stop();
            UnityEditor.EditorGUILayout.HelpBox("Move the sliders in UI > Battlefield Tuning: the mats redraw as they change.", UnityEditor.MessageType.None);
        }
    }
#endif
}
