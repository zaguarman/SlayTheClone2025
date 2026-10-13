using System;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Mouse wheel navigation for a list of choices: while the pointer is over the list, each tick picks the next item
    /// (wheel down) or the previous one (wheel up). The list says what "next" means through <see cref="OnStep"/>.
    /// </summary>
    public class WheelStepper : MonoBehaviour {
        public Action<int> OnStep; // +1: next (wheel down), -1: previous (wheel up)

        public static WheelStepper AddTo(GameObject list, Action<int> onStep) {
            var stepper = list.AddComponent<WheelStepper>();
            stepper.OnStep = onStep;
            return stepper;
        }

        private void Update() {
            float ticks = GameInput.WheelNotches;
            if (Mathf.Approximately(ticks, 0f) || OnStep == null) return;
            var canvas = GetComponentInParent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, GameInput.PointerPosition, eventCamera)) return;
            OnStep(ticks < 0f ? 1 : -1);
        }

        /// <summary>The item this many steps from the current one, stopping at the ends of the list.</summary>
        public static T Next<T>(System.Collections.Generic.IReadOnlyList<T> items, T current, int steps) {
            if (items == null || items.Count == 0) return current;
            int index = 0;
            for (int i = 0; i < items.Count; i++) {
                if (Equals(items[i], current)) { index = i; break; }
            }
            return items[Mathf.Clamp(index + steps, 0, items.Count - 1)];
        }
    }
}
