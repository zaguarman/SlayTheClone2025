using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Screenshots of the battle arrows from every slot to every slot, one side aiming (the other side is the mirror), to
    /// judge the <see cref="ArrowTuning"/> sliders: UI > Arrow Gallery while the game plays. It fills both rows with plain
    /// creatures and plans attacks from the bottom row: each attacker slot at each target slot, several at one target (the
    /// neck where they merge), and Spray and Pray's fork from each slot to each. Every shot is labelled; each set is also
    /// put together into one sheet. Saved in ArrowGallery/ at the project root (not in the project's files).
    /// It changes the game it runs on: use a game you don't mind losing.
    /// </summary>
    public class ArrowGallery : MonoBehaviour {
        private static readonly string[] SlotNames = { "Vanguard", "Left Flank", "Right Flank", "Left HQ", "Right HQ" };
        private const float SettleSeconds = 0.6f; // The old arrows fade out, the new ones draw in
        private const float Scale = 0.5f;          // Of the screen, for each shot: a sheet of 25 stays a reasonable size

        public static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArrowGallery"));
        public static bool Running { get; private set; }

        private GameManager game;
        private TMPro.TextMeshProUGUI label;
        private readonly List<ICreature> attackers = new List<ICreature>();

        public static void Run() {
            if (Running || !Application.isPlaying) return;
            new GameObject("ArrowGallery").AddComponent<ArrowGallery>();
        }

        private IEnumerator Start() {
            Running = true;
            try {
                game = FindFirstObjectByType<GameManager>();
                if (game == null || game.Player1 == null) {
                    Debug.LogWarning("Arrow Gallery: start a game first (Play in the game scene)");
                    yield break;
                }
                MatchSetup.AI = AISeats.None; // Nobody plans anything else meanwhile
                Directory.CreateDirectory(Folder);
                Setup();
                yield return new WaitForSecondsRealtime(1f); // The summons pop in

                var singles = new List<(string, Texture2D)>();
                for (int a = 0; a < 5; a++) {
                    for (int t = 0; t < 5; t++) {
                        yield return Shot(singles, $"{SlotNames[a]} -> {SlotNames[t]}", (a, t));
                    }
                }
                Sheet("1 Singles", singles, 5);

                var merging = new List<(string, Texture2D)>();
                var groups = new (string name, int[] from)[] {
                    ("All five", new[] { 0, 1, 2, 3, 4 }), ("Both flanks", new[] { 1, 2 }), ("Both HQ", new[] { 3, 4 }),
                    ("Left side", new[] { 1, 3 }), ("Vanguard + Right HQ", new[] { 0, 4 })
                };
                foreach (var (name, from) in groups) {
                    for (int t = 0; t < 5; t++) {
                        yield return Shot(merging, $"{name} -> {SlotNames[t]}", from.Select(a => (a, t)).ToArray());
                    }
                }
                Sheet("2 Merging", merging, 5);

                var sprays = new List<(string, Texture2D)>();
                foreach (var attacker in attackers) attacker.Ability = CreatureAbility.SprayAndPray;
                for (int a = 0; a < 5; a++) {
                    for (int t = 0; t < 5; t++) {
                        yield return Shot(sprays, $"Spray: {SlotNames[a]} -> {SlotNames[t]}", (a, t));
                    }
                }
                foreach (var attacker in attackers) attacker.Ability = CreatureAbility.None;
                Sheet("3 Spray and Pray", sprays, 5);
                CancelAll();
                Debug.Log($"Arrow Gallery: saved in {Folder}");
            }
            finally {
                Running = false;
                if (label != null) Destroy(label.gameObject);
                Destroy(gameObject);
            }
        }

        // Plain creatures in every slot, both sides, and nothing else planned
        private void Setup() {
            foreach (var play in game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>().ToList()) game.ActionsQueue.CancelPlay(play.GetCard());
            for (int i = 0; i < 5; i++) {
                attackers.Add(Summon(game.Player1, i, $"Attacker {i}"));
                Summon(game.Player2, i, $"Target {i}");
            }
            CancelAll();

            var board = FindFirstObjectByType<BattlefieldUI>();
            var canvas = board.GetComponentInParent<Canvas>().rootCanvas;
            label = UITheme.NewText("GalleryLabel", canvas.transform, "", 40f, UITheme.Gold, TMPro.TextAlignmentOptions.TopLeft);
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
            rect.position = cam.ViewportToWorldPoint(new Vector3(0.02f, 0.97f, cam.nearClipPlane));
            rect.position = new Vector3(rect.position.x, rect.position.y, canvas.transform.position.z);
        }

        private ICreature Summon(Player side, int slot, string name) {
            var data = ScriptableObject.CreateInstance<CreatureData>();
            data.name = data.cardName = name;
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

        // Plans these attacks (attacker slot, target slot) alone, waits for the arrows, and takes the picture
        private IEnumerator Shot(List<(string, Texture2D)> into, string name, params (int from, int to)[] attacks) {
            CancelAll();
            foreach (var (from, to) in attacks) game.CombatHandler.HandleCreatureCombat(attackers[from], game.Player2.Battlefield[to]);
            label.text = name;
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return new WaitForEndOfFrame();
            var picture = Capture();
            File.WriteAllBytes(Path.Combine(Folder, Safe(name) + ".png"), picture.EncodeToPNG());
            into.Add((name, picture));
        }

        private static Texture2D Capture() {
            var cam = Camera.main;
            int width = Mathf.RoundToInt(Screen.width * Scale), height = Mathf.RoundToInt(Screen.height * Scale);
            var target = RenderTexture.GetTemporary(width, height, 24);
            var previous = cam.targetTexture;
            cam.targetTexture = target;
            cam.Render();
            cam.targetTexture = previous;
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            return picture;
        }

        // The shots of a set side by side, in reading order, a thin dark gap between them
        private static void Sheet(string name, List<(string name, Texture2D picture)> shots, int columns) {
            if (shots.Count == 0) return;
            const int gap = 6;
            int w = shots[0].picture.width, h = shots[0].picture.height;
            int rows = Mathf.CeilToInt(shots.Count / (float)columns);
            var sheet = new Texture2D(columns * (w + gap) + gap, rows * (h + gap) + gap, TextureFormat.RGB24, false);
            var fill = Enumerable.Repeat(new Color32(12, 14, 22, 255), sheet.width * sheet.height).ToArray();
            sheet.SetPixels32(fill);
            for (int i = 0; i < shots.Count; i++) {
                int column = i % columns, row = i / columns;
                int x = gap + column * (w + gap);
                int y = sheet.height - (row + 1) * (h + gap); // Textures count rows from the bottom
                sheet.SetPixels(x, y, w, h, shots[i].picture.GetPixels());
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Folder, $"Sheet - {name}.png"), sheet.EncodeToPNG());
        }

        private static string Safe(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '>' ? '_' : c));

#if UNITY_EDITOR
        [UnityEditor.MenuItem("UI/Arrow Gallery (while playing)")]
        private static void RunFromMenu() {
            if (!Application.isPlaying) {
                Debug.LogWarning("Arrow Gallery: press Play first, in the game scene");
                return;
            }
            Run();
        }
#endif
    }
}
