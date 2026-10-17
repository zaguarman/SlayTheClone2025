using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Zeitgeist {
    /// <summary>
    /// How every list scrolls (the user, 2026-10-17, the glossary's way for all the menus): over the items, the wheel moves one
    /// item a notch (a row of a grid), so the eye keeps its place; on the list's right edge (its last tenth) a notch moves
    /// three, to get somewhere fast; Up and Down do an item each while the pointer is over the list and nothing is being
    /// typed. It reads the wheel itself, so it works however the input events are routed; the ScrollRect's own wheel
    /// handling is switched off so a notch never scrolls twice. A menu that walks a selection with the wheel (the glossary)
    /// keeps the wheel for itself off the edge: <see cref="ScrollsAt"/>.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class WheelScroll : MonoBehaviour {
        public const float EdgeShare = 0.1f; // The right tenth of the list: the fast lane
        public const int EdgeItems = 3;
        public float PixelsPerNotch = 90f; // When the content has no items to measure
        /// <summary>Where over the list the wheel scrolls it (the glossary: its right edge; elsewhere the wheel walks the entries). Null: anywhere.</summary>
        public System.Func<Vector2, bool> ScrollsAt;
        private ScrollRect scroll;

        public static WheelScroll AddTo(ScrollRect scrollRect) {
            scrollRect.scrollSensitivity = 0f;
            return scrollRect.gameObject.AddComponent<WheelScroll>();
        }

        private void Awake() => scroll = GetComponent<ScrollRect>();

        private RectTransform Area => scroll.viewport != null ? scroll.viewport : (RectTransform)transform;

        private Camera EventCamera {
            get {
                var canvas = GetComponentInParent<Canvas>();
                return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            }
        }

        private bool PointerOver(Vector2 pointer) => RectTransformUtility.RectangleContainsScreenPoint(Area, pointer, EventCamera);

        /// <summary>The pointer is on the list's right edge, its last tenth: the fast lane.</summary>
        public bool OnEdge(Vector2 pointer) {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, pointer, EventCamera, out var local)) return false;
            return (local.x - Area.rect.xMin) / Area.rect.width >= 1f - EdgeShare;
        }

        private static bool Typing => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null;

        private void Update() {
            if (scroll.content == null) return;
            var pointer = GameInput.PointerPosition;
            if (!PointerOver(pointer)) return;
            float notches = GameInput.WheelNotches;
            if (!Mathf.Approximately(notches, 0f) && (ScrollsAt == null || ScrollsAt(pointer))) {
                ScrollPixels(notches * ItemHeight() * (OnEdge(pointer) ? EdgeItems : 1));
            }
            if (Typing) return;
            if (GameInput.KeyPressed(Key.DownArrow)) ScrollPixels(-ItemHeight());
            else if (GameInput.KeyPressed(Key.UpArrow)) ScrollPixels(ItemHeight());
        }

        /// <summary>One item's height with its spacing: a grid's cell, else the first item shown, else <see cref="PixelsPerNotch"/>.</summary>
        public float ItemHeight() {
            var content = scroll.content;
            if (content.GetComponent<GridLayoutGroup>() is GridLayoutGroup grid) return grid.cellSize.y + grid.spacing.y;
            float spacing = content.GetComponent<VerticalLayoutGroup>()?.spacing ?? 0f;
            foreach (RectTransform child in content) {
                if (!child.gameObject.activeSelf) continue;
                float height = LayoutUtility.GetPreferredHeight(child);
                if (height <= 0f) height = child.rect.height;
                if (height > 0f) return height + spacing;
            }
            return PixelsPerNotch;
        }

        /// <summary>Scrolls up for positive notches, down for negative ones, within the content.</summary>
        public void ScrollBy(float notches) => ScrollPixels(notches * PixelsPerNotch);

        /// <summary>Scrolls by this many pixels of content, up for positive, within the content.</summary>
        public void ScrollPixels(float pixels) {
            float hidden = scroll.content.rect.height - Area.rect.height;
            if (hidden <= 0f) return;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + pixels / hidden);
        }
    }
}
