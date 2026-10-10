using DG.Tweening;
using UnityEngine;

/// <summary>
/// A curved, tapered arrow drawn in world space: a soft glow, a dark outline, a body that fades in from
/// its origin with energy flowing toward the target, and a filled head. Queued arrows draw themselves in
/// when shown and fade out when removed.
/// </summary>
public class ArrowIndicator : MonoBehaviour {
    private const int Segments = 32;
    private const float BodyWidth = 0.15f;      // World units, at the head end of the body
    private const float BodyStartWidth = 0.35f; // Fraction of BodyWidth at the origin
    private const float OutlineWidth = 0.035f;
    private const float GlowWidthScale = 3.4f;
    private const float HeadLength = 0.42f;
    private const float HeadWidth = 0.4f;
    private const float Curvature = 0.2f;       // Sideways bulge as a fraction of the arrow's length
    private const float TargetInset = 0.25f;    // Stop short of the target's center so the head doesn't cover it
    private const float FlowSpeed = 1.4f;       // Texture cycles per second along the body
    private const float FlowCycleLength = 0.5f; // World units per flowing streak
    private const float DrawInDuration = 0.25f;
    private const float FadeOutDuration = 0.2f;
    private const int SortingOrder = 5000;

    private static readonly Color OutlineColor = new Color(0.07f, 0.04f, 0.06f, 0.9f);

    private static Texture2D flowTexture; // Bright streaks scrolled along the body
    private static Texture2D glowTexture; // Soft falloff across the glow's width
    private static Material plainMaterial;

    private LineRenderer glow;
    private LineRenderer outline;
    private LineRenderer body;
    private LineRenderer headOutline;
    private LineRenderer head;
    private Material bodyMaterial;
    private Material glowMaterial;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private Color color = Color.red;
    private float drawnFraction = 1f; // Draw-in progress along the curve
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
        EnsureSharedResources();
        bodyMaterial = new Material(plainMaterial) { mainTexture = flowTexture };
        glowMaterial = new Material(plainMaterial) { mainTexture = glowTexture };

        // Created back to front: later lines draw on top at the same sorting order
        glow = CreateLine("Glow", glowMaterial, 0);
        outline = CreateLine("Outline", plainMaterial, 1);
        body = CreateLine("Body", bodyMaterial, 2);
        headOutline = CreateLine("HeadOutline", plainMaterial, 3);
        head = CreateLine("Head", plainMaterial, 4);

        body.textureMode = LineTextureMode.Tile;
        glow.textureMode = LineTextureMode.Stretch;
        Hide();
    }

    private LineRenderer CreateLine(string lineName, Material material, int order) {
        var lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        var line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = material;
        line.sortingOrder = SortingOrder + order;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return line;
    }

    public void SetColor(Color newColor) {
        color = newColor;
        if (isVisible) Redraw();
    }

    /// <summary>Shows the arrow between two world positions; drawIn animates it growing from its origin.</summary>
    public void Show(Vector3 start, Vector3 end, bool drawIn = false) {
        startPosition = new Vector3(start.x, start.y, 0f);
        endPosition = new Vector3(end.x, end.y, 0f);
        isVisible = true;
        gameObject.SetActive(true);

        if (drawIn) {
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

    private void Update() {
        if (!isVisible) return;
        // Scroll the streaks from the origin toward the target
        bodyMaterial.mainTextureOffset -= new Vector2(FlowSpeed * Time.deltaTime, 0f);
    }

    private void Redraw() {
        Vector3 toEnd = endPosition - startPosition;
        float fullLength = toEnd.magnitude;
        if (fullLength < 0.05f) {
            SetLinesEnabled(false);
            return;
        }
        SetLinesEnabled(true);

        // Quadratic curve bowing to one consistent side
        Vector3 direction = toEnd / fullLength;
        Vector3 side = new Vector3(-direction.y, direction.x, 0f);
        if (side.y < 0f) side = -side;
        Vector3 tip = endPosition - direction * Mathf.Min(TargetInset, fullLength * 0.25f);
        Vector3 control = (startPosition + tip) * 0.5f + side * (fullLength * Curvature);

        // Body runs up to the head's base; the head points along the curve's direction at the tip
        float drawnEnd = Mathf.Max(0.01f, drawnFraction);
        Vector3 drawnTip = Bezier(startPosition, control, tip, drawnEnd);
        Vector3 tipDirection = BezierTangent(startPosition, control, tip, drawnEnd).normalized;
        float headLength = Mathf.Min(HeadLength, fullLength * 0.4f);
        Vector3 headBase = drawnTip - tipDirection * headLength;

        var points = new Vector3[Segments + 1];
        for (int i = 0; i <= Segments; i++) {
            points[i] = Bezier(startPosition, control, tip, drawnEnd * i / Segments);
        }
        points[Segments] = headBase;
        float bodyLength = 0f;
        for (int i = 1; i <= Segments; i++) bodyLength += Vector3.Distance(points[i - 1], points[i]);

        ApplyBody(glow, points, BodyWidth * GlowWidthScale, BodyStartWidth, Fade(color, 0.28f));
        ApplyBody(outline, points, BodyWidth + OutlineWidth * 2f, (BodyWidth * BodyStartWidth + OutlineWidth * 2f) / (BodyWidth + OutlineWidth * 2f), OutlineColor);
        ApplyBody(body, points, BodyWidth, BodyStartWidth, color);
        bodyMaterial.mainTextureScale = new Vector2(bodyLength / FlowCycleLength, 1f);

        ApplyHead(headOutline, headBase - tipDirection * OutlineWidth, drawnTip + tipDirection * OutlineWidth * 1.6f, HeadWidth + OutlineWidth * 3f, OutlineColor);
        ApplyHead(head, headBase, drawnTip, HeadWidth, Lighten(color));
    }

    // Tapered line that fades in from its origin
    private void ApplyBody(LineRenderer line, Vector3[] points, float width, float startWidthFraction, Color lineColor) {
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, startWidthFraction), new Keyframe(1f, 1f));
        line.colorGradient = FadeInGradient(lineColor);
    }

    // A two-point line whose width narrows to zero draws a filled triangle
    private void ApplyHead(LineRenderer line, Vector3 from, Vector3 to, float width, Color lineColor) {
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        line.numCapVertices = 0;
        var solid = Fade(lineColor, 1f);
        line.startColor = solid;
        line.endColor = solid;
    }

    private Gradient FadeInGradient(Color lineColor) {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(lineColor, 0f), new GradientColorKey(lineColor, 1f) },
            new[] {
                new GradientAlphaKey(lineColor.a * 0.15f * opacity, 0f),
                new GradientAlphaKey(lineColor.a * 0.85f * opacity, 0.35f),
                new GradientAlphaKey(lineColor.a * opacity, 1f),
            });
        return gradient;
    }

    private Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha * opacity);
    private static Color Lighten(Color c) => Color.Lerp(c, Color.white, 0.18f);

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, float t) =>
        2f * (1f - t) * (b - a) + 2f * t * (c - b);

    private void SetLinesEnabled(bool enabled) {
        glow.enabled = enabled;
        outline.enabled = enabled;
        body.enabled = enabled;
        headOutline.enabled = enabled;
        head.enabled = enabled;
    }

    private static void EnsureSharedResources() {
        if (plainMaterial == null) {
            plainMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Arrow" };
        }

        if (flowTexture == null) {
            // Along the body: mostly base brightness with one bright streak per cycle
            flowTexture = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "ArrowFlow" };
            for (int x = 0; x < 64; x++) {
                float streak = Mathf.Clamp01(1f - Mathf.Abs(x - 48f) / 10f);
                float brightness = Mathf.Lerp(0.8f, 1f, streak);
                flowTexture.SetPixel(x, 0, new Color(brightness, brightness, brightness, 1f));
            }
            flowTexture.Apply();
        }

        if (glowTexture == null) {
            // Across the glow: opaque in the middle, transparent at the edges
            glowTexture = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ArrowGlow" };
            for (int y = 0; y < 32; y++) {
                float fromCenter = Mathf.Abs(y / 31f * 2f - 1f);
                glowTexture.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, fromCenter)));
            }
            glowTexture.Apply();
        }
    }

    private void OnDestroy() {
        DOTween.Kill(this);
        if (bodyMaterial != null) Destroy(bodyMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
    }
}
