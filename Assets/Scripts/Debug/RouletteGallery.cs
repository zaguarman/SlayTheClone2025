using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The card roulette turning on its own, to judge the <see cref="RouletteTuning"/> sliders: a creature with cards tucked
    /// under it shows its enlarged preview, and the wheel turns through its stack, one card at a time, round to the creature
    /// again; then the next shot. UI > Roulette Gallery plays it back while the game plays: the window sets how long each
    /// card stays, pauses (the wheel can still be turned by hand there), and steps back and forth; it loops until stopped.
    /// It fills both rows with plain creatures and tucks cards from both decks under them (see <see cref="Set"/>).
    /// It changes the game it runs on: use a game you don't mind losing.
    /// </summary>
    public class RouletteGallery : MonoBehaviour {
        /// <summary>What each set shows.</summary>
        public enum Set {
            Sizes,  // Stacks of 1 to 8 cards on your Vanguard: the wheel's reach, how faint the waiting cards are
            Hidden, // Enemy spells on their way among them, showing their backs
            Places  // A stack of 3 on each slot, both sides: where the preview and its wheel sit
        }

        // Set from the window before it starts; how long each card stays can change while it runs
        public static float SecondsPerTurn = 1.2f;
        public static readonly HashSet<Set> Chosen = new HashSet<Set>((Set[])System.Enum.GetValues(typeof(Set)));

        public static RouletteGallery Instance { get; private set; }
        public static bool Running => Instance != null;

        public bool Paused { get; set; }
        public int Index { get; private set; }
        public int Count => shots.Count;
        public string Current => Index < shots.Count ? $"{shots[Index].Set}: {shots[Index].Name}" : "Setting up...";

        private class Shot {
            public Set Set;
            public string Name;
            public bool Yours;   // Your side's slot, or the enemy's
            public int Slot;
            public int Tucked;
            public int[] Hidden; // Which of the tucked cards are enemy spells on their way (their backs show), from 0
        }

        private GameManager game;
        private GameReferences references;
        private TMPro.TextMeshProUGUI label;
        private readonly List<Shot> shots = new List<Shot>();
        private readonly List<(ICard Card, IPlayer Owner)> pool = new List<(ICard, IPlayer)>(); // Cards to tuck, from both decks
        private CardController shown; // The creature whose stack shows now
        private int jump;   // Previous / Next pressed
        private int turns;  // Turns of the wheel asked for by hand

        /// <summary>Plays the roulette back, looping.</summary>
        public static void Run() {
            if (Running || !Application.isPlaying) return;
            new GameObject("RouletteGallery").AddComponent<RouletteGallery>();
        }

        public static void Stop() {
            if (Instance != null) Destroy(Instance.gameObject);
        }

        /// <summary>Goes this many shots on (back when negative), now.</summary>
        public void Jump(int by) => jump += by;

        /// <summary>Turns the wheel this many cards on (back when negative), now.</summary>
        public void Turn(int by) => turns += by;

        private void Awake() => Instance = this;

        private void OnDestroy() {
            if (Instance == this) Instance = null;
            Tooltip.Existing?.HideTooltip();
            Untuck();
            if (label != null) Destroy(label.gameObject);
        }

        private IEnumerator Start() {
            game = FindFirstObjectByType<GameManager>();
            references = FindFirstObjectByType<GameReferences>();
            if (game == null || game.Player1 == null || references == null) {
                Debug.LogWarning("Roulette Gallery: start a game first (Play in the game scene)");
                Destroy(gameObject);
                yield break;
            }
            MatchSetup.AI = AISeats.None; // Nobody plans anything else meanwhile
            Setup();
            BuildShots();
            yield return new WaitForSecondsRealtime(1f); // The summons pop in
            if (shots.Count == 0 || pool.Count == 0) {
                Debug.LogWarning(shots.Count == 0 ? "Roulette Gallery: no set chosen" : "Roulette Gallery: no cards in the decks to tuck");
                Destroy(gameObject);
                yield break;
            }

            Index = 0;
            while (true) {
                var shot = shots[Index];
                ShowShot(shot);
                // Once round the stack, back to the creature, a card at a time; by hand too, while paused
                int turned = 0;
                float waited = 0f;
                while (jump == 0 && (Paused || turned <= shot.Tucked)) {
                    if (turns != 0) {
                        Tooltip.Existing?.Browse(turns > 0 ? 1 : -1);
                        turns -= System.Math.Sign(turns);
                        waited = 0f;
                    }
                    else if (!Paused && (waited += Time.unscaledDeltaTime) >= SecondsPerTurn) {
                        waited = 0f;
                        if (++turned > shot.Tucked) break;
                        Tooltip.Existing?.Browse(1);
                    }
                    yield return null;
                }
                int next = Index + (jump != 0 ? jump : 1);
                jump = 0;
                Index = ((next % shots.Count) + shots.Count) % shots.Count; // It loops
            }
        }

        // Plain creatures in every slot, both sides, nothing planned, the cards to tuck, and the label in the top left corner
        private void Setup() {
            foreach (var play in game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>().ToList()) game.ActionsQueue.CancelPlay(play.GetCard());
            for (int i = 0; i < 5; i++) {
                if (game.Player1.Battlefield[i].OccupyingCreature == null) ArrowGallery.Summon(game, game.Player1, i, $"Yours {i}");
                if (game.Player2.Battlefield[i].OccupyingCreature == null) ArrowGallery.Summon(game, game.Player2, i, $"Theirs {i}");
            }
            foreach (var side in new[] { game.Player1, game.Player2 }) {
                foreach (var slot in side.Battlefield) {
                    if (slot.OccupyingCreature != null) game.ActionsQueue.CancelDeclaredAction(slot.OccupyingCreature.TargetId);
                }
            }
            // Spells first (most of what's tucked), yours and the enemy's taking turns
            var yours = game.Player1.Deck.GetDeckPreview().Concat(game.Player1.Hand).OrderBy(c => c is ICreature).ToList();
            var theirs = game.Player2.Deck.GetDeckPreview().Concat(game.Player2.Hand).OrderBy(c => c is ICreature).ToList();
            for (int i = 0; i < Mathf.Max(yours.Count, theirs.Count); i++) {
                if (i < yours.Count) pool.Add((yours[i], game.Player1));
                if (i < theirs.Count) pool.Add((theirs[i], game.Player2));
            }
            label = ArrowGallery.GalleryLabel(Board(game.Player1));
        }

        private void BuildShots() {
            if (Chosen.Contains(Set.Sizes)) {
                foreach (int n in new[] { 1, 2, 3, 5, 8 }) Add(Set.Sizes, $"{n} tucked under your Vanguard", true, 0, n);
            }
            if (Chosen.Contains(Set.Hidden)) {
                Add(Set.Hidden, "4 tucked, the 2nd and 4th enemy spells on their way", true, 0, 4, 1, 3);
                Add(Set.Hidden, "3 enemy spells on their way", true, 0, 3, 0, 1, 2);
            }
            if (Chosen.Contains(Set.Places)) {
                foreach (bool yours in new[] { true, false }) {
                    for (int slot = 0; slot < 5; slot++) {
                        Add(Set.Places, $"3 tucked under {(yours ? "your" : "the enemy's")} {ArrowGallery.SlotNames[slot]}", yours, slot, 3);
                    }
                }
            }
        }

        private void Add(Set set, string name, bool yours, int slot, int tucked, params int[] hidden) =>
            shots.Add(new Shot { Set = set, Name = name, Yours = yours, Slot = slot, Tucked = tucked, Hidden = hidden });

        private void ShowShot(Shot shot) {
            Tooltip.Existing?.HideTooltip();
            Untuck();
            turns = 0;
            label.text = $"{shot.Set}: {shot.Name}";
            var side = shot.Yours ? game.Player1 : game.Player2;
            shown = Board(side).FindSlotView(side.Battlefield[shot.Slot])?.OccupyingCard;
            if (shown == null) return;
            var stack = Enumerable.Range(0, shot.Tucked).Select(i => pool[i % pool.Count]).ToList();
            shown.AttachedCards = stack.Select(s => s.Card).ToList();
            shown.AttachedOwners = stack.Select((s, i) => shot.Hidden.Contains(i) ? game.Player2 : s.Owner).ToList();
            shown.AttachedHidden = stack.Select((s, i) => shot.Hidden.Contains(i)).ToList();
            Tooltip.Shared.Pin(shown);
        }

        // The creature shown last gets its own stack back (none: the gallery's creatures have nothing on them)
        private void Untuck() {
            if (shown == null) return;
            shown.AttachedCards = new List<ICard>();
            shown.AttachedOwners = new List<IPlayer>();
            shown.AttachedHidden = new List<bool>();
            shown = null;
        }

        private BattlefieldUI Board(IPlayer side) =>
            side == game.Player1 ? references.Player1References.BattlefieldUI : references.Player2References.BattlefieldUI;
    }

#if UNITY_EDITOR
    /// <summary>The gallery's controls (UI > Roulette Gallery): which sets, how long each card stays, pause, turn by hand, back, next.</summary>
    public class RouletteGalleryWindow : UnityEditor.EditorWindow {
        private const string Prefs = "SlayTheClone.RouletteGallery.";

        [UnityEditor.MenuItem("UI/Roulette Gallery")]
        private static void Open() {
            var window = GetWindow<RouletteGalleryWindow>("Roulette Gallery");
            window.minSize = new Vector2(320f, 280f);
        }

        private void OnEnable() {
            RouletteGallery.SecondsPerTurn = UnityEditor.EditorPrefs.GetFloat(Prefs + "Seconds", 1.2f);
            foreach (RouletteGallery.Set set in System.Enum.GetValues(typeof(RouletteGallery.Set))) {
                if (UnityEditor.EditorPrefs.GetBool(Prefs + set, true)) RouletteGallery.Chosen.Add(set);
                else RouletteGallery.Chosen.Remove(set);
            }
        }

        private void OnInspectorUpdate() {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI() {
            var gallery = RouletteGallery.Instance;
            UnityEditor.EditorGUILayout.LabelField("Each card", UnityEditor.EditorStyles.boldLabel);
            float seconds = UnityEditor.EditorGUILayout.Slider("Seconds on screen", RouletteGallery.SecondsPerTurn, 0.3f, 8f);
            if (!Mathf.Approximately(seconds, RouletteGallery.SecondsPerTurn)) {
                RouletteGallery.SecondsPerTurn = seconds;
                UnityEditor.EditorPrefs.SetFloat(Prefs + "Seconds", seconds);
            }

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Sets (chosen before it starts)", UnityEditor.EditorStyles.boldLabel);
            using (new UnityEditor.EditorGUI.DisabledScope(gallery != null)) {
                foreach (RouletteGallery.Set set in System.Enum.GetValues(typeof(RouletteGallery.Set))) {
                    bool on = UnityEditor.EditorGUILayout.ToggleLeft($"{set}: {Shows(set)}", RouletteGallery.Chosen.Contains(set));
                    if (on == RouletteGallery.Chosen.Contains(set)) continue;
                    if (on) RouletteGallery.Chosen.Add(set);
                    else RouletteGallery.Chosen.Remove(set);
                    UnityEditor.EditorPrefs.SetBool(Prefs + set, on);
                }
            }

            UnityEditor.EditorGUILayout.Space();
            if (!Application.isPlaying) {
                UnityEditor.EditorGUILayout.HelpBox("Press Play in the game scene, then Start. It fills the board with test creatures: use a game you don't mind losing.", UnityEditor.MessageType.Info);
                return;
            }
            if (gallery == null) {
                if (GUILayout.Button("Start", GUILayout.Height(28f))) RouletteGallery.Run();
                return;
            }
            UnityEditor.EditorGUILayout.LabelField($"{gallery.Index + 1} / {gallery.Count}", gallery.Current);
            using (new UnityEditor.EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("< Back")) gallery.Jump(-1);
                if (GUILayout.Button(gallery.Paused ? "Resume" : "Pause")) gallery.Paused = !gallery.Paused;
                if (GUILayout.Button("Next >")) gallery.Jump(1);
            }
            using (new UnityEditor.EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("< Turn back")) gallery.Turn(-1);
                if (GUILayout.Button("Turn on >")) gallery.Turn(1);
            }
            if (GUILayout.Button("Stop")) RouletteGallery.Stop();
            UnityEditor.EditorGUILayout.HelpBox("Pause on a shot, then move the sliders in UI > Roulette Tuning: the wheel redraws as they change. Turn it by hand to see a turn again.", UnityEditor.MessageType.None);
        }

        private static string Shows(RouletteGallery.Set set) => set switch {
            RouletteGallery.Set.Sizes => "stacks of 1 to 8: reach, fading",
            RouletteGallery.Set.Hidden => "enemy spells showing their backs",
            RouletteGallery.Set.Places => "each slot, both sides: placement",
            _ => ""
        };
    }
#endif
}
