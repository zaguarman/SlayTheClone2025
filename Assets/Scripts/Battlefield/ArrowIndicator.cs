using System.Collections.Generic;
using System.Linq;
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
        /// <summary>How an arrow sits between its ends, in world units. Its numbers come from <see cref="ArrowTuning"/>, by kind.</summary>
        public struct Style {
            public enum Kind { Attack, Damage, Move, Drag }
            public Kind Of;
            private bool toEdge, centered;
            public float Width;      // The band's width where it meets the head (a trunk merging several is wider)
            public float Lane;       // Shift to the right of travel, so arrows going opposite ways pass side by side
            public float Bow;        // Bulge to the right of travel, as a fraction of its length...
            public float MaxBow;     // ...and at most this much
            public float StartInset; // From each start toward the target: the tail starts near the card's edge, not its middle
            public float EndInset;   // Back from the end: the head stops at the target's near edge, clear of its stats

            // Cards are 1.6 x 1.96 units, and the rows 1.26 apart
            public static Style Attack => From(Kind.Attack);
            public static Style Damage => From(Kind.Damage);
            public static Style Move => From(Kind.Move);
            public static Style Drag => From(Kind.Drag);

            private static Style From(Kind kind) {
                var t = ArrowTuning.Current;
                switch (kind) {
                    case Kind.Damage: return new Style { Of = kind, Width = t.damageWidth, Lane = 0f, Bow = t.damageBow, MaxBow = 0.35f * t.maxBowScale, StartInset = t.damageStartY, EndInset = t.damageEndY };
                    case Kind.Move: return new Style { Of = kind, Width = t.moveWidth, Lane = 0f, Bow = t.moveBow, MaxBow = 0.42f * t.maxBowScale, StartInset = t.moveStartY, EndInset = t.moveEndY };
                    case Kind.Drag: return new Style { Of = kind, Width = t.dragWidth, Lane = 0f, Bow = t.dragBow, MaxBow = 0.4f * t.maxBowScale, StartInset = t.dragStartY, EndInset = t.dragEndY };
                    default: return new Style { Of = kind, Width = t.attackWidth, Lane = t.attackLane, Bow = t.attackBow, MaxBow = 0.35f * t.maxBowScale, StartInset = t.attackStartY, EndInset = t.attackEndY };
                }
            }

            /// <summary>The same style with the tuning as it is now (the sliders may have moved since it was made).</summary>
            public Style Resolved() {
                var style = From(Of);
                if (centered) style = style.Centered();
                if (toEdge) style = style.ToEdge();
                return style;
            }

            /// <summary>Down the middle, no lane: a forked arrow stays symmetrical when its targets are.</summary>
            public Style Centered() {
                var style = this;
                style.centered = true;
                style.Lane = 0f;
                return style;
            }

            /// <summary>To the edge of a whole battlefield: the tip lands exactly there (between two slots), no lane.</summary>
            public Style ToEdge() {
                var style = this;
                style.toEdge = true;
                style.EndInset = 0f;
                style.Lane = 0f;
                return style;
            }
        }

        // Shape
        private const int Samples = 64;             // Points along each curve
        private const float PointSpacing = 0.035f;  // A band's points are spaced evenly along it, this far apart (world units)
        // The rest are sliders (see ArrowTuning)
        private static ArrowTuning T => ArrowTuning.Current;
        private static float BendRoom => T.bendRoom;           // A band is never wider than this share of its bend's radius: it can't fold over
        private static float TailWidth => T.tailWidth;         // Of the band's width, at its tail
        private static float NotchDepth => T.notchDepth;       // The swallowtail's depth, in half-widths
        private static float HeadLength => T.headLength;       // In band widths
        private static float HeadHalfWidth => T.headHalfWidth; // In band widths
        private static float MaxHeadLength => T.maxHeadLength;
        private static float MaxHeadHalfWidth => T.maxHeadHalfWidth;
        private static float BarbSweep => T.barbSweep;         // How far back the barbs reach, in head lengths
        private static float RimWidth => T.rimWidth;           // A bright line round the band: it reads on the dark table...
        private static float OutlineWidth => T.outlineWidth;   // ...and a thin dark one outside it, on the light cards
        private static float WidthPerExtraBranch => T.widthPerExtraBranch; // In band widths: the trunk stays lean as attackers join...
        private static float MaxTrunkWidth => T.maxTrunkWidth;
        private static float BranchWidthAtMerge => T.branchWidthAtMerge;   // ...and the branches flow into it this wide
        private static float TrunkShare => T.trunkShare;       // The merged trunk, as a share of the way to the target
        private static float CornerRadius => T.cornerRadius;   // A branch's bends are rounded this much at most (world units), like a PCB track's
        private static float RouteSlope => Mathf.Tan(T.routeAngle * Mathf.Deg2Rad); // A routed branch's diagonal: across per along
        private const float BranchShare = 0.55f;    // How much of the draw-in the branches take before the trunk
        private const float ForkShare = 0.4f;       // A forked arrow: how much of the draw-in its trunk takes before the prongs
        private static float ProngWidth => T.prongWidth;       // A fork's prongs, of the trunk's width: the middle one too, a small arrow
        private static float ForkAt => T.forkAt;               // Where the trunk forks, as a share of the way to the nearest target
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
        private readonly List<Vector3> forks = new List<Vector3>(); // More ends: the trunk splits like a Y (Spray and Pray)
        private Vector3 endPosition;
        private Color color = Color.red;
        private Style style; // An attack's until set (no tuning read here: a MonoBehaviour can't load resources while it's made)
        private float drawnFraction = 1f; // Draw-in progress
        private float opacity = 1f;
        private bool isVisible;
        private bool isFadingOut;

        // Every arrow there is, so a window over the table can hide them all at once
        private static readonly HashSet<ArrowIndicator> all = new HashSet<ArrowIndicator>();
        private static bool suppressed;

        /// <summary>
        /// While a window covers the table (a pile, a pick), every arrow is hidden: they're drawn over the interface.
        /// They come back as they were when it's off.
        /// </summary>
        public static bool Suppressed {
            get => suppressed;
            set {
                if (suppressed == value) return;
                suppressed = value;
                foreach (var arrow in all) arrow.ApplySuppressed();
            }
        }

        private void ApplySuppressed() {
            foreach (var part in new Renderer[] { fillRenderer, edgeRenderer, outlineRenderer }) {
                if (part != null) part.forceRenderingOff = suppressed;
            }
            foreach (var label in labels) { // The order numbers too: they're drawn over the interface as well
                if (label != null && label.TryGetComponent<MeshRenderer>(out var text)) text.forceRenderingOff = suppressed;
            }
        }

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
            all.Add(this);
            ApplySuppressed();
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

        public Color Color => color;

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

        /// <summary>Redraws every arrow there is: an <see cref="ArrowTuning"/> slider moved.</summary>
        public static void RedrawAll() {
            foreach (var arrow in all) {
                if (arrow != null && arrow.isVisible) arrow.Redraw();
            }
        }

        public bool IsVisible() => isVisible;
        public bool IsFadingOut => isFadingOut;
        /// <summary>Where it was asked to start (the first start, when several merge) and end, before insets and lanes.</summary>
        // The order labels: a number by each start, telling when that attack resolves
        private const float LabelSize = 3.2f;   // TextMeshPro size: about a third of a world unit tall
        private const float LabelOffset = 0.55f; // From the start toward the end, on the card's edge
        private readonly List<TMPro.TextMeshPro> labels = new List<TMPro.TextMeshPro>();

        /// <summary>The numbers shown by the arrow's starts (one per start; null or empty shows none).</summary>
        public IReadOnlyList<string> Labels => labels.Where(l => l != null && l.gameObject.activeSelf).Select(l => Keywords.Plain(l.text)).ToList();

        public void SetLabels(IReadOnlyList<string> texts) {
            for (int i = 0; i < Mathf.Max(texts?.Count ?? 0, labels.Count); i++) {
                string text = texts != null && i < texts.Count && i < starts.Count ? texts[i] : null;
                if (i >= labels.Count) labels.Add(null);
                if (string.IsNullOrEmpty(text)) {
                    if (labels[i] != null) labels[i].gameObject.SetActive(false);
                    continue;
                }
                var label = labels[i] ?? (labels[i] = NewLabel());
                label.gameObject.SetActive(true);
                label.text = $"<mark=#141420D0 padding=\"14,14,4,4\">{text}</mark>"; // A dark badge: it reads on art and parchment alike
                var direction = (endPosition - starts[i]);
                direction.z = 0f;
                label.transform.position = starts[i] + (direction.sqrMagnitude > 0.0001f ? direction.normalized * LabelOffset : Vector3.zero) + new Vector3(0f, 0f, -0.01f);
                float scale = transform.lossyScale.x > 0 ? 1f / transform.lossyScale.x : 1f;
                label.transform.localScale = Vector3.one * scale;
                label.color = new Color(1f, 1f, 1f, opacity);
            }
        }

        private TMPro.TextMeshPro NewLabel() {
            var labelObject = new GameObject("OrderLabel");
            labelObject.transform.SetParent(transform, false);
            var label = labelObject.AddComponent<TMPro.TextMeshPro>();
            label.fontSize = LabelSize;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
            label.outlineWidth = 0.3f;
            label.outlineColor = new Color32(20, 20, 30, 255);
            label.sortingOrder = SortingOrder + 700; // Over every arrow
            if (label.TryGetComponent<MeshRenderer>(out var text)) text.forceRenderingOff = suppressed;
            return label;
        }

        /// <summary>
        /// The other targets an attack hits as it lands (Spray and Pray): the arrow's trunk splits like a Y, a prong to each
        /// of them and a small one on to its own end. None: a plain arrow.
        /// </summary>
        public void SetForks(IEnumerable<Vector3> ends) {
            forks.Clear();
            if (ends != null) foreach (var end in ends) forks.Add(new Vector3(end.x, end.y, 0f));
            if (isVisible) Redraw();
        }

        public IReadOnlyList<Vector3> Forks => forks;

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
            var style = this.style.Resolved();
            if (forks.Count > 0 && starts.Count == 1) {
                BuildFork(layers);
                return;
            }
            Vector2 end = endPosition;
            Vector2 centroid = Vector2.zero;
            foreach (var start in starts) centroid += (Vector2)start;
            centroid /= starts.Count;
            Vector2 travel = end - centroid;
            if (travel.magnitude < MinLength) return;
            Vector2 forward = travel.normalized;
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 axis = Mathf.Abs(forward.y) >= 0.35f ? new Vector2(0f, Mathf.Sign(forward.y)) : forward; // Rows face each other
            Vector2 tip = end - axis * style.EndInset + right * (style.Lane + T.endX);

            var tails = new List<Vector2>();
            foreach (var start in starts) tails.Add((Vector2)start + axis * style.StartInset + right * (style.Lane + T.startX));

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
            // The trunk gives the branches room for 45° when they come from far to the side, but keeps room for the head
            Vector2 acrossAxis = new Vector2(axis.y, -axis.x);
            float widest = 0f;
            foreach (var from in tails) widest = Mathf.Max(widest, Mathf.Abs(Vector2.Dot(tip - from, acrossAxis)));
            float trunkLength = Mathf.Max(Mathf.Min(reach * TrunkShare, reach - widest / RouteSlope - 0.3f), headLength + 0.3f);
            Vector2 merge = tip - axis * trunkLength;
            float branchProgress = Mathf.Clamp01(drawnFraction / BranchShare);
            foreach (var from in tails) {
                var branch = Route(from, merge + axis * 0.03f, axis);
                AddBand(layers, Cut(branch, Length(branch) * branchProgress), width * TailWidth, width * BranchWidthAtMerge, 0f, BranchShare, notched: true);
            }
            float trunkProgress = Mathf.Clamp01((drawnFraction - BranchShare) / (1f - BranchShare));
            if (trunkProgress <= 0f) return;
            var trunk = Sample(merge, merge + axis * trunkLength / 3f, merge + axis * trunkLength * 2f / 3f, tip);
            AddBandWithHead(layers, trunk, trunkProgress, width * BranchWidthAtMerge, TrunkWidth(width, tails.Count), TrunkWidth(width, tails.Count),
                BranchShare, 1f, notched: false);
        }

        // A trunk straight out along the rows' axis, splitting like a Y: a prong to each target, the middle one a small arrow
        // on to its own end. Every prong is routed the same way (no lane, no bow), so mirrored targets get a mirrored fork
        private void BuildFork(Layers layers) {
            var style = this.style.Resolved();
            Vector2 from = starts[0], end = endPosition;
            Vector2 travel = end - from;
            if (travel.magnitude < MinLength) return;
            Vector2 forward = travel.normalized;
            Vector2 axis = Mathf.Abs(forward.y) >= 0.35f ? new Vector2(0f, Mathf.Sign(forward.y)) : forward; // Rows face each other
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 tail = from + axis * style.StartInset + right * T.startX;
            var tips = new List<Vector2> { end - axis * style.EndInset + right * T.endX };
            foreach (var fork in forks) tips.Add((Vector2)fork - axis * style.EndInset + right * T.endX);

            float nearest = tips.Min(t => Vector2.Dot(t - tail, axis));
            if (nearest < MinLength) return;
            float width = style.Width;
            Vector2 split = tail + axis * nearest * ForkAt;
            var trunk = Sample(tail, Vector2.Lerp(tail, split, 1f / 3f), Vector2.Lerp(tail, split, 2f / 3f), split);
            float trunkProgress = Mathf.Clamp01(drawnFraction / ForkShare);
            AddBand(layers, Cut(trunk, Length(trunk) * trunkProgress), width * TailWidth, width, 0f, ForkShare, notched: true);

            float prongProgress = Mathf.Clamp01((drawnFraction - ForkShare) / (1f - ForkShare));
            if (prongProgress <= 0f) return;
            float prong = width * ProngWidth;
            // Each prong leaves the fork going forward and bends smoothly onto its target: a Y, not a T
            Vector2 root = split - axis * 0.03f;
            foreach (var tip in tips) {
                float rise = Mathf.Max(0.05f, Vector2.Dot(tip - root, axis));
                var route = Sample(root, root + axis * rise * T.prongLeave, tip - axis * rise * T.prongArrive, tip);
                AddBandWithHead(layers, route, prongProgress, prong, prong, prong, ForkShare, 1f, notched: false);
            }
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
            float headLength = Mathf.Min(HeadLengthOf(headWidth), length * 0.6f);
            // The head points along the last stretch of the curve, not its last (maybe tiny) step
            Vector2 direction = (tip - PointAt(drawn, CumulativeLengths(drawn), length - headLength * 0.6f)).normalized;
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
        // Round a tight bend only the inner edge gives way (so it can't fold over), and gradually: the outer edge keeps its
        // sweep, so there's no bottleneck and no kink.
        private static void AddRibbon(MeshBuffer mesh, List<Vector2> points, float fromHalf, float toHalf, float fromU, float toU,
                                      bool notched, float expand, System.Func<float, Color> colorAt) {
            var path = new List<Vector2>(points);
            if (expand > 0f && notched) path.Insert(0, path[0] - (path[1] - path[0]).normalized * expand); // The edge wraps the tail
            path = Even(path);
            var lengths = CumulativeLengths(path);
            float total = lengths[lengths.Count - 1];
            if (total <= 0f) return;
            float notch = notched ? Mathf.Max(0f, fromHalf * NotchDepth - expand * 0.5f) : 0f;
            Vector2 notchPoint = PointAt(path, lengths, Mathf.Min(notch, total * 0.4f));

            var room = InnerRoom(path);
            int first = mesh.Vertices.Count;
            for (int i = 0; i < path.Count; i++) {
                Vector2 tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float u = lengths[i] / total;
                float half = Mathf.Lerp(fromHalf, toHalf, Mathf.SmoothStep(0f, 1f, u)) + expand;
                float inner = Mathf.Min(half, room[i].radius * BendRoom + expand);
                float left = room[i].turnsLeft ? inner : half, right = room[i].turnsLeft ? half : inner;
                Vector2 middle = lengths[i] < notch ? notchPoint : path[i];
                var vertexColor = colorAt(Mathf.Lerp(fromU, toU, u));
                mesh.Add(path[i] + normal * left, vertexColor);
                mesh.Add(middle, vertexColor);
                mesh.Add(path[i] - normal * right, vertexColor);
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
        // A branch routed like a PCB track: straight out along the rows' axis, a 45° diagonal across (shallower only when
        // the rows are too close for 45°), then straight on into the merge; every corner rounded off, never a sharp turn
        private static List<Vector2> Route(Vector2 from, Vector2 to, Vector2 axis) {
            Vector2 across = new Vector2(axis.y, -axis.x);
            float rise = Vector2.Dot(to - from, axis), lateral = Vector2.Dot(to - from, across);
            if (Mathf.Abs(lateral) < 0.02f || rise <= 0.05f) return Sample(from, Vector2.Lerp(from, to, 1f / 3f), Vector2.Lerp(from, to, 2f / 3f), to);
            float stub = Mathf.Clamp(rise * 0.2f, 0.06f, 0.35f);
            float diagonal = Mathf.Min(Mathf.Max(rise - 2f * stub, 0.05f), Mathf.Abs(lateral) / RouteSlope); // Along the axis: at the tuned angle
            float before = (rise - diagonal) / 2f;
            Vector2 bendIn = from + axis * before;
            Vector2 bendOut = bendIn + axis * diagonal + across * lateral;
            return Rounded(new List<Vector2> { from, bendIn, bendOut, to });
        }

        // The polyline with each corner replaced by a curve tangent to both sides
        private static List<Vector2> Rounded(List<Vector2> corners) {
            var points = new List<Vector2> { corners[0] };
            for (int i = 1; i < corners.Count - 1; i++) {
                Vector2 previous = corners[i - 1], corner = corners[i], next = corners[i + 1];
                float cut = Mathf.Min(CornerRadius, Vector2.Distance(previous, corner) * 0.48f, Vector2.Distance(corner, next) * 0.48f);
                Vector2 enter = corner + (previous - corner).normalized * cut, leave = corner + (next - corner).normalized * cut;
                AddLine(points, enter);
                for (int k = 1; k <= 12; k++) { // A quadratic curve through the corner's control point
                    float t = k / 12f, u = 1f - t;
                    points.Add(u * u * enter + 2f * u * t * corner + t * t * leave);
                }
            }
            AddLine(points, corners[corners.Count - 1]);
            return points;
        }

        private static void AddLine(List<Vector2> points, Vector2 to) {
            Vector2 from = points[points.Count - 1];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / (PointSpacing * 0.5f)));
            for (int k = 1; k <= steps; k++) points.Add(Vector2.Lerp(from, to, (float)k / steps));
        }

        private static List<Vector2> Sample(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
            var points = new List<Vector2>(Samples + 1);
            for (int i = 0; i <= Samples; i++) {
                float t = (float)i / Samples, u = 1f - t;
                points.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
            }
            return points;
        }

        // The same curve through evenly spaced points: smooth edges, and no tiny step to throw a tangent off
        private static List<Vector2> Even(List<Vector2> points) {
            var lengths = CumulativeLengths(points);
            float total = lengths[lengths.Count - 1];
            if (total <= 0f) return points;
            int count = Mathf.Clamp(Mathf.CeilToInt(total / PointSpacing), 8, 160);
            var even = new List<Vector2>(count + 1);
            for (int i = 0; i <= count; i++) even.Add(PointAt(points, lengths, total * i / count));
            return even;
        }

        // How much room the inside of the bend leaves at each point, and which side the inside is: the tightest bend nearby,
        // eased in and out along the band so the inner edge never kinks
        private const int BendWindow = 6; // Points either side
        private static (float radius, bool turnsLeft)[] InnerRoom(List<Vector2> path) {
            int n = path.Count;
            var radius = new float[n];
            var turn = new float[n];
            for (int i = 0; i < n; i++) {
                radius[i] = BendRadius(path, i);
                int a = Mathf.Max(0, i - 3), c = Mathf.Min(n - 1, i + 3);
                Vector2 before = path[i] - path[a], after = path[c] - path[i];
                turn[i] = before.x * after.y - before.y * after.x; // Above 0: turning left
            }
            var tightest = new float[n];
            for (int i = 0; i < n; i++) {
                float min = float.MaxValue;
                for (int j = Mathf.Max(0, i - BendWindow); j <= Mathf.Min(n - 1, i + BendWindow); j++) min = Mathf.Min(min, radius[j]);
                tightest[i] = min;
            }
            var room = new (float, bool)[n];
            for (int i = 0; i < n; i++) {
                float sum = 0f;
                int count = 0;
                for (int j = Mathf.Max(0, i - BendWindow); j <= Mathf.Min(n - 1, i + BendWindow); j++) {
                    sum += Mathf.Min(tightest[j], 10f);
                    count++;
                }
                room[i] = (sum / count, turn[i] > 0f);
            }
            return room;
        }

        // How tightly the curve bends at this point: the radius of the circle through it and its neighbours
        private static float BendRadius(List<Vector2> path, int i) {
            if (i <= 0 || i >= path.Count - 1) return float.MaxValue;
            Vector2 a = path[i - 1], b = path[i], c = path[i + 1];
            float cross = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            if (cross < 1e-7f) return float.MaxValue;
            return Vector2.Distance(a, b) * Vector2.Distance(b, c) * Vector2.Distance(a, c) / (2f * cross);
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
            all.Remove(this);
            DOTween.Kill(this);
            if (material != null) Destroy(material);
            if (fillMesh != null) Destroy(fillMesh);
            if (edgeMesh != null) Destroy(edgeMesh);
            if (outlineMesh != null) Destroy(outlineMesh);
        }
    }
}
