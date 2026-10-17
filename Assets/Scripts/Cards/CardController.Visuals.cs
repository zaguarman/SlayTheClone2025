using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>A card on screen: the visuals of playing it (a part of CardController).</summary>
    public partial class CardController {

        #region Play Visuals
        /// <summary>
        /// Shows the card as played-but-unresolved: translucent, pulsing, with a "QUEUED" badge (or another word, like
        /// "MOVING" on a move ghost).
        /// </summary>
        public void SetQueued(bool queued, bool blockRaycasts = true, bool canBeDragged = false, string badge = "Queued") {
            IsQueued = queued;
            canDragWhileQueued = queued && canBeDragged;
            queuedPulse?.Kill();
            queuedPulse = null;

            if (queued) {
                canvasGroup.alpha = QueuedAlphaHigh;
                canvasGroup.blocksRaycasts = blockRaycasts;
                queuedPulse = FadeTo(QueuedAlphaLow, 0.8f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetTarget(transform);
                if (queuedBadge == null) queuedBadge = CreateBadge("QueuedBadge", badge, UITheme.Gold, 0.4f, 0.6f);
                queuedBadge.GetComponentInChildren<TextMeshProUGUI>().text = badge;
                queuedBadge.SetActive(true);
            }
            else {
                canvasGroup.alpha = RestingAlpha;
                canvasGroup.blocksRaycasts = true;
                if (queuedBadge != null) queuedBadge.SetActive(false);
            }
        }

        /// <summary>
        /// A creature still deploying (the user): a see-through hourglass over the middle of its art, 80% of its height,
        /// and the card faded like a queued one, so it reads as not usable yet.
        /// </summary>
        public void SetDeploying(bool deploying) {
            if (deploying == IsDeploying && (!deploying || deployingBadge != null)) return;
            IsDeploying = deploying;
            if (deploying && deployingBadge == null) deployingBadge = CreateHourglass();
            if (deployingBadge != null) deployingBadge.SetActive(deploying);
            if (summonTween == null || !summonTween.IsActive()) canvasGroup.alpha = RestingAlpha; // Else it ends there
        }

        public bool IsDeploying { get; private set; }
        private GameObject deployingBadge;
        private Sequence summonTween;

        // How see-through the card is when nothing animates it: queued cards pulse on their own
        private float RestingAlpha => IsLeavingPlay ? canvasGroup.alpha
            : IsQueued ? QueuedAlphaHigh
            : isMarkedForReplacement || isReplacementPreviewed ? ReplacedAlpha
            : IsDeploying ? DeployingAlpha : 1f;

        // The Deployment Time's hourglass (the effect icon), centred on the art, 80% of its height
        private GameObject CreateHourglass() {
            if (artImage == null) return null;
            var artFrame = artImage.transform.parent;
            var image = UITheme.NewImage("DeployingHourglass", artFrame, UITheme.WithAlpha(Color.white, HourglassAlpha));
            image.sprite = HourglassSprite;
            image.preserveAspect = true;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.1f);
            rect.anchorMax = new Vector2(1f, 0.9f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var under = windowTrim != null ? windowTrim.transform : artImage.transform;
            image.transform.SetSiblingIndex(under.GetSiblingIndex() + 1); // Over the art and its window, under the status icons
            return image.gameObject;
        }

        private static Sprite hourglassSprite;

        // Cut from the effect icons' sprite sheet (TMP's default sprite asset), shared by every card
        private static Sprite HourglassSprite {
            get {
                if (hourglassSprite != null) return hourglassSprite;
                var icons = TMP_Settings.defaultSpriteAsset;
                if (icons == null || !(icons.spriteSheet is Texture2D sheet)) return null;
                foreach (var character in icons.spriteCharacterTable) {
                    if (character.name != EffectIcons.Days || character.glyph == null) continue;
                    var r = character.glyph.glyphRect;
                    hourglassSprite = Sprite.Create(sheet, new Rect(r.x, r.y, r.width, r.height), new Vector2(0.5f, 0.5f));
                    break;
                }
                return hourglassSprite;
            }
        }

        /// <summary>
        /// Marks this battlefield card as replaced by a queued summon into its slot.
        /// </summary>
        public void SetMarkedForReplacement(bool marked) {
            isMarkedForReplacement = marked;
            RefreshReplacementVisual();
        }

        /// <summary>
        /// Previews the replacement while a creature from hand is dragged over this card's slot.
        /// </summary>
        public void SetReplacementPreview(bool previewed) {
            isReplacementPreviewed = previewed;
            RefreshReplacementVisual();
        }

        private void RefreshReplacementVisual() {
            if (IsLeavingPlay) return;

            bool show = isMarkedForReplacement || isReplacementPreviewed;
            canvasGroup.alpha = RestingAlpha;
            // Bottom strip, so it stays visible under the raised ghost of the incoming creature
            if (show && replaceBadge == null) replaceBadge = CreateBadge("ReplaceBadge", "Replace", ReplaceLabelColor, 0.02f, 0.22f);
            if (replaceBadge != null) replaceBadge.SetActive(show);
        }

        /// <summary>Glowing in the hand as set up for a combo (see <see cref="Combo.IsSetUp"/>).</summary>
        public bool HasComboGlow { get; private set; }
        private Image comboGlow;

        /// <summary>A soft golden ring just outside the card, pulsing slowly, while what it was made to follow is in place.</summary>
        public void SetComboGlow(bool on) {
            if (HasComboGlow == on) return;
            HasComboGlow = on;
            if (on && comboGlow == null) comboGlow = UITheme.AddTrim(transform, UITheme.WithAlpha(UITheme.Gold, 0.45f), 1f, -5f, "ComboGlow");
            if (comboGlow != null) {
                comboGlow.gameObject.SetActive(on);
                comboGlow.transform.SetAsLastSibling();
            }
        }

        /// <summary>Fog of War: this creature plans something the screen can't show. A "???" band across its art.</summary>
        public void SetPlansHidden(bool hidden) {
            ArePlansHidden = hidden;
            if (hidden && hiddenPlansBadge == null) hiddenPlansBadge = CreateBadge("HiddenPlansBadge", "???", UITheme.Gold, 0.6f, 0.76f);
            if (hiddenPlansBadge != null) hiddenPlansBadge.SetActive(hidden && !IsAnonymous); // A card already all ??? needs no band
        }

        public bool ArePlansHidden { get; private set; }
        private GameObject hiddenPlansBadge;

        // A navy band across the card with brass edges and a word on it
        /// <summary>
        /// A reminder: this creature could act this Day (attack, move, use its ability) and has nothing planned. A comic
        /// thought bubble with "zZz" in it (the user, 2026-10-15) over the top of its art, pulsing softly, while its player plans.
        /// </summary>
        public void SetIdle(bool idle) {
            IsIdle = idle;
            if (idle && idleBadge == null) {
                idleBadge = CreateThoughtBubble("IdleBadge", "z<size=135%>Z</size>z");
                var group = idleBadge.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                DOTween.To(() => group.alpha, a => group.alpha = a, 0.55f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(idleBadge).SetLink(idleBadge);
            }
            if (idleBadge != null) idleBadge.SetActive(idle);
        }

        public bool IsIdle { get; private set; }
        private GameObject idleBadge;

        /// <summary>A queued summon's place in the order its side's creatures arrive (null hides it), like an attack arrow's number.</summary>
        public void SetOrderNumber(int? number) {
            if (number == null) {
                if (orderBadge != null) orderBadge.SetActive(false);
                return;
            }
            if (orderBadge == null) {
                var image = UITheme.NewImage("OrderNumber", transform, UITheme.WithAlpha(UITheme.Hex(0x141420), 0.82f));
                UITheme.Round(image, image.color, 2f);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.6f);
                rect.sizeDelta = new Vector2(30f, 30f);
                var label = UITheme.NewText("Number", image.transform, "", 22f, Color.white);
                label.fontStyle = FontStyles.Bold;
                UITheme.Stretch(label.rectTransform);
                orderBadge = image.gameObject;
            }
            orderBadge.transform.SetAsLastSibling();
            orderBadge.GetComponentInChildren<TextMeshProUGUI>().text = number.Value.ToString();
            orderBadge.SetActive(true);
        }

        /// <summary>The number shown by <see cref="SetOrderNumber"/>, if any.</summary>
        public string OrderNumber => orderBadge != null && orderBadge.activeSelf ? orderBadge.GetComponentInChildren<TextMeshProUGUI>().text : null;
        private GameObject orderBadge;

        // A thought bubble over the top right of the art, with a few words in its oval
        private GameObject CreateThoughtBubble(string objectName, string text) {
            // The area it may take, and inside it the bubble at its own proportions, so the words sit in the drawn oval
            var area = new GameObject(objectName, typeof(RectTransform));
            area.layer = gameObject.layer;
            area.transform.SetParent(transform, false);
            var rect = (RectTransform)area.transform;
            rect.anchorMin = new Vector2(0.4f, 0.58f);
            rect.anchorMax = new Vector2(0.98f, 0.88f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            bubble.layer = gameObject.layer;
            bubble.transform.SetParent(area.transform, false);
            var fitter = bubble.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = UITheme.ThoughtBubble.rect.width / UITheme.ThoughtBubble.rect.height;
            var image = bubble.GetComponent<Image>();
            image.sprite = UITheme.ThoughtBubble;
            image.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.gameObject.layer = gameObject.layer;
            label.transform.SetParent(bubble.transform, false);
            var oval = UITheme.ThoughtBubbleOval;
            var inset = new Vector2(oval.width, oval.height) * 0.16f; // Clear of the outline
            label.rectTransform.anchorMin = oval.min + inset;
            label.rectTransform.anchorMax = oval.max - inset;
            label.margin = Vector4.zero;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.text = text;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.1f, 0.1f, 0.12f);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 6;
            label.fontSizeMax = 40;
            label.raycastTarget = false;
            area.transform.SetAsLastSibling();
            return area;
        }

        private GameObject CreateBadge(string objectName, string text, Color textColor, float anchorMinY, float anchorMaxY) {
            var badge = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            badge.layer = gameObject.layer;
            badge.transform.SetParent(transform, false);
            var badgeRect = (RectTransform)badge.transform;
            badgeRect.anchorMin = new Vector2(0f, anchorMinY);
            badgeRect.anchorMax = new Vector2(1f, anchorMaxY);
            badgeRect.offsetMin = badgeRect.offsetMax = Vector2.zero;
            var badgeImage = badge.GetComponent<Image>();
            badgeImage.color = UITheme.WithAlpha(UITheme.TableDark, 0.92f);
            badgeImage.raycastTarget = false;
            foreach (float edge in new[] { 0f, 1f }) {
                var line = UITheme.NewImage("Edge", badge.transform, UITheme.Gold);
                line.rectTransform.anchorMin = new Vector2(0f, edge);
                line.rectTransform.anchorMax = new Vector2(1f, edge);
                line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            }

            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.gameObject.layer = gameObject.layer;
            label.transform.SetParent(badge.transform, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.text = text;
            label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            label.characterSpacing = 6f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = textColor;
            label.enableAutoSizing = true;
            label.fontSizeMin = 4;
            label.fontSizeMax = 24;
            label.raycastTarget = false;
            return badge;
        }

        /// <summary>
        /// Pops the card into place when its creature is summoned.
        /// </summary>
        public void PlaySummonAnimation(float delay = 0f) {
            Vector3 endScale = transform.localScale;
            transform.localScale = endScale * 0.5f;
            canvasGroup.alpha = 0f;

            summonTween = DOTween.Sequence().SetTarget(transform)
                .AppendInterval(delay)
                .Append(FadeTo(1f, 0.15f))
                .Join(transform.DOScale(endScale * 1.15f, 0.2f).SetEase(Ease.OutQuad))
                .Append(transform.DOScale(endScale, 0.15f).SetEase(Ease.InOutQuad))
                .OnComplete(() => canvasGroup.alpha = RestingAlpha); // Faded again if it's still deploying
        }

        /// <summary>
        /// Shrinks and fades the card out, then destroys it. Used when a creature leaves the battlefield.
        /// </summary>
        public void PlayLeavePlayAnimation() {
            if (IsLeavingPlay) return;
            IsLeavingPlay = true;

            if (replaceBadge != null) replaceBadge.SetActive(false);
            canvasGroup.blocksRaycasts = false;
            Tooltip.Existing?.HideTooltip();
            DOTween.Kill(transform);

            DOTween.Sequence().SetTarget(transform)
                .Append(FadeTo(0f, 0.3f))
                .Join(transform.DOScale(transform.localScale * 0.6f, 0.3f).SetEase(Ease.InBack))
                .OnComplete(() => Destroy(gameObject));
        }

        /// <summary>
        /// Slay the Spire's cast: the card leaps out of the hand to the point, straightening and swelling with a little
        /// overshoot, holds a beat, then snaps shut into a burst of light and is gone. Used when a spell resolves.
        /// It swells to a quarter more than its size, or to <paramref name="shownScale"/> (in the fly root's units) when
        /// given, for a card that starts small, like a spell's ghost on the board.
        /// </summary>
        public void PlayCastAnimation(Transform flyRoot, Vector3 worldTarget, float delay = 0f, float? shownScale = null) {
            SetQueued(false);
            canvasGroup.blocksRaycasts = false;
            Tooltip.Existing?.HideTooltipFor(this);
            DOTween.Kill(transform);

            float unit = CardFx.UnitOf(transform.parent, flyRoot);
            transform.SetParent(flyRoot, true);
            transform.SetAsLastSibling();
            worldTarget.z = transform.position.z;
            Vector3 shown = shownScale.HasValue ? Vector3.one * shownScale.Value : transform.localScale * 1.25f;

            DOTween.Sequence().SetTarget(transform)
                .AppendInterval(delay)
                .Append(transform.DOMove(worldTarget, 0.3f).SetEase(Ease.OutCubic))
                .Join(transform.DORotate(Vector3.zero, 0.3f).SetEase(Ease.OutCubic))
                .Join(transform.DOScale(shown, 0.3f).SetEase(Ease.OutBack))
                .Append(transform.DOPunchScale(shown * 0.08f, 0.22f, 6, 0.5f))
                .AppendCallback(() => CardFx.Burst(flyRoot, transform.position, 0.12f, unit))
                .Append(transform.DOScale(shown * 0.15f, 0.2f).SetEase(Ease.InBack))
                .Join(transform.DORotate(new Vector3(0f, 0f, -30f), 0.2f))
                .Join(FadeTo(0f, 0.2f).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(gameObject));
        }

        private Tween FadeTo(float alpha, float duration) {
            return DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, alpha, duration);
        }
        #endregion
    }
}
