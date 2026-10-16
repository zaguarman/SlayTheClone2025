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

        /// <summary>New corners, keeping the stroke (the sides were switched, F8: the formation faces the other way).</summary>
        public void Reshape(IEnumerable<Vector2> corners) => SetShape(corners, thickness, inset);

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
        /// <param name="noseWidth">The nose's flat over the Vanguard, its whole length (a slider; the same units as <paramref name="tuckWidth"/>).</param>
        /// <param name="tuckGap">How far behind the Flanks' cards the tucked-in flat sits (a slider): only the flat moves.</param>
        /// <param name="tipOut">The wingtips reach this much further out (a slider), <paramref name="tipFront"/> and
        /// <paramref name="tipBack"/> move their corners toward and away from the middle, and <paramref name="wingSize"/> is
        /// the flap's level back edge, from the wingtip in.</param>
        /// <param name="tuckWidth">The tucked-in flat, its whole length (a slider).</param>
        public static List<Vector2> Around(IReadOnlyList<Vector2> slots, Vector2 cardHalf, float noseRoom, float tailRoom, float towardMiddle,
            float noseWidth = 200f, float tuckGap = 15f, float tipOut = 0f, float tipFront = 0f, float tipBack = 0f, float wingSize = 200f,
            float tuckWidth = 400f) {
            float s = towardMiddle;
            // Worked out with the middle of the table up (y flipped for the top side), then flipped back
            Vector2 Up(Vector2 p) => new Vector2(p.x, p.y * s);
            Vector2 v = Up(slots[0]), right = Up(slots[4]);
            float cx = v.x; // The formation's middle column
            // Its flat's whole length (the user: in the same units as the tuck's), as pointy as wanted: down to a point
            var a = new Vector2(cx + Mathf.Max(0.5f, noseWidth / 2f), v.y + cardHalf.y + noseRoom);
            var b = new Vector2(right.x + cardHalf.x + tailRoom + tipOut, right.y + cardHalf.y + tailRoom + tipFront);
            var c = new Vector2(right.x + cardHalf.x + tailRoom + tipOut, right.y - cardHalf.y - tailRoom - tipBack);
            // The flap (the user): from the wingtip the trailing edge runs level, this long, then rises to the tucked-in flat
            // behind the Flanks, parallel to the Vanguard's back edge. The tuck's gap moves only that flat, never the wings
            var k = new Vector2(Mathf.Clamp(c.x - wingSize, cx + 1.5f, c.x - 0.5f), c.y);
            Vector2 flank = Up(slots[2]);
            float flat = flank.y - cardHalf.y - tuckGap;
            var d = new Vector2(cx + Mathf.Clamp(tuckWidth / 2f, 0.5f, k.x - cx - 1f), flat); // Its whole length, down to a point
            Vector2 Mirror(Vector2 p) => new Vector2(2f * cx - p.x, p.y);
            Vector2 Back(Vector2 p) => new Vector2(p.x, p.y * s);
            return new List<Vector2> { Back(a), Back(b), Back(c), Back(k), Back(d), Back(Mirror(d)), Back(Mirror(k)), Back(Mirror(c)), Back(Mirror(b)), Back(Mirror(a)) };
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            if (outline == null || outline.Count < 3) return;
            // The wing's corners rounded, for the look only (the user): the shape the game uses stays its ten corners
            var drawn = outline.Count == 10 ? Rounded(outline, BattlefieldTuning.Current.Roundings) : outline;
            var outer = Offset(drawn, inset);
            if (thickness <= 0f) {
                // Filled: cut into triangles round the edge (ear clipping), whatever its corners
                int start = vh.currentVertCount;
                foreach (var point in outer) vh.AddVert(point, color, Vector2.zero);
                foreach (var (i, j, k) in Triangulate(outer)) vh.AddTriangle(start + i, start + j, start + k);
                return;
            }
            // An outline: a band between the edge (moved in by the inset) and the same edge moved in by the thickness
            var inner = Offset(drawn, inset + thickness);
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

        // Each corner rounded: cut back along both its edges by its radius (never past 45% of an edge), the curve between
        // bulging toward the old corner
        private static List<Vector2> Rounded(List<Vector2> points, float[] radii) {
            const int Steps = 8;
            var result = new List<Vector2>();
            int n = points.Count;
            for (int i = 0; i < n; i++) {
                var point = points[i];
                float radius = i < radii.Length ? radii[i] : 0f;
                if (radius <= 0f) {
                    result.Add(point);
                    continue;
                }
                var previous = points[(i - 1 + n) % n];
                var next = points[(i + 1) % n];
                var from = point + (previous - point).normalized * Mathf.Min(radius, 0.45f * Vector2.Distance(previous, point));
                var to = point + (next - point).normalized * Mathf.Min(radius, 0.45f * Vector2.Distance(next, point));
                for (int s = 0; s <= Steps; s++) {
                    float t = s / (float)Steps;
                    result.Add((1 - t) * (1 - t) * from + 2 * (1 - t) * t * point + t * t * to);
                }
            }
            return result;
        }

        // Triangles filling a simple polygon (its corners in order round the edge), by clipping ears
        private static List<(int, int, int)> Triangulate(List<Vector2> points) {
            var triangles = new List<(int, int, int)>();
            var left = new List<int>();
            for (int i = 0; i < points.Count; i++) left.Add(i);
            float area = 0f;
            for (int i = 0; i < points.Count; i++) {
                var p = points[i];
                var q = points[(i + 1) % points.Count];
                area += p.x * q.y - q.x * p.y;
            }
            float turn = area > 0f ? 1f : -1f; // Counter-clockwise: an ear turns left
            float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
                Cross(a, b, p) * turn >= 0f && Cross(b, c, p) * turn >= 0f && Cross(c, a, p) * turn >= 0f;
            int guard = 0;
            while (left.Count > 3 && guard++ < 10000) {
                bool clipped = false;
                for (int i = 0; i < left.Count; i++) {
                    int ia = left[(i - 1 + left.Count) % left.Count], ib = left[i], ic = left[(i + 1) % left.Count];
                    Vector2 a = points[ia], b = points[ib], c = points[ic];
                    if (Cross(a, b, c) * turn <= 1e-4f) continue; // A reflex (or flat) corner isn't an ear
                    bool empty = true;
                    foreach (int other in left) {
                        if (other == ia || other == ib || other == ic) continue;
                        if (Inside(points[other], a, b, c)) { empty = false; break; }
                    }
                    if (!empty) continue;
                    triangles.Add((ia, ib, ic));
                    left.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // A degenerate shape: what's left is fanned
            }
            for (int i = 1; i < left.Count - 1; i++) triangles.Add((left[0], left[i], left[i + 1]));
            return triangles;
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
