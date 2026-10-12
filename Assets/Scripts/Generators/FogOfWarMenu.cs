#if UNITY_EDITOR
using UnityEditor;

namespace SlayTheClone {
    /// <summary>Debug > Fog of War: switches off what the screen hides (ticked when it's on). F9 does the same in play.</summary>
    public static class FogOfWarMenu {
        private const string Path = "Debug/Fog of War";

        [MenuItem(Path)]
        private static void Toggle() => FogOfWar.Toggle();

        [MenuItem(Path, true)]
        private static bool ShowState() {
            Menu.SetChecked(Path, FogOfWar.Enabled);
            return true;
        }
    }
}
#endif
