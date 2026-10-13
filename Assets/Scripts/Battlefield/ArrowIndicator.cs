using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// A battle arrow in the style of an old campaign map: a broad translucent band with a crisp darker edge, a swallowtail
    /// at its tail and a barbed head, fading in from where it starts so its direction reads at a glance.
    /// One arrow can start from several places: when creatures attack the same target, their bands flow together into
    /// one trunk, wider for each of them, that ends in a single head. The parts of an arrow merge into one shape (see the
    /// TacticalArrow shader), so overlaps never show as darker seams.
    /// Arrows keep to the right of where they're going: two going opposite ways pass side by side instead of on top of
    /// each other, and longer ones bow gently to that side. Queued arrows draw themselves in and fade out when removed.
    /// </summary>
    public class ArrowIndicator : MonoBehaviour {
        /// <summary>How an arrow sits between its ends, in world units.</summary>
        public struct Style {
            public float Width;      // The band's width where it meets the head (a trunk merging several is wider)
            public float Lane;       // Shift to the right of travel, so arrows going opposite ways pass side by side
            public float Bow;        // Bulge to the right of travel, as a fraction of its length...
            public float MaxBow;     // ...and at most this much
            public float StartInset; // From each start toward the target: the tail starts near the card's edge, not its middle
            public float EndInset;   // Back from the end: the head stops at the target's near edge, clear of its stats

            // Cards are 1.6 x 1.96 units, and the rows 1.26 apart
            public static Style Attack => new Style { Width = 0.2f, Lane = 0.22f, Bow = 0.08f, MaxBow = 0.35f, StartInset = 0.8f, EndInset = 0.99f };
            public static Style Damage => new Style { Width = 0.16f, Lane = 0f, Bow = 0.08f, MaxBow = 0.35f, StartInset = 0.8f, EndInset = 0.99f };
            public static Style Spell => new Style { Width = 0.17f, Lane = 0.5f, Bow = 0.1f, MaxBow = 0.5f, StartInset = 0.45f, EndInset = 0.99f };
            public static Style Move => new Style { Width = 0.14f, Lane = 0f, Bow = 0.2f, MaxBow = 0.42f, StartInset = 0.3f, EndInset = 0.42f };
            public static Style Drag => new Style { Width = 0.18f, Lane = 0f, Bow = 0.1f, MaxBow = 0.4f, StartInset = 0.3f, EndInset = 0f };

            /// <summary>To the edge of a whole battlefield: the tip lands exactly there (between two slots), no lane.</summary>
            public Style ToEdge() {
                var style = this;
                style.EndInset = 0f;
                style.Lane = 0f;
                return style;
            }
        }

        // Shape
        private const int Samples = 28;             // Points along each curve
        private const float TailWidth = 0.7f;       // Of the band's width, at its tail
        private const float NotchDepth = 0.9f;      // The swallowtail's depth, in half-widths
        private const float HeadLength = 1.9f;      // In band widths
        private const float HeadHalfWidth = 1.35f;  // In band widths
        private const float MaxHeadLength = 0.62f, MaxHeadHalfWidth = 0.46f;
        private const float BarbSweep = 0.28f;      // How far back the barbs reach, in head lengths
        private const float RimWidth = 0.022f;     // A bright line round the band: it reads on the dark table...
        private const float OutlineWidth = 0.045f; // ...and a thin dark one outside it, on the light cards
        private const float WidthPerExtraBranch = 0.45f, MaxTrunkWidth = 2.2f; // In band widths
        private const float BranchShare = 0.55f;    // How much of the draw-in the branches take before the trunk
        private const float MinLength = 0.1f;

        // Look
        private const float TailAlpha = 0.12f, BodyAlpha = 0.42f, NearHeadAlpha = 0.52f, HeadAlpha = 0.6f;
        private const float EdgeTailAlpha = 0.18f, EdgeAlpha = 0.95f, OutlineAlpha = 0.55f;
        private const float DrawInDuration = 0.3f;
        private const float FadeOutDuration = 0.2f;
        private const int SortingOrder = 5000;

        private static Material sharedMaterial;
        private static int nextStencil;   // Each arrow merges only with itself
        private static int nextSortSlot;  // Later arrows draw over earlier ones

        private MeshFilter fillFilter, edgeFilter, outlineFilter;
        private MeshRenderer fillRenderer, edgeRenderer, outlineRenderer;
        private Mesh fillMesh, edgeMesh, outlineMesh;
        private Material material;

        private readonly List<Vector3> starts = new List<Vector3>();
        private Vector3 endPosition;
        private Color color = Color.red;
        private Style style = Style.Attack;
        private float drawnFraction = 1f; // Draw-in progress
        private float opacity = 1f;
        private bool isVisible;
        private bool isFadingOut;

        public static ArrowIndicator Create(Transform parent = null) {
            var arrowObject = new GameObject("ArrowIndicator");
            if (parent != null) {
                arrowObject.transform.SetParent(parent, false);
                arrowObject.transform.localPosition = Vector3.zero;
            }
            return arrowObject.AddComponent<ArrowIndicator>();
        }

        private void Awake() {
            if (sharedMaterial == null) {
                var shader = Resources.Load<Shader>("Shaders/TacticalArrow") ?? Shader.Find("Sprites/Default");
                sharedMaterial = new Material(shader) { name = "TacticalArrow" };
            }
            material = new Material(sharedMaterial);
            nextStencil = nextStencil % 254 + 1;
            material.SetFloat("_StencilRef", nextStencil);

            // Drawn in this order, each part only where the ones before it aren't: the fill, a bright rim round it, a dark line round that
            int order = SortingOrder + 3 * (nextSortSlot++ % 200);
            (fillFilter, fillRenderer, fillMesh) = CreatePart("Fill", order);
            (edgeFilter, edgeRenderer, edgeMesh) = CreatePart("Rim", order + 1);
            (outlineFilter, outlineRenderer, outlineMesh) = CreatePart("Outline", order + 2);
            Hide();
        }

        private (MeshFilter, MeshRenderer, Mesh) CreatePart(string partName, int order) {
            var part = new GameObject(partName);
            part.transform.SetParent(transform, false);
            var filter = part.AddComponent<MeshFilter>();
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            var mesh = new Mesh { name = $"Arrow{partName}" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            return (filter, renderer, mesh);
        }

        public void SetColor(Color newColor) {
            color = newColor;
            if (isVisible) Redraw();
        }

        public void SetStyle(Style newStyle) {
            style = newStyle;
            if (isVisible) Redraw();
        }

        /// <summary>Shows the arrow between two world positions; drawIn animates it growing from its origin.</summary>
        public void Show(Vector3 start, Vector3 end, bool drawIn = false) => Show(new[] { start }, end, drawIn);

        /// <summary>Shows one arrow from each start, merging into one trunk and head at the end.</summary>
        public void Show(IReadOnlyList<Vector3> fromPositions, Vector3 end, bool drawIn = false) {
            starts.Clear();
            foreach (var start in fromPositions) starts.Add(new Vector3(start.x, start.y, 0f));
            endPosition = new Vector3(end.x, end.y, 0f);
            isVisible = true;
            gameObject.SetActive(true);

            if (drawIn) {
                DOTween.Kill(this);
                drawnFraction = 0f;
                DOTween.To(() => drawnFraction, f => { drawnFraction = f; Redraw(); }, 1f, DrawInDuration)
                    .SetEase(Ease.OutCubic).SetTarget(this);
            }
            Redraw();
        }

        public void UpdateEndPosition(Vector3 newEnd) {
            if (!isVisible) return;
            endPosition = new Vector3(newEnd.x, newEnd.y, 0f);
            Redraw();
        }

        public void Hide() {
            isVisible = false;
            DOTween.Kill(this);
            gameObject.SetActive(false);
        }

        /// <summary>Fades the arrow out, then destroys it.</summary>
        public void FadeOutAndDestroy() {
            if (isFadingOut) return;
            isFadingOut = true;
            DOTween.Kill(this);
            DOTween.To(() => opacity, o => { opacity = o; Redraw(); }, 0f, FadeOutDuration)
                .SetTarget(this)
                .OnComplete(() => Destroy(gameObject));
        }

        public bool IsVisible() => isVisible;
        public bool IsFadingOut => isFadingOut;
        /// <summary>Where it was asked to start (the first start, when several merge) and end, before insets and lanes.</summary>
        public Vector3 StartPosition => starts.Count > 0 ? starts[0] : Vector3.zero;
        public Vector3 EndPosition => endPosition;
        public IReadOnlyList<Vector3> StartPositions => starts;
        /// <summary>How many arrows flow into this one's head.</summary>
        public int Branches => starts.Count;

        #region Drawing
        private void Redraw() {
            var fill = new MeshBuffer();
            var edge = new MeshBuffer();
            var outline = new MeshBuffer();
            if (starts.Count > 0) BuildShape(new Layers(fill, edge, outline));
            fill.WriteTo(fillMesh, transform);
            edge.WriteTo(edgeMesh, transform);
            outline.WriteTo(outlineMesh, transform);
        }

        // Where the arrow runs: insets and the lane first, then one curve (or branches flowing into a trunk), then the head
        private void BuildShape(Layers layers) {
            Vector2 end = endPosition;
            Vector2 centroid = Vector2.zero;
            foreach (var start in starts) centroid += (Vector2)start;
            centroid /= starts.Count;
            Vector2 travel = end - centroid;
            if (travel.magnitude < MinLength) return;
            Vector2 forward = travel.normalized;
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 axis = Mathf.Abs(forward.y) >= 0.35f ? new Vector2(0f, Mathf.Sign(forward.y)) : forward; // Rows face each other
            Vector2 tip = end - axis * style.EndInset + right * style.Lane;

            var tails = new List<Vector2>();
            foreach (var start in starts) tails.Add((Vector2)start + axis * style.StartInset + right * style.Lane);

            float width = style.Width;
            if (tails.Count == 1) {
                Vector2 from = tails[0];
                Vector2 span = tip - from;
                if (span.magnitude < MinLength) return;
                Vector2 side = new Vector2(span.normalized.y, -span.normalized.x);
                Vector2 bulge = side * Mathf.Min(span.magnitude * style.Bow, style.MaxBow) * 1.333f;
                var curve = Sample(from, from + span / 3f + bulge, from + span * 2f / 3f + bulge, tip);
                AddBandWithHead(layers, curve, drawnFraction, width * TailWidth, width, width, 0f, 1f, notched: true);
                return;
            }

            // Several starts: each flows along the rows' axis into a trunk that runs straight into the target
            float reach = Mathf.Abs(Vector2.Dot(tip - (centroid + axis * style.StartInset), axis));
            float headLength = HeadLengthOf(TrunkWidth(width, tails.Count));
            float trunkLength = Mathf.Max(reach * 0.5f, headLength + 0.3f);
            Vector2 merge = tip - axis * trunkLength;
            float branchProgress = Mathf.Clamp01(drawnFraction / BranchShare);
            foreach (var from in tails) {
                float rise = Mathf.Max(0.12f, Vector2.Dot(merge - from, axis));
                var branch = Sample(from, from + axis * rise * 0.45f, merge - axis * rise * 0.55f, merge + axis * 0.03f);
                AddBand(layers, Cut(branch, Length(branch) * branchProgress), width * TailWidth, width * 0.9f, 0f, BranchShare, notched: true);
            }
            float trunkProgress = Mathf.Clamp01((drawnFraction - BranchShare) / (1f - BranchShare));
            if (trunkProgress <= 0f) return;
            var trunk = Sample(merge, merge + axis * trunkLength / 3f, merge + axis * trunkLength * 2f / 3f, tip);
            AddBandWithHead(layers, trunk, trunkProgress, width * 0.9f, TrunkWidth(width, tails.Count), TrunkWidth(width, tails.Count),
                BranchShare, 1f, notched: false);
        }

        private static float TrunkWidth(float width, int branches) => width * Mathf.Min(MaxTrunkWidth, 1f + WidthPerExtraBranch * (branches - 1));
        private static float HeadLengthOf(float width) => Mathf.Min(MaxHeadLength, width * HeadLength);

        // A band along the curve, as far as it's drawn in, ending in the head
        private void AddBandWithHead(Layers layers, List<Vector2> curve, float progress, float fromWidth, float toWidth,
                                     float headWidth, float fromU, float toU, bool notched) {
            var drawn = Cut(curve, Length(curve) * progress);
            float length = Length(drawn);
            if (length < 0.02f) return;
            Vector2 tip = drawn[drawn.Count - 1];
            Vector2 direction = (tip - drawn[drawn.Count - 2]).normalized;
            float headLength = Mathf.Min(HeadLengthOf(headWidth), length * 0.6f);
            float headHalf = Mathf.Min(MaxHeadHalfWidth, headWidth * HeadHalfWidth) * Mathf.Clamp01(headLength / HeadLengthOf(headWidth) * 1.4f);
            var body = Cut(drawn, length - headLength * 0.98f);
            AddBand(layers, body, fromWidth, toWidth, fromU, Mathf.Lerp(fromU, toU, 0.9f), notched);
            AddHead(layers.Fill, tip, direction, headLength, headHalf, toWidth / 2f, 0f, Fill(HeadAlpha));
            AddHead(layers.Rim, tip, direction, headLength, headHalf, toWidth / 2f, RimWidth, Rim(EdgeAlpha));
            AddHead(layers.Outline, tip, direction, headLength, headHalf, toWidth / 2f, OutlineWidth, Outline(OutlineAlpha));
        }

        private void AddBand(Layers layers, List<Vector2> points, float fromWidth, float toWidth, float fromU, float toU, bool notched) {
            if (points.Count < 2 || Length(points) < 0.01f) return;
            AddRibbon(layers.Fill, points, fromWidth / 2f, toWidth / 2f, fromU, toU, notched, 0f, u => Fill(FillAlpha(u)));
            AddRibbon(layers.Rim, points, fromWidth / 2f, toWidth / 2f, fromU, toU, notched, RimWidth, u => Rim(EdgeStrength(u) * EdgeAlpha));
            AddRibbon(layers.Outline, points, fromWidth / 2f, toWidth / 2f, fromU, toU, notched, OutlineWidth, u => Outline(EdgeStrength(u) * OutlineAlpha));
        }

        // The edges fade toward the tail like the fill
        private static float EdgeStrength(float u) => Mathf.Lerp(EdgeTailAlpha, 1f, Mathf.Clamp01(u * 1.6f));

        // The three parts of an arrow, drawn in this order
        private readonly struct Layers {
            public readonly MeshBuffer Fill, Rim, Outline;
            public Layers(MeshBuffer fill, MeshBuffer rim, MeshBuffer outline) {
                Fill = fill;
                Rim = rim;
                Outline = outline;
            }
        }

        // Faint at the tail, strongest by the head: the direction reads even without seeing the head
        private static float FillAlpha(float u) => u < 0.5f ? Mathf.Lerp(TailAlpha, BodyAlpha, u / 0.5f) : Mathf.Lerp(BodyAlpha, NearHeadAlpha, (u - 0.5f) / 0.5f);

        private Color Fill(float alpha) => new Color(color.r, color.g, color.b, alpha * opacity);
        private Color Rim(float alpha) {
            var bright = Color.Lerp(color, Color.white, 0.22f);
            return new Color(bright.r, bright.g, bright.b, alpha * opacity);
        }

        private Color Outline(float alpha) {
            var dark = Color.Lerp(color, Color.black, 0.75f);
            return new Color(dark.r, dark.g, dark.b, alpha * opacity);
        }

        // A band three vertices wide (left edge, middle, right edge), narrowing to the tail. Its tail is a swallowtail: the
        // middle starts further in than the edges. Grown by expand all round, it makes the edge drawn around the fill.
        private static void AddRibbon(MeshBuffer mesh, List<Vector2> points, float fromHalf, float toHalf, float fromU, float toU,
                                      bool notched, float expand, System.Func<float, Color> colorAt) {
            var path = new List<Vector2>(points);
            if (expand > 0f && notched) path.Insert(0, path[0] - (path[1] - path[0]).normalized * expand); // The edge wraps the tail
            var lengths = CumulativeLengths(path);
            float total = lengths[lengths.Count - 1];
            if (total <= 0f) return;
            float notch = notched ? Mathf.Max(0f, fromHalf * NotchDepth - expand * 0.5f) : 0f;
            Vector2 notchPoint = PointAt(path, lengths, Mathf.Min(notch, total * 0.4f));

            int first = mesh.Vertices.Count;
            for (int i = 0; i < path.Count; i++) {
                Vector2 tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float u = lengths[i] / total;
                float half = Mathf.Lerp(fromHalf, toHalf, Mathf.SmoothStep(0f, 1f, u)) + expand;
                Vector2 middle = lengths[i] < notch ? notchPoint : path[i];
                var vertexColor = colorAt(Mathf.Lerp(fromU, toU, u));
                mesh.Add(path[i] + normal * half, vertexColor);
                mesh.Add(middle, vertexColor);
                mesh.Add(path[i] - normal * half, vertexColor);
            }
            for (int i = 0; i < path.Count - 1; i++) {
                int a = first + i * 3, b = a + 3; // Left, middle, right of this point and the next
                mesh.Triangle(a, a + 1, b);
                mesh.Triangle(a + 1, b + 1, b);
                mesh.Triangle(a + 1, a + 2, b + 1);
                mesh.Triangle(a + 2, b + 2, b + 1);
            }
        }

        // A barbed head: two swept-back wings either side of the band's end. Grown by expand for the edge
        private static void AddHead(MeshBuffer mesh, Vector2 tip, Vector2 direction, float length, float halfWidth, float shoulderHalf,
                                    float expand, Color headColor) {
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2 back = tip - direction * length;
            Vector2 point = tip + direction * expand * 2.2f;
            Vector2 barbLeft = back - direction * (length * BarbSweep + expand * 0.6f) + normal * (halfWidth + expand * 1.4f);
            Vector2 barbRight = back - direction * (length * BarbSweep + expand * 0.6f) - normal * (halfWidth + expand * 1.4f);
            Vector2 shoulderLeft = back - direction * expand * 0.5f + normal * (shoulderHalf + expand);
            Vector2 shoulderRight = back - direction * expand * 0.5f - normal * (shoulderHalf + expand);
            int t = mesh.Add(point, headColor);
            int bl = mesh.Add(barbLeft, headColor), sl = mesh.Add(shoulderLeft, headColor);
            int sr = mesh.Add(shoulderRight, headColor), br = mesh.Add(barbRight, headColor);
            mesh.Triangle(t, bl, sl);
            mesh.Triangle(t, sl, sr);
            mesh.Triangle(t, sr, br);
        }
        #endregion

        #region Curves
        private static List<Vector2> Sample(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
            var points = new List<Vector2>(Samples + 1);
            for (int i = 0; i <= Samples; i++) {
                float t = (float)i / Samples, u = 1f - t;
                points.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
            }
            return points;
        }

        private static List<float> CumulativeLengths(List<Vector2> points) {
            var lengths = new List<float>(points.Count) { 0f };
            for (int i = 1; i < points.Count; i++) lengths.Add(lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]));
            return lengths;
        }

        private static float Length(List<Vector2> points) {
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }

        private static Vector2 PointAt(List<Vector2> points, List<float> lengths, float at) {
            for (int i = 1; i < points.Count; i++) {
                if (lengths[i] >= at) return Vector2.Lerp(points[i - 1], points[i], Mathf.InverseLerp(lengths[i - 1], lengths[i], at));
            }
            return points[points.Count - 1];
        }

        // The curve up to this length along it
        private static List<Vector2> Cut(List<Vector2> points, float length) {
            var cut = new List<Vector2> { points[0] };
            float walked = 0f;
            for (int i = 1; i < points.Count; i++) {
                float step = Vector2.Distance(points[i - 1], points[i]);
                if (walked + step >= length) {
                    if (length > walked) cut.Add(Vector2.Lerp(points[i - 1], points[i], (length - walked) / step));
                    break;
                }
                walked += step;
                cut.Add(points[i]);
            }
            return cut;
        }
        #endregion

        private class MeshBuffer {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();

            public int Add(Vector2 position, Color vertexColor) {
                Vertices.Add(new Vector3(position.x, position.y, 0f));
                colors.Add(vertexColor);
                return Vertices.Count - 1;
            }

            public void Triangle(int a, int b, int c) {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }

            // The shape is worked out on the table (world space, flat at z 0); the mesh is in the arrow's own space
            public void WriteTo(Mesh mesh, Transform space) {
                for (int i = 0; i < Vertices.Count; i++) Vertices[i] = space.InverseTransformPoint(Vertices[i]);
                mesh.Clear();
                mesh.SetVertices(Vertices);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
            }
        }

        private void OnDestroy() {
            DOTween.Kill(this);
            if (material != null) Destroy(material);
            if (fillMesh != null) Destroy(fillMesh);
            if (edgeMesh != null) Destroy(edgeMesh);
            if (outlineMesh != null) Destroy(outlineMesh);
        }
    }
}
