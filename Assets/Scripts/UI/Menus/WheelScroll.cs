using UnityEngine;
using UnityEngine.UI;

namespace Zeitgeist {
    /// <summary>
    /// Scrolls a ScrollRect with the mouse wheel whenever the pointer is over it, whatever is under the pointer inside it
    /// (buttons, cards). It reads the wheel itself, so it works however the input events are routed; the ScrollRect's own
    /// wheel handling is switched off so a notch never scrolls twice.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class WheelScroll : MonoBehaviour {
        public float PixelsPerNotch = 90f;
        /// <summary>Where over the list the wheel scrolls it (the glossary: its right edge; elsewhere the wheel walks the entries). Null: anywhere.</summary>
        public System.Func<Vector2, bool> ScrollsAt;
        private ScrollRect scroll;

        public static WheelScroll AddTo(ScrollRect scrollRect) {
            scrollRect.scrollSensitivity = 0f;
            return scrollRect.gameObject.AddComponent<WheelScroll>();
        }

        private void Awake() => scroll = GetComponent<ScrollRect>();

        private void Update() {
            float notches = GameInput.WheelNotches;
            if (Mathf.Approximately(notches, 0f) || scroll.content == null) return;
            var area = scroll.viewport != null ? scroll.viewport : (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(area, GameInput.PointerPosition, eventCamera)) return;
            if (ScrollsAt != null && !ScrollsAt(GameInput.PointerPosition)) return;
            ScrollBy(notches);
        }

        /// <summary>Scrolls up for positive notches, down for negative ones, within the content.</summary>
        public void ScrollBy(float notches) {
            float hidden = scroll.content.rect.height - ((RectTransform)(scroll.viewport != null ? scroll.viewport : transform)).rect.height;
            if (hidden <= 0f) return;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + notches * PixelsPerNotch / hidden);
        }
    }
}
