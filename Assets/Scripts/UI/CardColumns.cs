using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>
    /// Fills the width a grid of cards has with a set number of columns, each card as big as that allows, at its own
    /// proportions, so the cards are legible at a glance instead of small ones lost in empty space. Each cell holds one card
    /// face, centered; it fits again whenever the width changes (the window resized, a scrollbar showing up).
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class CardColumns : UIBehaviour {
        public const float CardWidth = 160f, CardHeight = 200f; // A card face at scale 1
        public int Columns = 4;
        /// <summary>The scale the cards are shown at.</summary>
        public float Scale { get; private set; } = 1f;

        private GridLayoutGroup grid;
        private float fittedWidth = -1f;

        public static CardColumns AddTo(GridLayoutGroup grid, int columns) {
            var fit = grid.gameObject.GetComponent<CardColumns>() ?? grid.gameObject.AddComponent<CardColumns>();
            fit.Columns = columns;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            fit.Fit(force: true);
            return fit;
        }

        /// <summary>Sizes the cells to the width and scales every card face in them to fill its cell.</summary>
        public void Fit(bool force = false) {
            grid ??= GetComponent<GridLayoutGroup>();
            var area = transform.parent as RectTransform ?? (RectTransform)transform; // The viewport: the width there is
            float width = area.rect.width - grid.padding.horizontal;
            if (width <= 0f || (!force && Mathf.Approximately(width, fittedWidth))) {
                ScaleFaces();
                return;
            }
            fittedWidth = width;
            float cell = Mathf.Floor((width - grid.spacing.x * (Columns - 1)) / Columns);
            Scale = cell / CardWidth;
            grid.cellSize = new Vector2(cell, Mathf.Floor(CardHeight * Scale));
            ScaleFaces();
        }

        private void ScaleFaces() {
            foreach (Transform cell in transform) {
                var face = cell.GetComponentInChildren<CardController>(true);
                if (face != null && face.transform != cell) face.transform.localScale = Vector3.one * Scale;
            }
        }

        protected override void OnRectTransformDimensionsChange() {
            if (isActiveAndEnabled) Fit();
        }

        protected override void OnTransformParentChanged() => Fit(force: true);
    }
}
