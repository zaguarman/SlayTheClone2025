using UnityEngine;

namespace SlayTheClone {
    public static class CanvasUtility {
        /// <summary>
        /// The camera that renders the root canvas of <paramref name="element"/>, or null for a screen-space overlay canvas
        /// (what RectTransformUtility expects).
        /// </summary>
        public static Camera GetEventCamera(Component element) {
            var canvas = element.GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var rootCanvas = canvas.rootCanvas;
            return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        }
    }
}
