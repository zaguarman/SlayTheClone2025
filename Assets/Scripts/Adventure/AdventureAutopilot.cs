using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The adventure on autopilot (the user, 2026-10-16: "an automated run tool with a speed changer to analyze the runs
    /// personally"). It plays a whole run on screen, as you would: the map's rooms, the rewards, the shops, the events and
    /// the rest sites are chosen as the AI adventure simulation chooses them, and the fights are played by the AI on both
    /// seats. A panel in the top-left corner shows where it is and controls it, in the map and in the fights:
    /// • Speed (- and =, or the buttons): 0.25x to 16x. It sets the game's time scale, so the fights' playback follows it,
    ///   and the pauses between its choices and the AI's turns shrink with it.
    /// • Pause (P): everything stops, to read the board or the queue; Step makes one choice while paused.
    /// • Repeat: when the run ends, a new one starts with the same faction.
    /// • Stop: you take over the run where it is.
    /// Each run is logged, one line a choice and one a fight, with a summary at the end (its deck, relics, potions), to
    /// Logs/Autopilot in the project folder (the log button opens it).
    /// </summary>
    public class AdventureAutopilot : MonoBehaviour {
        public static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f };
        public const Key SlowerKey = Key.Minus, FasterKey = Key.Equals, PauseKey = Key.P;
        public const float StepSeconds = 1.5f; // Real seconds between two choices, at 1x
        public const int HurtBelow = 40;       // A rest site: below this it sleeps, above it trains
        private const int OverlayOrder = UITheme.WindowsLayer + 500;

        public static AdventureAutopilot Instance { get; private set; }
        public static bool On => Instance != null;
        private static int speedIndex = 3; // 2x
        public static float Speed => Speeds[speedIndex];
        public static bool Paused { get; private set; }
        public static bool Repeat { get; set; }
        /// <summary>How well the AI plays your seat in the fights.</summary>
        public static AILevel Level { get; private set; } = AILevel.Hard;
        public static string LogFolder => Path.Combine(Application.isEditor ? Path.GetDirectoryName(Application.dataPath) : Application.persistentDataPath, "Logs", "Autopilot");

        /// <summary>The runs played since it was switched on, and how many beat the boss.</summary>
        public int Runs { get; private set; }
        public int Wins { get; private set; }
        /// <summary>The run's log so far, a line a choice or a fight.</summary>
        public IReadOnlyList<string> RunLog => runLog;

        private readonly List<string> runLog = new List<string>();
        private string logPath;
        private Faction faction;
        private System.Random random;
        private float wait;
        private bool stepOnce;
        private bool runEnded, fightLogged;
        private bool boughtCard, boughtPotion; // At the shop it's in
        private string lostIn = "a fight"; // The room of the last fight, for the defeat's line
        private TextMeshProUGUI statusText, speedText, pauseText, repeatText, levelText, lastLine;

        #region Switching on and off
        /// <summary>Takes over the run under way (start one first: Embark), from the adventure screen.</summary>
        public static void Begin() {
            if (!AdventureRun.Active) return;
            if (Instance == null) {
                var go = new GameObject("Adventure Autopilot");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<AdventureAutopilot>();
                Instance.BuildOverlay();
            }
            Instance.StartRun(fresh: AdventureRun.Path.Count == 0);
        }

        /// <summary>You take over: the autopilot stops where it is, the speed back to normal.</summary>
        public static void End() {
            if (Instance == null) return;
            Instance.Write("Autopilot stopped: you take over.");
            Destroy(Instance.gameObject);
        }

        private void OnDestroy() {
            if (Instance != this) return;
            Instance = null;
            Paused = false;
            Time.timeScale = 1f;
            AIController.TurnPause = 1f;
            if (MatchSetup.AI == AISeats.Both && AdventureRun.Fighting != null) MatchSetup.AI = AISeats.Player2; // Your seat is yours again
        }

        private void StartRun(bool fresh) {
            faction = AdventureRun.Faction;
            random = new System.Random(Environment.TickCount);
            runLog.Clear();
            runEnded = false;
            Directory.CreateDirectory(LogFolder);
            logPath = Path.Combine(LogFolder, $"{DateTime.Now:yyyy-MM-dd HH-mm-ss} {CardTextFormatter.FactionName(faction).TrimEnd('.')}.txt");
            Write($"Adventure on autopilot: {CardTextFormatter.FactionName(faction)}, map seed {AdventureRun.Map.Seed}, your seat played by the {new AIEntrant(Level).Name} AI"
                  + (fresh ? "" : $" (taken over in room {AdventureRun.Path.Count}, Health {AdventureRun.Health})"));
            Write($"Starter deck: {DeckList(AdventureRun.Deck.Cards)}");
            wait = StepSeconds;
        }
        #endregion

        #region Each frame
        private void Update() {
            if (!MenuWidgets.IsTyping) {
                if (GameInput.KeyPressed(SlowerKey) || GameInput.KeyPressed(Key.NumpadMinus)) ChangeSpeed(-1);
                if (GameInput.KeyPressed(FasterKey) || GameInput.KeyPressed(Key.NumpadPlus)) ChangeSpeed(+1);
                if (GameInput.KeyPressed(PauseKey)) TogglePause();
            }
            bool menuPaused = FindFirstObjectByType<PauseMenu>() is PauseMenu menu && menu.IsPaused;
            if (!menuPaused) Time.timeScale = Paused ? 0f : Speed;
            AIController.TurnPause = 1f / Speed; // The AI's look at its plans, as fast as the rest
            RefreshOverlay();

            if (menuPaused || (Paused && !stepOnce)) return;
            wait -= Time.unscaledDeltaTime * Speed;
            if (wait > 0f && !stepOnce) return;
            stepOnce = false;
            wait = StepSeconds;
            try {
                Step();
            }
            catch (Exception exception) {
                Write($"The autopilot ran into an error and stopped: {exception.Message}");
                Debug.LogException(exception);
                End();
            }
        }

        // One choice, wherever the run is
        private void Step() {
            if (FindFirstObjectByType<GameManager>() is GameManager game) {
                StepFight(game);
                return;
            }
            var menu = FindFirstObjectByType<AdventureMenu>();
            if (menu == null) return; // A scene loading
            if (!menu.IsOpen) {
                menu.Show();
                return;
            }
            if (AdventureRun.Won || AdventureRun.Lost) {
                EndRun(menu);
                return;
            }
            if (!AdventureRun.Active) {
                End();
                return;
            }
            if (menu.ViewingDeck) menu.ShowDeck(false);
            else if (AdventureRun.InRewards) StepRewards(menu);
            else if (AdventureRun.InShop) StepShop(menu);
            else if (AdventureRun.InEvent) StepEvent(menu);
            else StepMap(menu);
        }
        #endregion

        #region The choices
        // The next room: at random along the paths, leaning toward rest when hurt, toward shops with money, away from elites when weak
        private void StepMap(AdventureMenu menu) {
            var rooms = AdventureRun.Reachable();
            if (rooms.Count == 0) return;
            bool hurt = AdventureRun.Health < HurtBelow;
            float Weight(MapNode room) => room.Type switch {
                RoomType.Rest => hurt ? 4f : 1f,
                RoomType.Shop => AdventureRun.EuroYuans >= 20 ? 2f : 0.5f,
                RoomType.Treasure => 3f,
                RoomType.Elite => AdventureRun.Health >= 45 ? 1f : 0.2f,
                _ => 1f
            };
            double roll = random.NextDouble() * rooms.Sum(Weight);
            var room = rooms.Last();
            foreach (var option in rooms) {
                roll -= Weight(option);
                if (roll < 0) {
                    room = option;
                    break;
                }
            }
            Write($"Row {room.Row + 1}: {AdventureMenu.RoomName(room.Type)} (of {string.Join(", ", rooms.Select(r => AdventureMenu.RoomName(r.Type)))})");
            boughtCard = boughtPotion = false;
            fightLogged = false;
            if (room.IsCombat) {
                menu.Enter(room); // Sets the fight up, then the game scene loads
                var enemy = AdventureRun.EnemyDeck;
                Write($"    Fight: {enemy?.GetDeckName()} ({MatchSetup.OpponentHealth} Health, {new AIEntrant(MatchSetup.LevelOf(AISeats.Player2)).Name} AI); you: {AdventureRun.Health} Health");
                lostIn = room.Type == RoomType.Boss ? "the boss" : $"{(room.Type == RoomType.Elite ? "an elite" : "a fight")} in row {room.Row + 1}";
            }
            else menu.Enter(room);
        }

        // A fight: the AI plays both seats; once it's over, its result, then on
        private void StepFight(GameManager game) {
            if (MatchSetup.AI != AISeats.Both) {
                MatchSetup.AI = AISeats.Both; // Taken over in the middle of a fight
                MatchSetup.SetLevel(AISeats.Player1, Level);
            }
            var over = FindFirstObjectByType<GameOverScreen>();
            if (over == null || !over.IsShown) return;
            if (!fightLogged) {
                fightLogged = true;
                int day = game.TurnManager?.TurnNumber ?? 0;
                bool won = !AdventureRun.Lost;
                Write(won
                    ? $"    Won on Day {day}, {AdventureRun.Health} Health left" + (AdventureRun.Earned > 0 ? $", +{AdventureRun.Earned} EuroYuans" : "")
                      + (AdventureRun.Found.Count > 0 ? $"; {string.Join("; ", AdventureRun.Found.Select(Plain))}" : "")
                    : $"    Lost on Day {day}: {game.Player2.Health} Health left on their side");
                return; // A moment to see the result
            }
            over.MainMenuButton.onClick.Invoke(); // Continue: back to the map
        }

        // Each pack: open it, take its strongest card; then on
        private void StepRewards(AdventureMenu menu) {
            if (menu.OpenPack != null) {
                var best = menu.OpenPack.Cards.OrderByDescending(AdventureDecks.Power).First();
                Write($"    Pack: {string.Join(", ", menu.OpenPack.Cards.Select(c => c.cardName))}: takes {best.cardName}");
                menu.TakeReward(best);
                return;
            }
            var pack = AdventureRun.Rewards.FirstOrDefault(p => !p.Done && p.Cards.Count > 0);
            if (pack != null) menu.OpenRewardPack(pack);
            else menu.ProceedFromRewards();
        }

        // The strongest card it can pay, a potion with room for it, then its weakest card out; then on
        private void StepShop(AdventureMenu menu) {
            if (menu.Removing) {
                var weakest = AdventureRun.Deck.Cards.OrderBy(AdventureDecks.Power).First();
                Write($"    Shop: takes {weakest.cardName} out for {AdventureRun.RemovalCost}");
                menu.RemoveFromDeck(weakest);
                return;
            }
            if (!boughtCard) {
                boughtCard = true;
                var card = AdventureRun.Shop.Where(o => !o.Sold && !o.IsPotion && o.Price <= AdventureRun.EuroYuans)
                    .OrderByDescending(o => AdventureDecks.Power(o.Card)).FirstOrDefault();
                if (card != null) {
                    Write($"    Shop: buys {card.Card.cardName} for {card.Price}");
                    menu.Buy(card);
                    return;
                }
            }
            if (!boughtPotion) {
                boughtPotion = true;
                var potion = AdventureRun.Shop.FirstOrDefault(o => !o.Sold && o.IsPotion && o.Price <= AdventureRun.EuroYuans);
                if (potion != null && AdventureRun.Potions.Count < AdventureRun.MaxPotions) {
                    Write($"    Shop: buys the potion {potion.Card.cardName} for {potion.Price}");
                    menu.Buy(potion);
                    return;
                }
            }
            if (AdventureRun.CanRemove) {
                menu.OpenRemoval(true);
                return;
            }
            Write($"    Shop: leaves with {AdventureRun.EuroYuans} EuroYuans");
            menu.LeaveShop();
        }

        // A rest site: sleep when hurt, otherwise train the strongest card; a chest: open it; an event: a choice at random
        private void StepEvent(AdventureMenu menu) {
            var happening = AdventureRun.Event;
            if (AdventureRun.Upgrading) {
                var best = CardUpgrades.Upgradable(AdventureRun.Deck.Cards).OrderByDescending(AdventureDecks.Power).FirstOrDefault();
                if (best == null) return;
                Write($"    Trains: {best.cardName} becomes {best.cardName}{CardUpgrades.Mark}");
                menu.UpgradeCard(best);
                return;
            }
            if (AdventureRun.EventOutcome != null) {
                Write($"    {Plain(AdventureRun.EventOutcome)}");
                menu.LeaveEvent();
                return;
            }
            var open = happening.Choices.Where(c => c.Available()).ToList();
            if (open.Count == 0) {
                menu.LeaveEvent();
                return;
            }
            AdventureEvents.Choice choice;
            if (happening.Id == "Rest") choice = AdventureRun.Health < HurtBelow || !open.Any(c => c.Trains) ? open.First(c => !c.Trains) : open.First(c => c.Trains);
            else if (happening.Id == "Treasure") choice = open.First();
            else choice = open[random.Next(open.Count)];
            Write($"    {happening.Title}: {Plain(choice.Label)}");
            menu.ChooseAtEvent(choice);
        }

        // The run is over: its summary; then a new one (Repeat) or it stops, the map showing how far it got
        private void EndRun(AdventureMenu menu) {
            if (!runEnded) {
                runEnded = true;
                Runs++;
                if (AdventureRun.Won) Wins++;
                Write(AdventureRun.Won ? "THE BOSS IS BEATEN: the run is won." : $"DEFEATED: {lostIn}.");
                Write($"Rooms: {AdventureRun.Path.Count}, fights won: {AdventureRun.FightsWon}, Health {AdventureRun.Health}, EuroYuans {AdventureRun.EuroYuans}");
                Write($"Relics: {(AdventureRun.Relics.Count == 0 ? "none" : string.Join(", ", AdventureRun.Relics.Select(Relics.NameOf)))}; potions: {(AdventureRun.Potions.Count == 0 ? "none" : string.Join(", ", AdventureRun.Potions.Select(p => p.cardName)))}");
                Write($"Deck ({AdventureRun.Deck.Cards.Count}): {DeckList(AdventureRun.Deck.Cards)}");
                Write($"Runs so far: {Runs}, bosses beaten: {Wins}");
                return; // A moment to see the map
            }
            if (!Repeat) {
                End();
                return;
            }
            AdventureRun.Clear();
            menu.Pick(faction);
            menu.Embark();
            StartRun(fresh: true);
        }
        #endregion

        #region The log
        private void Write(string line) {
            runLog.Add(line);
            if (lastLine != null) lastLine.text = line.Trim();
            try {
                if (logPath != null) File.AppendAllText(logPath, line + Environment.NewLine);
            }
            catch (IOException) { } // The log is a help, not the run
        }

        private static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich ?? "", "<[^>]+>", "").Replace("\n", " ");

        private static string DeckList(IEnumerable<CardDataScriptableObject> cards) =>
            string.Join(", ", cards.GroupBy(c => c.cardName).OrderBy(g => g.Key).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));
        #endregion

        #region Controls
        public static void ChangeSpeed(int by) => speedIndex = Mathf.Clamp(speedIndex + by, 0, Speeds.Length - 1);

        public static void TogglePause() => Paused = !Paused;

        /// <summary>One choice now, paused or not.</summary>
        public void StepNow() => stepOnce = true;

        public static void CycleLevel() => Level = Level >= AILevel.VeryHard ? AILevel.Easy : Level + 1;

        private static void OpenLogs() {
            Directory.CreateDirectory(LogFolder);
            Application.OpenURL("file:///" + LogFolder.Replace('\\', '/'));
        }
        #endregion

        #region The panel
        private const float PanelWidth = 280f, RowHeight = 34f;

        // A small panel in the top-left corner, over everything, in every scene (it stays clear of the board and the plaques)
        private void BuildOverlay() {
            var canvas = new GameObject("Autopilot Panel", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlayOrder;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            var panel = UITheme.NewImage("Panel", canvas.transform, UITheme.Panel, raycastTarget: true);
            UITheme.StylePanel(panel, trim: UITheme.Gold);
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -10f);
            rect.sizeDelta = new Vector2(PanelWidth, 0f);
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(10, 10, 8, 8);
            column.spacing = 4f;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            statusText = Label(panel.transform, "Status", 18f, 2);
            var speed = Row(panel.transform, "Speed");
            Small(speed, "Slower", "-", 40f, () => ChangeSpeed(-1));
            speedText = UITheme.NewText("Speed", speed, "", 20f, UITheme.Gold);
            speedText.gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;
            Small(speed, "Faster", "+", 40f, () => ChangeSpeed(+1));
            pauseText = Small(speed, "Pause", "Pause", 90f, TogglePause);
            var play = Row(panel.transform, "Play");
            Small(play, "Step", "Step", 70f, StepNow);
            levelText = Small(play, "Level", "AI", 170f, CycleLevel);
            var run = Row(panel.transform, "Run");
            repeatText = Small(run, "Repeat", "Repeat", 100f, () => Repeat = !Repeat);
            Small(run, "Logs", "Logs", 70f, OpenLogs);
            Small(run, "Stop", "Stop", 70f, End);
            lastLine = Label(panel.transform, "Last", 15f, 3);
            lastLine.color = UITheme.TextMuted;
        }

        private static Transform Row(Transform parent, string name) {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            return row.transform;
        }

        // A text a few lines tall, wrapping
        private static TextMeshProUGUI Label(Transform parent, string name, float size, int lines) {
            var text = UITheme.NewText(name, parent, "", size, UITheme.Parchment, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = size * 1.25f * lines;
            return text;
        }

        private static TextMeshProUGUI Small(Transform parent, string name, string label, float width, Action onClick) {
            var button = MenuWidgets.AddButton(parent, "Autopilot " + name, label, width, onClick);
            var element = button.GetComponent<LayoutElement>();
            element.preferredHeight = RowHeight;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        private void RefreshOverlay() {
            if (statusText == null) return;
            string where = AdventureRun.Won ? "won" : AdventureRun.Lost ? "lost"
                : AdventureRun.Fighting != null ? $"fight, row {AdventureRun.Fighting.Row + 1}"
                : AdventureRun.Current == null ? "at the start" : $"row {AdventureRun.Current.Row + 1}";
            statusText.text = $"<b>Autopilot</b>: {CardTextFormatter.FactionName(faction)}, {where}\n{AdventureRun.Health} Health"
                              + (Runs > 0 ? $", run {Runs + (runEnded ? 0 : 1)} ({Wins} won)" : "");
            speedText.text = Paused ? "<color=#E04B3A>paused</color>" : $"{Speed:0.##}x";
            pauseText.text = Paused ? "Resume" : "Pause";
            levelText.text = $"Your AI: {new AIEntrant(Level).Name}";
            repeatText.text = Repeat ? "<b>Repeat</b>" : "<s>Repeat</s>";
        }
        #endregion
    }
}
