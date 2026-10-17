using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Zeitgeist {
    /// <summary>
    /// The little flourishes cards make when they leave the hand, in the spirit of Slay the Spire: a discarded card fades
    /// away into a glowing dart that streaks into the discard pile, leaving a trail; a cast card bursts in a ring of light.
    /// Everything is made on the given canvas and cleans itself up.
    /// </summary>
    public static class CardFx {
        private static readonly Color DartColor = new Color(1f, 0.86f, 0.45f);
        private const float FadeSeconds = 0.16f;  // The card fading into the dart
        private const float FlySeconds = 0.38f;   // The dart's flight
        private const float TrailSeconds = 0.22f; // How long each speck of the trail lingers

        /// <summary>
        /// Fades the card out where it is and sends a dart from there to <paramref name="worldTarget"/> (the discard pile),
        /// which gives a small bump when it lands. The card is destroyed.
        /// </summary>
        public static void Discard(CardController card, Transform flyRoot, Vector3 worldTarget, Transform pile, float delay) {
            if (card == null) return;
            var group = card.GetComponent<CanvasGroup>();
            if (group != null) group.blocksRaycasts = false;
            Tooltip.Existing?.HideTooltipFor(card);
            float unit = UnitOf(card.transform.parent, flyRoot);
            card.transform.SetParent(flyRoot, true);
            card.transform.SetAsLastSibling();
            DOTween.Kill(card.transform);

            Vector3 from = card.transform.position;
            var fade = DOTween.Sequence().SetTarget(card.transform).AppendInterval(delay)
                .Append(card.transform.DOScale(card.transform.localScale * 0.35f, FadeSeconds).SetEase(Ease.InQuad))
                .Join(card.transform.DORotate(new Vector3(0f, 0f, card.transform.eulerAngles.z + 25f), FadeSeconds));
            if (group != null) fade.Join(DOTween.To(() => group.alpha, a => group.alpha = a, 0f, FadeSeconds));
            fade.OnComplete(() => {
                var runner = flyRoot.GetComponent<FxRunner>();
                if (runner == null) runner = flyRoot.gameObject.AddComponent<FxRunner>();
                runner.StartCoroutine(FlyDart(flyRoot, from, worldTarget, pile, unit));
                Object.Destroy(card.gameObject);
            });
        }

        // The dart: a bright sliver curving to the pile, shedding specks that fade behind it
        private static IEnumerator FlyDart(Transform root, Vector3 from, Vector3 to, Transform pile, float unit) {
            var dart = Speck(root, DartColor, new Vector2(46f, 12f) * unit, 32f / 12f);
            to.z = from.z;
            // A bow to the side, like a thrown dart
            Vector3 control = (from + to) * 0.5f + Vector3.up * Mathf.Max(0.6f, Vector3.Distance(from, to) * 0.35f);
            float t = 0f;
            Vector3 last = from;
            while (t < 1f && dart != null) {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / FlySeconds);
                float eased = t * t * (3f - 2f * t) * 0.4f + t * t * 0.6f; // Picks up speed as it goes
                Vector3 point = Bezier(from, control, to, eased);
                dart.rectTransform.position = point;
                Vector3 heading = point - last;
                if (heading.sqrMagnitude > 0.000001f) dart.rectTransform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
                last = point;

                var speck = Speck(root, DartColor, new Vector2(16f, 16f) * unit, 2f);
                speck.rectTransform.position = point;
                speck.rectTransform.DOScale(0.1f, TrailSeconds).SetEase(Ease.InQuad);
                Fade(speck, TrailSeconds).OnComplete(() => { if (speck != null) Object.Destroy(speck.gameObject); });
                yield return null;
            }
            if (dart != null) Object.Destroy(dart.gameObject);
            if (pile != null) {
                pile.DOKill(complete: true);
                pile.DOPunchScale(pile.localScale * 0.12f, 0.25f, 8, 0.6f); // Relative: the button is tiny in the root's units
            }
        }

        /// <summary>
        /// How many of the root's units make one unit of the space the cards are laid out in (the table's canvas is scaled
        /// down under the world-space root), so the effects are sized like the cards.
        /// </summary>
        public static float UnitOf(Transform cardSpace, Transform root) =>
            cardSpace != null && root != null ? cardSpace.lossyScale.x / Mathf.Max(0.0001f, root.lossyScale.x) : 1f;

        /// <summary>A ring of light that swells and fades where a spell is cast.</summary>
        public static void Burst(Transform root, Vector3 worldPoint, float delay, float unit) {
            var ring = Speck(root, DartColor, new Vector2(120f, 120f) * unit, 32f / 120f);
            ring.sprite = UITheme.RoundedOutline; // A ring, not a disc
            ring.rectTransform.position = worldPoint;
            ring.rectTransform.localScale = Vector3.one * 0.2f;
            var color = ring.color;
            ring.color = new Color(color.r, color.g, color.b, 0f);
            DOTween.Sequence().SetTarget(ring.rectTransform).AppendInterval(delay)
                .AppendCallback(() => ring.color = color)
                .Append(ring.rectTransform.DOScale(2.6f, 0.35f).SetEase(Ease.OutCubic))
                .Join(Fade(ring, 0.35f).SetEase(Ease.InQuad))
                .OnComplete(() => { if (ring != null) Object.Destroy(ring.gameObject); });
        }

        private static Tween Fade(Graphic graphic, float seconds) =>
            DOTween.To(() => graphic.color.a, a => { if (graphic != null) graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, a); }, 0f, seconds)
                .SetTarget(graphic);

        private static Image Speck(Transform root, Color color, Vector2 size, float cornerScale) {
            var image = UITheme.NewImage("CardFx", root, color);
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = size;
            UITheme.Round(image, color, cornerScale); // cornerScale sets the corners: about 64 / its size makes it round
            image.transform.SetAsLastSibling();
            return image;
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /// <summary>Runs the effects' coroutines on a canvas that has nothing else to run them.</summary>
        private class FxRunner : MonoBehaviour { }
    }
}
