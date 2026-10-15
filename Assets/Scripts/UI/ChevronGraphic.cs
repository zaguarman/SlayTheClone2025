using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>
    /// A battlefield mat in the formation's shape (the user, 2026-10-15): a chevron band hugging the five slots, a Λ on the
    /// bottom side and a V on the top one, instead of a rectangle around them. Filled, or only its outline (a stroke this
    /// thick, this far inside the edge). Only the look: drops still land on the mat's rectangle, as before.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ChevronGraphic : MaskableGraphic {
        [SerializeField] private List<Vector2> outline = new List<Vector2>();
        [SerializeField] private float thickness; // 0: filled
        [SerializeField] private float inset;

        /// <summary>The shape's corners, in the mat's local space, in order round the edge.</summary>
        public IReadOnlyList<Vector2> Outline => outline;

        public void SetShape(IEnumerable<Vector2> corners, float strokeThickness = 0f, float strokeInset = 0f) {
            outline = new List<Vector2>(corners);
            thickness = strokeThickness;
            inset = strokeInset;
            raycastTarget = false;
            SetVerticesDirty();
        }

        /// <summary>
        /// The chevron round a formation's slots, shaped like a swept wing (the user: a plane's wing, not a B2's sawtooth):
        /// a blunt nose over the Vanguard, the leading edge sweeping back to the wingtips at Headquarters, square tips, and
        /// a trailing edge that rises from each tip to a flat stretch tucked in just behind the Flanks, parallel to the
        /// Vanguard's back edge. It keeps clear of every card: a little room at the nose, more at the back.
        /// </summary>
        /// <param name="slots">The slots' centres by formation index (0 the Vanguard, 1 and 2 the Flanks, 3 and 4 Headquarters).</param>
        /// <param name="cardHalf">Half a slot's size.</param>
        /// <param name="noseRoom">Room round the Vanguard; the nose is a little wider still, to blunt it.</param>
        /// <param name="tailRoom">Room round the rest, Headquarters most of all.</param>
        /// <param name="towardMiddle">+1 when the middle of the table is up (the bottom side), -1 otherwise.</param>
        public static List<Vector2> Around(IReadOnlyList<Vector2> slots, Vector2 cardHalf, float noseRoom, float tailRoom, float towardMiddle) {
            float s = towardMiddle;
            // Worked out with the middle of the table up (y flipped for the top side), then flipped back
            Vector2 Up(Vector2 p) => new Vector2(p.x, p.y * s);
            Vector2 v = Up(slots[0]), right = Up(slots[4]);
            float cx = v.x; // The formation's middle column
            var a = new Vector2(v.x + cardHalf.x + noseRoom * 1.6f, v.y + cardHalf.y + noseRoom);
            var b = new Vector2(right.x + cardHalf.x + tailRoom, right.y + cardHalf.y + tailRoom);
            var c = new Vector2(right.x + cardHalf.x + tailRoom, right.y - cardHalf.y - tailRoom);
            // The tuck (the user): under each Headquarters card the trailing edge keeps the same room the Flanks get, then
            // past its inner corner it rises to a flat stretch just behind the Flanks, parallel to the Vanguard's back edge
            Vector2 flank = Up(slots[2]);
            float gap = tailRoom * 0.5f;
            float flat = flank.y - cardHalf.y - gap;
            var k = new Vector2(right.x - cardHalf.x - gap, right.y - cardHalf.y - gap); // Clear of the Headquarters card's inner corner
            var d = new Vector2(Mathf.Min(flank.x, k.x - 1f), flat);                      // The flat starts under the Flank's middle
            Vector2 Mirror(Vector2 p) => new Vector2(2f * cx - p.x, p.y);
            Vector2 Back(Vector2 p) => new Vector2(p.x, p.y * s);
            return new List<Vector2> { Back(a), Back(b), Back(c), Back(k), Back(d), Back(Mirror(d)), Back(Mirror(k)), Back(Mirror(c)), Back(Mirror(b)), Back(Mirror(a)) };
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            if (outline == null || outline.Count < 3) return;
            var outer = Offset(outline, inset);
            if (thickness <= 0f) {
                // The chevron is two mirrored fans and a middle triangle: corners 0-4 on one side, 4-8 on the other
                int start = vh.currentVertCount;
                foreach (var point in outer) vh.AddVert(point, color, Vector2.zero);
                int n = outer.Count;
                if (n == 10) { // The wing: nose a-a2, tips b-c, the room under Headquarters to k, the tuck d-d2 behind the Flanks
                    vh.AddTriangle(start, start + 1, start + 2);     // a, b, c
                    vh.AddTriangle(start, start + 2, start + 3);     // a, c, k
                    vh.AddTriangle(start, start + 3, start + 4);     // a, k, d
                    vh.AddTriangle(start, start + 4, start + 5);     // a, d, d2
                    vh.AddTriangle(start, start + 5, start + 9);     // a, d2, a2
                    vh.AddTriangle(start + 9, start + 5, start + 6); // a2, d2, k2
                    vh.AddTriangle(start + 9, start + 6, start + 7); // a2, k2, c2
                    vh.AddTriangle(start + 9, start + 7, start + 8); // a2, c2, b2
                }
                else {
                    for (int i = 1; i < n - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
                }
                return;
            }
            // An outline: a band between the edge (moved in by the inset) and the same edge moved in by the thickness
            var inner = Offset(outline, inset + thickness);
            for (int i = 0; i < outer.Count; i++) {
                int j = (i + 1) % outer.Count;
                int start = vh.currentVertCount;
                vh.AddVert(outer[i], color, Vector2.zero);
                vh.AddVert(outer[j], color, Vector2.zero);
                vh.AddVert(inner[j], color, Vector2.zero);
                vh.AddVert(inner[i], color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }

        // The outline moved inward by a distance, each corner along its mitre
        private static List<Vector2> Offset(List<Vector2> points, float distance) {
            if (Mathf.Approximately(distance, 0f)) return points;
            float area = 0f;
            for (int i = 0; i < points.Count; i++) {
                var p = points[i];
                var q = points[(i + 1) % points.Count];
                area += p.x * q.y - q.x * p.y;
            }
            float inward = area > 0f ? 1f : -1f; // Counter-clockwise: the inside is to the left of each edge
            var moved = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++) {
                var previous = points[(i - 1 + points.Count) % points.Count];
                var point = points[i];
                var next = points[(i + 1) % points.Count];
                Vector2 Normal(Vector2 from, Vector2 to) {
                    var edge = (to - from).normalized;
                    return new Vector2(-edge.y, edge.x) * inward;
                }
                var n1 = Normal(previous, point);
                var n2 = Normal(point, next);
                var mitre = (n1 + n2).normalized;
                float along = Vector2.Dot(mitre, n1);
                moved.Add(point + mitre * (distance / Mathf.Max(0.25f, along)));
            }
            return moved;
        }
    }
}
