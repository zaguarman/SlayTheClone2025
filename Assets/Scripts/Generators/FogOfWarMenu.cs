#if UNITY_EDITOR
using UnityEditor;

namespace Zeitgeist {
    /// <summary>
    /// Debug > Fog of War: switches off what the screen hides (ticked when it's on), V in play. Debug > Play As Player 2:
    /// the screen shows player 2's view and uses their cards, S in play.
    /// </summary>
    public static class FogOfWarMenu {
        private const string Path = "Debug/Fog of War";
        private const string SidesPath = "Debug/Play As Player 2";

        [MenuItem(Path)]
        private static void Toggle() => FogOfWar.Toggle();

        [MenuItem(Path, true)]
        private static bool ShowState() {
            Menu.SetChecked(Path, FogOfWar.Enabled);
            return true;
        }

        [MenuItem(SidesPath)]
        private static void SwitchSides() => FogOfWar.SwitchSides();

        [MenuItem(SidesPath, true)]
        private static bool ShowSides() {
            Menu.SetChecked(SidesPath, !FogOfWar.PlaysAsPlayer1);
            return true;
        }
    }
}
#endif
