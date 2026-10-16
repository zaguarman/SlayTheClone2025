using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The battle arrows from every slot to every slot, one side aiming (the other side is the mirror), shown one after the
    /// other to judge the <see cref="ArrowTuning"/> sliders. UI > Arrow Gallery plays them back while the game plays: the
    /// window sets how long each shot stays, pauses on one while the sliders move, and steps back and forth; it loops until
    /// stopped. It fills both rows with plain creatures; each set shows one kind of arrow (see <see cref="Set"/>).
    /// It changes the game it runs on: use a game you don't mind losing.
    /// </summary>
    public class ArrowGallery : MonoBehaviour {
        /// <summary>What each set shows, and so which sliders it tests (each slider's tooltip says its sets).</summary>
        public enum Set {
            Singles, // One attack, each attacker slot at each target slot: attack width, bow, lane; the head, tail and edges
            Merging, // Several attacks on one target: the neck (trunk width, merge, trunk length), routed branches (angle, corners)
            Spray,   // Spray and Pray's fork, each slot at each slot: fork point, prongs
            Damage,  // A damage arrow, each slot at each slot (a hit landing, planned damage): damage width and bow
            Moves,   // A move, each slot to each other slot of its own row: move width and bow
            Drag     // The arrow that follows the pointer, dragged from each slot onto each target: drag width and bow
        }

        public static readonly string[] SlotNames = { "Vanguard", "Left Flank", "Right Flank", "Left HQ", "Right HQ" };
        private const float SettleSeconds = 0.6f; // The old arrows fade out, the new ones draw in

        // Set from the window before it starts; how long each shot stays can change while it runs
        public static float SecondsPerShot = 2f;
        public static readonly HashSet<Set> Chosen = new HashSet<Set>((Set[])System.Enum.GetValues(typeof(Set)));

        public static ArrowGallery Instance { get; private set; }
        public static bool Running => Instance != null;

        public bool Paused { get; set; }
        public int Index { get; private set; }
        public int Count => shots.Count;
        public string Current => Index < shots.Count ? $"{shots[Index].Set}: {shots[Index].Name}" : "Setting up...";

        private class Shot {
            public Set Set;
            public string Name;
            public System.Action Show;
        }

        private GameManager game;
        private GameReferences references;
        private TMPro.TextMeshProUGUI label;
        private readonly List<ICreature> attackers = new List<ICreature>();
        private readonly List<Shot> shots = new List<Shot>();
        private readonly List<ArrowIndicator> drawn = new List<ArrowIndicator>(); // Arrows the gallery draws itself
        private int jump; // Previous / Next pressed

        /// <summary>Plays the arrows back, looping.</summary>
        public static void Run() {
            if (Running || !Application.isPlaying) return;
            new GameObject("ArrowGallery").AddComponent<ArrowGallery>();
        }

        public static void Stop() {
            if (Instance != null) Destroy(Instance.gameObject);
        }

        /// <summary>Goes this many shots on (back when negative), now.</summary>
        public void Jump(int by) => jump += by;

        private void Awake() => Instance = this;

        private void OnDestroy() {
            if (Instance == this) Instance = null;
            ClearDrawn();
            if (label != null) Destroy(label.gameObject);
            if (game != null) {
                CancelAll();
                foreach (var attacker in attackers) attacker.Ability = CreatureAbility.None;
            }
        }

        private IEnumerator Start() {
            game = FindFirstObjectByType<GameManager>();
            references = FindFirstObjectByType<GameReferences>();
            if (game == null || game.Player1 == null || references == null) {
                Debug.LogWarning("Arrow Gallery: start a game first (Play in the game scene)");
                Destroy(gameObject);
                yield break;
            }
            MatchSetup.AI = AISeats.None; // Nobody plans anything else meanwhile
            Setup();
            BuildShots();
            yield return new WaitForSecondsRealtime(1f); // The summons pop in
            if (shots.Count == 0) {
                Debug.LogWarning("Arrow Gallery: no set chosen");
                Destroy(gameObject);
                yield break;
            }

            Index = 0;
            while (true) {
                ShowShot(shots[Index]);
                yield return new WaitForSecondsRealtime(SettleSeconds);
                float shown = SettleSeconds;
                while ((Paused || shown < SecondsPerShot) && jump == 0) {
                    shown += Time.unscaledDeltaTime;
                    yield return null;
                }
                int next = Index + (jump != 0 ? jump : 1);
                jump = 0;
                Index = ((next % shots.Count) + shots.Count) % shots.Count; // It loops
            }
        }

        // Plain creatures in every slot, both sides, nothing else planned, and the label in the top left corner
        private void Setup() {
            foreach (var play in game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>().ToList()) game.ActionsQueue.CancelPlay(play.GetCard());
            for (int i = 0; i < 5; i++) {
                attackers.Add(Summon(game, game.Player1, i, $"Attacker {i}"));
                Summon(game, game.Player2, i, $"Target {i}");
            }
            CancelAll();
            label = GalleryLabel(Board(game.Player1));
        }

        /// <summary>A label in the screen's top left corner, at the table's size of text, over everything: what a gallery shows now.</summary>
        public static TMPro.TextMeshProUGUI GalleryLabel(BattlefieldUI board) {
            var canvas = board.GetComponentInParent<Canvas>().rootCanvas;
            var label = UITheme.NewText("GalleryLabel", canvas.transform, "", 40f, UITheme.Gold, TMPro.TextAlignmentOptions.TopLeft);
            label.transform.localScale = Vector3.one * (board.transform.lossyScale.x / canvas.transform.lossyScale.x); // The table's size of text
            label.fontStyle = TMPro.FontStyles.Bold;
            label.raycastTarget = false;
            UITheme.AddDropShadow(label);
            var top = label.gameObject.AddComponent<Canvas>();
            top.overrideSorting = true;
            top.sortingOrder = 2000;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(1400f, 80f);
            var cam = Camera.main;
            var corner = cam.ViewportToWorldPoint(new Vector3(0.02f, 0.97f, cam.nearClipPlane));
            rect.position = new Vector3(corner.x, corner.y, canvas.transform.position.z);
            return label;
        }

        private void BuildShots() {
            var pairs = from a in Enumerable.Range(0, 5) from t in Enumerable.Range(0, 5) select (a, t);
            if (Chosen.Contains(Set.Singles)) {
                foreach (var (a, t) in pairs) Add(Set.Singles, $"{SlotNames[a]} -> {SlotNames[t]}", () => Attack(a, t));
            }
            if (Chosen.Contains(Set.Merging)) {
                var groups = new (string name, int[] from)[] {
                    ("All five", new[] { 0, 1, 2, 3, 4 }), ("Both flanks", new[] { 1, 2 }), ("Both HQ", new[] { 3, 4 }),
                    ("Left side", new[] { 1, 3 }), ("Vanguard + Right HQ", new[] { 0, 4 })
                };
                foreach (var (name, from) in groups) {
                    for (int t = 0; t < 5; t++) {
                        int target = t;
                        Add(Set.Merging, $"{name} -> {SlotNames[t]}", () => { foreach (int a in from) Attack(a, target); });
                    }
                }
            }
            if (Chosen.Contains(Set.Spray)) {
                foreach (var (a, t) in pairs) Add(Set.Spray, $"{SlotNames[a]} -> {SlotNames[t]}", () => {
                    attackers[a].Ability = CreatureAbility.SprayAndPray;
                    Attack(a, t);
                });
            }
            if (Chosen.Contains(Set.Damage)) {
                foreach (var (a, t) in pairs) Add(Set.Damage, $"{SlotNames[a]} -> {SlotNames[t]}",
                    () => Draw(game.Player1.Battlefield[a], game.Player2.Battlefield[t], ArrowIndicator.Style.Damage, BattlefieldArrowManager.OurDamageColor));
            }
            if (Chosen.Contains(Set.Moves)) {
                foreach (var (a, t) in pairs.Where(p => p.a != p.t)) Add(Set.Moves, $"{SlotNames[a]} -> {SlotNames[t]}",
                    () => Draw(game.Player1.Battlefield[a], game.Player1.Battlefield[t], ArrowIndicator.Style.Move, BattlefieldArrowManager.MoveColor));
            }
            if (Chosen.Contains(Set.Drag)) {
                foreach (var (a, t) in pairs) Add(Set.Drag, $"{SlotNames[a]} onto {SlotNames[t]}",
                    () => Draw(game.Player1.Battlefield[a], game.Player2.Battlefield[t], ArrowIndicator.Style.Drag, BattlefieldArrowManager.OurAttackColor));
            }
        }

        private void Add(Set set, string name, System.Action show) => shots.Add(new Shot { Set = set, Name = name, Show = show });

        private void ShowShot(Shot shot) {
            CancelAll();
            ClearDrawn();
            foreach (var attacker in attackers) attacker.Ability = CreatureAbility.None;
            label.text = $"{shot.Set}: {shot.Name}";
            shot.Show();
        }

        private void Attack(int from, int to) => game.CombatHandler.HandleCreatureCombat(attackers[from], game.Player2.Battlefield[to]);

        // An arrow the game draws only at certain moments (a hit landing, a move, a drag), drawn here between two slots
        private void Draw(BoardSlot from, BoardSlot to, ArrowIndicator.Style style, Color color) {
            var fromView = Board(from.Owner).FindSlotView(from);
            var toView = Board(to.Owner).FindSlotView(to);
            if (fromView == null || toView == null) return;
            var arrow = ArrowIndicator.Create(Board(game.Player1).transform);
            arrow.SetStyle(style);
            arrow.SetColor(color);
            arrow.Show(fromView.transform.position, toView.transform.position, drawIn: true);
            drawn.Add(arrow);
        }

        private void ClearDrawn() {
            foreach (var arrow in drawn) {
                if (arrow != null) arrow.FadeOutAndDestroy();
            }
            drawn.Clear();
        }

        private BattlefieldUI Board(IPlayer side) =>
            side == game.Player1 ? references.Player1References.BattlefieldUI : references.Player2References.BattlefieldUI;

        /// <summary>A plain test creature put straight into a slot (2 Attack, 9 Health, no text).</summary>
        public static ICreature Summon(GameManager game, Player side, int slot, string name, int deployingDays = 0) {
            var data = ScriptableObject.CreateInstance<CreatureData>();
            data.name = data.cardName = name;
            data.turnsUntilOperative = deployingDays;
            data.attack = 2;
            data.health = 9;
            data.speed = 1;
            data.effects = new List<CardEffect>();
            var creature = (ICreature)CardFactory.CreateCard(data);
            game.ActionsQueue.AddAction(new SummonCreatureAction(creature, side, side.Battlefield[slot], fromDeck: true));
            game.ActionsQueue.ResolveActions();
            return creature;
        }

        private void CancelAll() {
            foreach (var side in new[] { game.Player1, game.Player2 }) {
                foreach (var slot in side.Battlefield) {
                    if (slot.OccupyingCreature != null) game.ActionsQueue.CancelDeclaredAction(slot.OccupyingCreature.TargetId);
                }
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// The gallery's controls (UI > Arrow Gallery): which sets, how long each shot stays, pause, back, next.
    /// </summary>
    public class ArrowGalleryWindow : UnityEditor.EditorWindow {
        private const string Prefs = "SlayTheClone.ArrowGallery.";

        // Opening it starts it: Play is pressed if needed, and it begins once the game is up
        [UnityEditor.MenuItem("UI/Arrow Gallery")]
        private static void Open() {
            var window = GetWindow<ArrowGalleryWindow>("Arrow Gallery");
            window.minSize = new Vector2(320f, 300f);
            GalleryLauncher.Start(GalleryLauncher.Arrows);
        }

        private void OnEnable() {
            ArrowGallery.SecondsPerShot = UnityEditor.EditorPrefs.GetFloat(Prefs + "Seconds", 2f);
            foreach (ArrowGallery.Set set in System.Enum.GetValues(typeof(ArrowGallery.Set))) {
                if (UnityEditor.EditorPrefs.GetBool(Prefs + set, true)) ArrowGallery.Chosen.Add(set);
                else ArrowGallery.Chosen.Remove(set);
            }
        }

        private void OnInspectorUpdate() {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI() {
            var gallery = ArrowGallery.Instance;
            UnityEditor.EditorGUILayout.LabelField("Each shot", UnityEditor.EditorStyles.boldLabel);
            float seconds = UnityEditor.EditorGUILayout.Slider("Seconds on screen", ArrowGallery.SecondsPerShot, 0.8f, 15f);
            if (!Mathf.Approximately(seconds, ArrowGallery.SecondsPerShot)) {
                ArrowGallery.SecondsPerShot = seconds;
                UnityEditor.EditorPrefs.SetFloat(Prefs + "Seconds", seconds);
            }

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Sets (chosen before it starts)", UnityEditor.EditorStyles.boldLabel);
            using (new UnityEditor.EditorGUI.DisabledScope(gallery != null)) {
                foreach (ArrowGallery.Set set in System.Enum.GetValues(typeof(ArrowGallery.Set))) {
                    bool on = UnityEditor.EditorGUILayout.ToggleLeft($"{set}: {Tests(set)}", ArrowGallery.Chosen.Contains(set));
                    if (on == ArrowGallery.Chosen.Contains(set)) continue;
                    if (on) ArrowGallery.Chosen.Add(set);
                    else ArrowGallery.Chosen.Remove(set);
                    UnityEditor.EditorPrefs.SetBool(Prefs + set, on);
                }
            }

            UnityEditor.EditorGUILayout.Space();
            if (gallery == null) {
                if (GalleryLauncher.Waiting == GalleryLauncher.Arrows) {
                    UnityEditor.EditorGUILayout.HelpBox("Starting the game: the gallery begins once it's up.", UnityEditor.MessageType.Info);
                    if (GUILayout.Button("Cancel")) GalleryLauncher.Cancel();
                    return;
                }
                if (GUILayout.Button("Start", GUILayout.Height(28f))) GalleryLauncher.Start(GalleryLauncher.Arrows);
                UnityEditor.EditorGUILayout.HelpBox("Start presses Play if needed (the game scene must be open). It fills the board with test creatures: use a game you don't mind losing.", UnityEditor.MessageType.None);
                return;
            }
            UnityEditor.EditorGUILayout.LabelField($"{gallery.Index + 1} / {gallery.Count}", gallery.Current);
            using (new UnityEditor.EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("< Back")) gallery.Jump(-1);
                if (GUILayout.Button(gallery.Paused ? "Resume" : "Pause")) gallery.Paused = !gallery.Paused;
                if (GUILayout.Button("Next >")) gallery.Jump(1);
            }
            if (GUILayout.Button("Stop")) ArrowGallery.Stop();
            UnityEditor.EditorGUILayout.HelpBox("Pause on a shot, then move the sliders in UI > Arrow Tuning: the arrows redraw as they change.", UnityEditor.MessageType.None);
        }

        private static string Tests(ArrowGallery.Set set) => set switch {
            ArrowGallery.Set.Singles => "attack width, bow, lane",
            ArrowGallery.Set.Merging => "neck, routed branches",
            ArrowGallery.Set.Spray => "fork and prongs",
            ArrowGallery.Set.Damage => "damage width, bow",
            ArrowGallery.Set.Moves => "move width, bow",
            ArrowGallery.Set.Drag => "drag width, bow",
            _ => ""
        };
    }
#endif
}
