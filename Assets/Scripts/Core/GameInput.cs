using UnityEngine;
using UnityEngine.InputSystem;

namespace Zeitgeist {
    /// <summary>
    /// What the player does with the mouse and keyboard, read from the Input System package. Scripts ask here rather than
    /// the input API, so the API lives in one place. With no mouse or keyboard (a test runner) nothing reads as pressed.
    /// </summary>
    public static class GameInput {
        /// <summary>The pointer in screen pixels, from the bottom-left corner.</summary>
        public static Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        /// <summary>The wheel this frame, in notches: positive up, negative down, 0 when it's still.</summary>
        public static float WheelNotches {
            get {
                if (Mouse.current == null) return 0f;
                float y = Mouse.current.scroll.ReadValue().y;
                return Mathf.Abs(y) >= 20f ? y / 120f : y; // Some platforms report 120 per notch
            }
        }

        /// <summary>A mouse button, left or right, went down this frame.</summary>
        public static bool ClickedThisFrame => Mouse.current != null
            && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);

        /// <summary>The right mouse button went down this frame.</summary>
        public static bool RightClickedThisFrame => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        /// <summary>
        /// The right click under way was used up (it cancelled a drag, or took a card back from the roulette): until the
        /// button is let go, nothing else takes it, its release included (the user: that right click cancels nothing else).
        /// </summary>
        public static bool RightClickSwallowed { get; private set; }

        public static void SwallowRightClick() {
            if (RightClickSwallowed) return;
            RightClickSwallowed = true;
            InputSystem.onAfterUpdate += WatchSwallowed;
        }

        private static void WatchSwallowed() {
            var mouse = Mouse.current;
            if (mouse != null && (mouse.rightButton.isPressed || mouse.rightButton.wasReleasedThisFrame)) return;
            RightClickSwallowed = false;
            InputSystem.onAfterUpdate -= WatchSwallowed;
        }

        public static bool KeyPressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        public static bool KeyHeld(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;

        /// <summary>A mouse button held down (a drag going on).</summary>
        public static bool ButtonHeld => Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed);

        public static bool ShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
    }
}
