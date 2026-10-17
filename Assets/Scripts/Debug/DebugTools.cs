using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Zeitgeist {
    /// <summary>
    /// The debugging tools behind one key (the user, 2026-10-17: "a debugger mode that you can activate or deactivate"): D
    /// shows and hides them anywhere. On, the game shows the key list under End Turn (the Fog of War, the seat shown, the AI
    /// seats), the adventure autopilot's panel and the pause menu's autoplay button; off, none of it, and those keys do
    /// nothing. Off at every start.
    /// </summary>
    public static class DebugTools {
        public const Key ToggleKey = Key.D;
        public static bool On { get; private set; }
        public static event Action Changed;

        public static void Toggle() => Set(!On);

        public static void Set(bool on) {
            if (On == on) return;
            On = on;
            Changed?.Invoke();
        }

        // One watcher for the key, wherever the game is
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Watch() {
            if (UnityEngine.Object.FindFirstObjectByType<DebugToolsKey>() != null) return;
            var go = new GameObject("Debug Tools Key");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DebugToolsKey>();
        }

        private class DebugToolsKey : MonoBehaviour {
            private void Update() {
                if (GameInput.KeyPressed(ToggleKey) && !Typing) Toggle();
            }

            private static bool Typing => UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null
                && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null;
        }
    }
}
