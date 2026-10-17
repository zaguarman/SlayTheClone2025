using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Zeitgeist.MenuWidgets;

namespace Zeitgeist {
    /// <summary>
    /// The settings (the dossier, 2026-10-17: "no settings screen"), reached from the title screen and from the pause menu: the
    /// playback speed (1x, 2x, 4x: what F cycles), fullscreen, whether the game records your games (the telemetry), and the
    /// keys, which nothing else on screen listed. Every choice is kept between sessions. Escape or Back closes it.
    /// </summary>
    public class SettingsMenu : MenuScreen {
        public const string SpeedButton = "Settings speed: ";
        public const string FullscreenButtonName = "SettingsFullscreen";
        public const string RecordsButtonName = "SettingsRecords";
        public const string BackButtonName = "SettingsBackButton";

        private readonly List<(Button button, float speed)> speedButtons = new List<(Button, float)>();
        private Button fullscreenButton, recordsButton;

        protected override bool NeedsCards => false;

        protected override GameObject Build() {
            var screen = AddScreen(transform, "SettingsPanel", "Settings");
            var column = AddColumn(screen.columns, null);
            column.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var layout = column.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.spacing = 14;

            Heading(column, "Playback speed", "How fast the Day resolves on screen. F cycles it in a game; Space held skips the beats.");
            var speeds = Row(column);
            foreach (var speed in ResolutionPresenter.Speeds) {
                float chosen = speed;
                var button = AddButton(speeds, SpeedButton + speed, $"{speed:0}x", 160, () => { ResolutionPresenter.SetSpeed(chosen); Refresh(); });
                speedButtons.Add((button, speed));
            }

            Heading(column, "Screen", "Fullscreen or in a window. In the browser the window is the page.");
            fullscreenButton = AddButton(Row(column), FullscreenButtonName, "", 320, () => { Screen.fullScreen = !Screen.fullScreen; Refresh(); });

            Heading(column, "Game records", "The game keeps a record of each game played, for the balance studies: who played what, how it went. Nothing about you but a random id.");
            recordsButton = AddButton(Row(column), RecordsButtonName, "", 320, () => { Telemetry.SetOptedIn(!Telemetry.OptedIn); Refresh(); });

            Heading(column, "Keys", null);
            var keys = AddText(column, string.Join("\n", new[] {
                Shortcut("Left click", "decides: plays, picks, adds, opens"),
                Shortcut("Right click", "takes back: a play, a copy in the deck builder; calls off a drag"),
                Shortcut("Drag", "a card onto a slot or a creature; Shift + drag uses an ability; right-drag attacks your own"),
                Shortcut("Escape", "back, or the pause menu in a game"),
                Shortcut("F", "playback speed"), Shortcut("Space (held)", "skips the beats"),
                Shortcut("D", "the debugging tools on or off: the key list under End Turn, the adventure's autopilot"),
                Shortcut("V", "Fog of War on or off (with D)"), Shortcut("S", "the seat the screen shows (with D)"),
                Shortcut("1, 2", "the AI playing each seat: off, Easy, Medium, Hard, Very Hard (with D)"),
                Shortcut("Up, Down", "walk a list; the wheel moves an item, three on a list's right edge"),
                Shortcut("-, =, P", "the adventure autopilot's speed and pause"),
            }), 20, TextColor, FontStyles.Normal, height: -1);
            keys.textWrappingMode = TextWrappingModes.Normal;
            keys.alignment = TextAlignmentOptions.TopLeft;

            AddButton(AddFooter(screen.panel.transform), BackButtonName, "Back", BackWidth, Hide);
            return screen.panel;
        }

        private static string Shortcut(string key, string what) => $"<color=#B8B0A0>{key}</color>  {what}";

        private static void Heading(Transform column, string title, string note) {
            var heading = AddText(column, title, 24, AccentColor, FontStyles.Bold | FontStyles.SmallCaps, height: 36);
            heading.alignment = TextAlignmentOptions.BottomLeft;
            if (string.IsNullOrEmpty(note)) return;
            var text = AddText(column, note, 19, UITheme.Hex(0xC9C0AE), FontStyles.Normal, height: -1);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
        }

        private static Transform Row(Transform column) {
            var row = NewObject("Row", column).transform;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
            return row;
        }

        /// <summary>What each setting is now, on its buttons.</summary>
        public void Refresh() {
            foreach (var (button, speed) in speedButtons) ShowChosen(button, Mathf.Approximately(ResolutionPresenter.Speed, speed));
            if (fullscreenButton != null) fullscreenButton.GetComponentInChildren<TextMeshProUGUI>().text = Screen.fullScreen ? "Fullscreen: on" : "Fullscreen: off";
            if (recordsButton != null) recordsButton.GetComponentInChildren<TextMeshProUGUI>().text = Telemetry.OptedIn ? "Records: on" : "Records: off";
        }

        protected override void Opened() => Refresh();

        protected override void Forget() {
            speedButtons.Clear();
            fullscreenButton = recordsButton = null;
        }
    }
}
