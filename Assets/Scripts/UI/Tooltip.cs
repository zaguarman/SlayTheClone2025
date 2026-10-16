using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>
    /// Manages a single reusable card tooltip for the game: an enlarged copy of the hovered card, its active effects, and the
    /// explanations of its keywords, opened by hovering them and chained from keyword to keyword.
    /// </summary>
    public class Tooltip : MonoBehaviour {
        private static Tooltip shared;

        /// <summary>The scene's one tooltip, created the first time it is shown (and again if it was destroyed).</summary>
        public static Tooltip Shared {
            get {
                if (shared == null) shared = Create(null);
                return shared;
            }
        }

        /// <summary>The tooltip if it exists. Never creates one, so hiding is safe while the scene is torn down.</summary>
        public static Tooltip Existing => shared != null ? shared : null;

        /// <summary>
        /// Creates and initializes a new tooltip instance
        /// </summary>
        public static Tooltip Create(Transform parent) {
            // Create tooltip GameObject
            GameObject tooltipObj = new GameObject("Tooltip");
            tooltipObj.transform.SetParent(parent, false);

            // Add Canvas component for UI rendering
            Canvas tooltipCanvas = tooltipObj.AddComponent<Canvas>();
            tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tooltipCanvas.sortingOrder = 10000; // Make sure it's always on top

            // Add CanvasScaler for consistent UI sizing
            CanvasScaler scaler = tooltipObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Takes the pointer where it shows (the preview, keyword popups), so the board under them isn't hovered
            tooltipObj.AddComponent<GraphicRaycaster>();

            // Add CanvasGroup for fade control
            CanvasGroup canvasGroup = tooltipObj.AddComponent<CanvasGroup>();

            // Create background panel
            GameObject bgPanel = new GameObject("Background");
            bgPanel.transform.SetParent(tooltipObj.transform, false);

            // Add Image component to background
            Image background = bgPanel.AddComponent<Image>();
            UITheme.StylePanel(background);
            background.raycastTarget = false; // It follows the pointer: it must never take the hover from what's under it

            // Configure the RectTransform for proper sizing and positioning
            RectTransform bgRect = background.rectTransform;
            bgRect.anchorMin = new Vector2(0.5f, 0);
            bgRect.anchorMax = new Vector2(0.5f, 0);
            bgRect.pivot = new Vector2(0.5f, 0);
            bgRect.sizeDelta = new Vector2(300, 150);

            // Add text for tooltip content
            GameObject textObj = new GameObject("TooltipText");
            textObj.transform.SetParent(bgPanel.transform, false);

            // Add TextMeshProUGUI component
            TextMeshProUGUI tooltipText = textObj.AddComponent<TextMeshProUGUI>();
            tooltipText.alignment = TextAlignmentOptions.Center;
            tooltipText.fontSize = Roulette.detailsFontSize;
            tooltipText.color = UITheme.Parchment;
            tooltipText.enableWordWrapping = true;
            tooltipText.raycastTarget = false; // Nor the text: under the pointer, it would take the hover from the row it explains, and flicker

            // Configure text RectTransform
            RectTransform textRect = tooltipText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);

            // Add shadow to make text more readable
            Shadow shadow = bgPanel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(2, -2);

            // Add Tooltip component
            Tooltip tooltip = tooltipObj.AddComponent<Tooltip>();

            // Set references
            tooltip.SetupReferences(tooltipText, bgRect, canvasGroup);

            // Hide initially
            tooltipObj.SetActive(false);

            return tooltip;
        }

        // Card hover preview: an enlarged live copy of the hovered card, placed next to it (its size is a RouletteTuning slider)
        private static float PreviewScale => RouletteTuning.Current.previewScale;
        private const float PreviewGap = 10f;      // Screen pixels between a preview and its explanations (the preview's own gap and the details box are sliders)
        private const float TextWidth = 420f; // A text tooltip, like a queued action's details

        // References to tooltip components
        private TextMeshProUGUI tooltipText;
        private RectTransform tooltipRect;
        private CanvasGroup canvasGroup;
        private Vector2 textPivot;

        // Tracking the active card
        private CardController activeCard;
        private CardController preview;
        private bool previewIsCard; // A card in your hand is raised out of the fan instead (Slay the Spire): it's its own preview
        private int browsed; // A creature with cards tucked under it: 0 shows the creature, i its i-th tucked card (the wheel)

        /// <summary>Which card of the hovered creature's stack the preview shows: 0 the creature itself, i its i-th tucked card.</summary>
        public int Browsed => browsed;

        // Browsing the stack turns a wheel of cards: the shown one swings away along an arc around a point below it while
        // the next swings in from the other side. Its measurements are sliders (UI > Roulette Tuning)
        private static RouletteTuning Roulette => RouletteTuning.Current;
        private static float RouletteSeconds => Roulette.seconds;
        private static float RouletteAngle => Roulette.angle;   // Degrees between two cards on the wheel
        private static float RouletteRadius => Roulette.radius; // The wheel's radius, in preview heights
        private readonly List<CardController> leaving = new List<CardController>();

        private void Awake() {
            // Try to get references if not already assigned
            if (tooltipText == null)
                tooltipText = GetComponentInChildren<TextMeshProUGUI>();

            if (tooltipRect == null)
                tooltipRect = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            textPivot = tooltipRect.pivot;
            FogOfWar.Changed += OnFogOfWarChanged;

            // Ensure tooltip starts hidden
            HideTooltip();
        }

        /// <summary>
        /// Sets up references to tooltip components
        /// </summary>
        public void SetupReferences(TextMeshProUGUI text, RectTransform rect, CanvasGroup group) {
            tooltipText = text;
            tooltipRect = rect;
            canvasGroup = group;
            textPivot = rect.pivot;
        }

        // A card is shown enlarged only once the pointer rests on it, never just passing over it on the way to something
        // else. Going on from one enlarged card to the next is quicker, for reading a hand card by card.
        public const float HoverDelay = 0.35f;
        public const float SwitchDelay = 0.1f;
        // A hand card rises at once, but explains itself only after a longer look: a glance along the hand opens nothing
        public const float HandDelay = 0.8f;
        private CardController pendingCard;
        private Coroutine pendingShow;
        private float pendingSince;

        // Why the last hover showed nothing, logged once per card when the pointer rests on it (the user's unresponsive cards)
        private CardController refusedCard;
        private string refusedWhy;

        private void Refused(CardController card, string why) {
            if (card == null || (card == refusedCard && why == refusedWhy)) return; // Said once
            if (why.StartsWith("a card is carried") && GameInput.ButtonHeld) return; // Carrying one: no explanations, as meant
            refusedCard = card;
            refusedWhy = why;
            Debug.LogWarning($"[Hover] {card.GetLinkedCardInstance()?.Name ?? card.name} shows nothing: {why}");
        }

        /// <summary>
        /// Shows the hovered card enlarged next to it, plus its active effects when it has any, once the pointer has rested
        /// on it for a moment (<see cref="HoverDelay"/>). Leaving the card before that shows nothing.
        /// </summary>
        public void ShowTooltip(CardController card) {
            // A wait that should have ended (its coroutine stopped with its card switched off a moment): start again
            if (card != null && card == pendingCard && Time.unscaledTime > pendingSince + HandDelay + 0.5f) CancelPending();
            if (pinned) { Refused(card, "a preview is pinned (a gallery)"); return; }
            if (card == null || card.GetBaseCardData() == null || card == pendingCard) return;
            if (card == activeCard && preview != null) {
                released = false;
                UpdateTooltipContent(card);
                return;
            }
            if (CardController.Dragged != null) { Refused(card, $"a card is carried ({CardController.Dragged.name})"); return; }
            if (!card.gameObject.activeInHierarchy) return; // On screen (a pile window's cards have their controller off)
            CancelPending();
            pendingCard = card;
            pendingSince = Time.unscaledTime;
            float delay = card.GetComponentInParent<HandUI>() != null ? HandDelay
                : preview != null && !PointerOnPreview(GameInput.PointerPosition) ? SwitchDelay : HoverDelay;
            pendingShow = card.StartCoroutine(ShowAfter(card, delay)); // Stops if the card goes
        }

        /// <summary>The mouse wheel turned this frame: what's shown hides, and nothing shows until the wheel is still a moment.</summary>
        public static bool WheelMoved => !Mathf.Approximately(GameInput.WheelNotches, 0f);

        // Waits the delay, starting over whenever the wheel turns, so nothing pops up while scrolling
        private IEnumerator ShowAfter(CardController card, float delay) {
            float ready = Time.unscaledTime + delay;
            while (Time.unscaledTime < ready) {
                if (WheelMoved) ready = Time.unscaledTime + HoverDelay;
                yield return null;
            }
            pendingCard = null;
            pendingShow = null;
            ShowNow(card);
        }

        private void CancelPending() {
            if (pendingCard != null && pendingShow != null) pendingCard.StopCoroutine(pendingShow);
            pendingCard = null;
            pendingShow = null;
        }

        private void ShowNow(CardController card) {
            if (card == null || card.GetBaseCardData() == null) return;
            if (card.IsFaceDown) { // Fog of War: a card in the enemy's hand, or the ghost of a spell they're casting
                ShowTooltip(card.IsQueued ? "<b>???</b>\nA spell the enemy is casting here." : "<b>???</b>\nA card in the enemy's hand.");
                return;
            }
            if (card.IsAnonymous) { // Fog of War: nothing about what it is
                ShowTooltip(card.IsQueued ? "<b>???</b>\nSomething is being summoned here." : "<b>???</b>\nAn enemy creature. You don't know what it is.");
                return;
            }
            if (CardController.Dragged != null) { Refused(card, $"a card is carried ({CardController.Dragged.name})"); return; } // The player is doing something: no explanations in the way
            if (card == activeCard && preview != null) {
                released = false;
                UpdateTooltipContent(card);
                return;
            }
            // Reading keywords: passing over another card on the way to an explanation doesn't replace them, nor does a
            // card under the enlarged one or its explanations
            if ((keywordPopups.Count > 0 && PointerInArea(GameInput.PointerPosition)) || PointerReading(GameInput.PointerPosition)) {
                Refused(card, $"the pointer is on what's shown already ({activeCard?.GetLinkedCardInstance()?.Name}, {keywordPopups.Count} explanations)");
                return;
            }
            // A hand card explains itself only once its hand raises it (the hand asks again when it does)
            var hand = card.GetComponentInParent<HandUI>();
            if (hand != null && hand.Raised != card) { Refused(card, $"its hand raised another card ({hand.Raised?.GetLinkedCardInstance()?.Name})"); return; }

            HideTooltip();
            activeCard = card;
            browsed = 0;
            canvasGroup.alpha = 1;
            gameObject.SetActive(true);

            if (hand != null || InWindow(card)) {
                preview = card; // The hand raises it, enlarged (a window's cards are big already); its explanations open beside it
                previewIsCard = true;
            }
            else {
                preview = card.CreatePreview(transform);
                PassThrough(preview);
                if (preview != null) {
                    preview.transform.localScale = Vector3.one * PreviewScaleFor(card);
                    PlacePreview(card, (RectTransform)preview.transform);
                    var shown = (RectTransform)preview.transform;
                    ShowWheel(shown.position, shown.up, fadeIn: false);
                }
            }
            UpdateTooltipContent(card);
        }

        /// <summary>
        /// Shows a text tooltip with the specified content
        /// </summary>
        public void ShowTooltip(string content) {
            if (string.IsNullOrEmpty(content)) return;

            HideTooltip();
            tooltipRect.gameObject.SetActive(true);
            tooltipRect.pivot = textPivot;
            tooltipText.text = content;
            tooltipText.alignment = TextAlignmentOptions.TopLeft;
            tooltipText.fontSize = 21;
            float textHeight = tooltipText.GetPreferredValues(content, TextWidth - 20f, 0f).y;
            tooltipRect.sizeDelta = new Vector2(TextWidth, textHeight + 20f);
            FollowMouse();

            canvasGroup.alpha = 1;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Refreshes the details shown next to the preview (the preview itself updates live)
        /// </summary>
        public void UpdateTooltipContent(CardController card) {
            if (card == null || card != activeCard) return;

            string details = card.GetActiveModifiersText();
            int tucked = card.AttachedCards.Count;
            if (tucked > 0 && !previewIsCard) {
                // A tucked card: only what it does to this creature, then where it is in the stack
                string what = card.IsAttachedHidden(browsed - 1) ? "<b>???</b>\nA spell the enemy is casting here: what it is shows when it lands."
                    : browsed > 0 ? card.GetEffectsFromText(card.AttachedCards[browsed - 1]) : "";
                details = browsed > 0
                    ? $"{what}\n<size=80%><color=#C9C0AE>{browsed} of {tucked} on {card.GetBaseCardData()?.cardName}: scroll for the others</color></size>"
                    : (string.IsNullOrEmpty(details) ? "" : details + "\n\n") + $"<size=85%>{tucked} card{(tucked == 1 ? "" : "s")} on it: scroll to see {(tucked == 1 ? "it" : "them")}</size>";
            }
            bool hasDetails = !string.IsNullOrEmpty(details) && preview != null;
            tooltipRect.gameObject.SetActive(hasDetails);
            if (!hasDetails) return;

            tooltipText.text = details;
            tooltipText.alignment = TextAlignmentOptions.Center;
            tooltipText.fontSize = 16;
            float textHeight = tooltipText.GetPreferredValues(details, Roulette.detailsWidth - 20f, 0f).y;
            tooltipRect.sizeDelta = new Vector2(Roulette.detailsWidth, textHeight + 20f);
            PlaceDetailsBesidePreview();
        }

        /// <summary>What a text tooltip says, while one shows.</summary>
        public string ShownText => gameObject.activeSelf && activeCard == null ? tooltipText.text : null;

        /// <summary>What the details beside a card's preview say, while they show.</summary>
        public string DetailsText => activeCard != null && tooltipRect.gameObject.activeSelf ? tooltipText.text : null;

        /// <summary>Hides a text tooltip; a card's preview stays.</summary>
        public void HideText() {
            if (activeCard == null) HideTooltip();
        }

        /// <summary>Hides the tooltip if it is showing this card (a destroyed preview must not hide the next card's).</summary>
        public void HideTooltipFor(CardController card) {
            if (card != null && card == pendingCard) CancelPending();
            if (card != null && card == activeCard) HideTooltip();
        }

        /// <summary>
        /// Hides the tooltip
        /// </summary>
        public void HideTooltip() {
            pinned = false;
            CancelPending();
            HideShown();
        }

        // Hides what's shown, but not a card about to show (the next one hovered)
        private void HideShown() {
            CloseKeywordsFrom(0);
            released = false;
            activeCard = null;
            if (preview != null && !previewIsCard) Destroy(preview.gameObject);
            preview = null;
            previewIsCard = false;
            ClearLeaving();
            ClearWheel();
            canvasGroup.alpha = 0;
            gameObject.SetActive(false);
        }

        // Above the card when it is in the lower half of the screen, below it otherwise; always fully on screen
        private void PlacePreview(CardController card, RectTransform previewRect) {
            var corners = new Vector3[4];
            ((RectTransform)card.transform).GetWorldCorners(corners);
            var eventCamera = CanvasUtility.GetEventCamera(card);
            Vector2 cardMin = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            Vector2 cardMax = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);

            bool showAbove = (cardMin.y + cardMax.y) * 0.5f < Screen.height * 0.5f;
            float height = ScreenRect(previewRect).height / ShownScaleOf(card); // The sliders' unit: the preview's height before Shown Scale
            float gap = Roulette.previewGap * height;
            previewRect.pivot = new Vector2(0.5f, showAbove ? 0f : 1f);
            previewRect.position = new Vector2((cardMin.x + cardMax.x) * 0.5f, showAbove ? cardMax.y + gap : cardMin.y - gap);
            // The sliders' nudges: sideways, and away from the card; with a stack, the shown card's own (its wheel stays put)
            previewRect.position += new Vector3(Roulette.previewSideways * height, (showAbove ? 1f : -1f) * Roulette.previewAway * height, 0f);
            if (card.AttachedCards.Count > 0) previewRect.position += Roulette.ShownOffset(previewRect.up, height);
            KeepOnScreen(previewRect);
        }

        // To the right of the preview, or to its left when there is no room
        private void PlaceDetailsBesidePreview() {
            var shown = PreviewScreenRect();
            float gap = previewIsCard ? PreviewGap : Roulette.detailsGap * shown.height, top = shown.yMax - (previewIsCard ? 0f : Roulette.detailsDown * shown.height);
            tooltipRect.pivot = new Vector2(0f, 1f);
            tooltipRect.position = new Vector2(shown.xMax + gap, top);

            var corners = new Vector3[4];
            tooltipRect.GetWorldCorners(corners);
            if (corners[2].x > Screen.width) {
                tooltipRect.pivot = new Vector2(1f, 1f);
                tooltipRect.position = new Vector2(shown.xMin - gap, top);
            }
            KeepOnScreen(tooltipRect);
        }

        // Where the enlarged card is on screen: the copy on this overlay, or the raised hand card on the table's canvas
        private Rect PreviewScreenRect() =>
            previewIsCard ? CardScreenRect(preview) ?? default : ScreenRect((RectTransform)preview.transform);

        private Camera PreviewCamera => previewIsCard && preview != null ? CanvasUtility.GetEventCamera(preview) : null;

        private static void KeepOnScreen(RectTransform rect) {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var shift = Vector3.zero;
            if (corners[0].x < 0) shift.x = -corners[0].x;
            else if (corners[2].x > Screen.width) shift.x = Screen.width - corners[2].x;
            if (corners[0].y < 0) shift.y = -corners[0].y;
            else if (corners[2].y > Screen.height) shift.y = Screen.height - corners[2].y;
            rect.position += shift;
        }

        // Above the pointer, or below it when there's no room above (near the top of the screen): never over it
        private void FollowMouse() {
            Vector2 mousePos = GameInput.PointerPosition;
            float height = tooltipRect.rect.height * tooltipRect.lossyScale.y;
            bool below = mousePos.y + 40f + height > Screen.height;
            tooltipRect.pivot = new Vector2(textPivot.x, below ? 1f : 0f);
            tooltipRect.position = new Vector2(mousePos.x, below ? mousePos.y - 30f : mousePos.y + 40f);
            KeepOnScreen(tooltipRect);
        }

        /// <summary>
        /// Text tooltips follow the mouse; card previews stay next to their card
        /// </summary>
        private void LateUpdate() {
            if (gameObject.activeSelf && activeCard == null) {
                FollowMouse();
            }
            if (stackStale) {
                stackStale = false;
                RefreshStack();
            }
        }

        #region Keyword popups
        // Slay the Spire style: hovering a keyword on the enlarged card opens its explanation beside it; keywords in that
        // explanation open theirs, stacked below it, and so on (5 deep). They never cover the preview or each other, so the
        // pointer can always reach the next one. The tooltip stays while the pointer is over the card, its preview or a
        // popup, and closes a moment after it's over none, or at a click anywhere else. Dragging a card closes it.
        private const float ReleaseGrace = 0.4f; // Seconds to cross from the card to its preview, or between popups
        private const float HandReleaseGrace = 0.15f; // A hand card's popups sit right beside it: little to cross
        private const int MaxKeywordPopups = 5;
        private const float PopupGap = 6f;
        private const float PopupWidth = 340f;
        private const float PopupPadding = 14f;

        private class KeywordPopup {
            public string Key;
            public RectTransform Rect;
            public TextMeshProUGUI Body;
        }

        private readonly List<KeywordPopup> keywordPopups = new List<KeywordPopup>();
        private bool released;    // The pointer left the card
        private float lastHeldAt; // When the pointer was last on the card, its preview or a popup

        /// <summary>The keywords whose explanations are open, from the card outwards.</summary>
        public List<string> OpenKeywords => keywordPopups.Select(p => p.Key).ToList();

        /// <summary>The enlarged card shown, if any: a copy, or the hand card itself, raised out of the fan.</summary>
        public CardController Preview => preview;

        /// <summary>The card whose explanation is shown, if any.</summary>
        public CardController ActiveCard => activeCard;

        /// <summary>The explanation open for this keyword, if any: its text, whose keywords can be hovered in turn.</summary>
        public TMP_Text KeywordText(string key) => keywordPopups.FirstOrDefault(p => p.Key == key)?.Body;

        /// <summary>The pointer left the card: the tooltip stays while the pointer goes on to the preview or its popups.</summary>
        public void ReleaseCard(CardController card) {
            if (card != null && card == pendingCard) CancelPending(); // It was only passed over
            if (card == null || card != activeCard) return;
            Release();
        }

        /// <summary>The pointer left something on the board: whatever is shown stays only while the pointer is around it.</summary>
        public void Release() {
            if (activeCard == null || released) return;
            released = true;
            lastHeldAt = Time.unscaledTime;
        }

        private void Update() {
            if (pinned) return; // The gallery turns it (the mouse wheel included), nothing hovered or scrolled here closes it
            // Over a creature with cards tucked under it, the wheel goes through them, in the preview
            if (WheelMoved && activeCard != null && preview != null && !previewIsCard && activeCard.AttachedCards.Count > 0
                && (PointerOnTooltip(GameInput.PointerPosition, includeCard: true))) {
                Browse(GameInput.WheelNotches < 0 ? 1 : -1);
                return;
            }
            if (WheelMoved) { // Scrolling: hide for a moment; the card still under the pointer shows again once the wheel is still
                // Nothing to scroll there (the user): a card on the board or in the hand, with no stack to turn, stays as it is
                if (activeCard != null && !InWindow(activeCard) && activeCard.GetComponentInParent<ScrollRect>() == null) return;
                var card = activeCard;
                HideTooltip();
                if (card != null && card.gameObject.activeInHierarchy && CardScreenRect(card) is Rect rect && rect.Contains(GameInput.PointerPosition)) ShowTooltip(card);
                return;
            }
            if (activeCard == null || preview == null) return;
            if (CardController.Dragged != null) { HideTooltip(); return; }
            // A hand card back down in the fan takes its explanations with it: none are left floating over the hand
            if (previewIsCard && !InWindow(activeCard) && !StillRaised(activeCard)) { HideShown(); return; }
            Vector2 pointer = GameInput.PointerPosition;
            if (GameInput.ClickedThisFrame && ClickAt(pointer)) return;
            TrackKeywords(pointer);
            // A risen hand card with its explanations open holds only while the pointer is around it or them, measured every
            // frame (a missed pointer exit can't keep it up over the hand), and lets go quickly so the next card can rise
            bool handReading = previewIsCard && keywordPopups.Count > 0;
            bool held = handReading ? PointerInArea(pointer) : !released || PointerInArea(pointer);
            if (held) lastHeldAt = Time.unscaledTime;
            else if (Time.unscaledTime - lastHeldAt > (handReading ? HandReleaseGrace : ReleaseGrace)) HideTooltip();
        }

        // A card of the deck window (a pile, a pick): shown big already, it's its own preview
        private static bool InWindow(CardController card) => card != null && card.GetComponent<DeckViewCardInterceptor>() != null;

        private static bool StillRaised(CardController card) {
            if (!card.isActiveAndEnabled) return false;
            var hand = card.GetComponentInParent<HandUI>();
            return hand != null && hand.Raised == card;
        }

        /// <summary>
        /// Shows the next (or, step -1, the previous) card of the hovered creature's stack enlarged: the creature, then each
        /// card tucked under it, round again.
        /// </summary>
        public void Browse(int step) {
            if (activeCard == null || preview == null || previewIsCard) return;
            var stack = activeCard.AttachedCards;
            int count = stack.Count + 1;
            browsed = ((browsed + step) % count + count) % count;
            CloseKeywordsFrom(0);
            var old = preview;
            preview = browsed == 0
                ? activeCard.CreatePreview(transform)
                : activeCard.CreatePreviewOfAttached(browsed - 1, transform);
            PassThrough(preview);
            if (preview == null) {
                Destroy(old.gameObject);
                return;
            }
            preview.transform.localScale = Vector3.one * PreviewScaleFor(activeCard);
            PlacePreview(activeCard, (RectTransform)preview.transform);
            UpdateTooltipContent(activeCard);
            var home = (RectTransform)preview.transform;
            Vector3 position = home.position, up = home.up; // Where it settles, before the spin turns it in
            Spin(old, preview, step < 0 ? -1 : 1);
            ShowWheel(position, up, fadeIn: true);
        }

        // The rest of the stack, faint, on the wheel around the shown card: the cards to come wait on the left (the next one
        // clearest), the ones gone by sit on the right, fainter the further back they are, until they're gone
        private readonly List<CardController> wheel = new List<CardController>();
        private static int WheelReach => Roulette.reach; // Cards shown each side

        private void ShowWheel(Vector3 home, Vector3 up, bool fadeIn) {
            ClearWheel();
            if (activeCard == null || preview == null || previewIsCard || activeCard.AttachedCards.Count == 0) return;
            var stack = activeCard.AttachedCards;
            var shown = (RectTransform)preview.transform;
            float height = shown.rect.height * shown.lossyScale.y / ShownScaleOf(activeCard); // The wheel's unit: before Shown Scale
            float radius = height * RouletteRadius;
            // From the shown card's bottom edge, whichever way it hangs from the card in play: above yours, below an enemy's
            // (the user: the same roulette both sides)
            home -= up * (shown.pivot.y * shown.rect.height * shown.lossyScale.y);
            Vector3 wheelHome = home - Roulette.ShownOffset(up, height); // The shown card's place on the wheel, before its own nudge
            Vector3 center = wheelHome - up * radius;
            // Never-ending (the user): once the stack fills the whole wheel, both sides, with no card showing twice (the
            // creature and its cards: 5 or more at a reach of 2), the wheel shows it going round, the first after the last
            int count = stack.Count + 1;
            bool endless = count >= 2 * WheelReach + 1;
            foreach (var (distance, ahead) in Enumerable.Range(1, Mathf.Max(0, WheelReach)).SelectMany(d => new[] { (d, true), (d, false) })) {
                int i = browsed + (ahead ? distance : -distance);
                if (endless) i = (i % count + count) % count;
                else if (i < 0 || i >= count) continue;
                float alpha = Roulette.AlphaAt(distance, ahead);
                var ghost = i == 0
                    ? activeCard.CreatePreview(transform)
                    : activeCard.CreatePreviewOfAttached(i - 1, transform);
                if (ghost == null) continue;
                var rect = (RectTransform)ghost.transform;
                rect.localScale = shown.localScale * Roulette.WaitingOfShown;
                float angle = (ahead ? 1f : -1f) * distance * RouletteAngle; // The next ones come from the left
                rect.position = center + Quaternion.Euler(0f, 0f, angle) * (wheelHome - center) + Nudge(up, height, ahead, distance);
                rect.rotation = Quaternion.Euler(0f, 0f, angle * Roulette.waitingTilt) * shown.parent.rotation;
                rect.SetAsFirstSibling(); // Behind the shown card and the explanations
                var group = ghost.GetComponent<CanvasGroup>() ?? ghost.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                group.alpha = fadeIn ? 0f : alpha;
                if (fadeIn) DOTween.To(() => group.alpha, a => group.alpha = a, alpha, RouletteSeconds).SetUpdate(true).SetTarget(group);
                wheel.Add(ghost);
            }
        }

        private void ClearWheel() {
            foreach (var ghost in wheel) {
                if (ghost == null) continue;
                DOTween.Kill(ghost.GetComponent<CanvasGroup>());
                Destroy(ghost.gameObject);
            }
            wheel.Clear();
        }

        // Where a waiting card sits beyond the wheel's arc, one place out (the sliders): out to its side, and up
        // places: how far out on the wheel (during a turn, a share of the first place). Out and Up grow with it; Lift is
        // the same for every waiting card
        private static Vector3 Nudge(Vector3 up, float height, bool ahead, float places) {
            var right = new Vector3(up.y, -up.x, 0f);
            return ((ahead ? -right : right) * Roulette.waitingOut + up * Roulette.waitingUp) * height * places
                   + up * Roulette.waitingLift * height * Mathf.Min(places, 1f);
        }

        // The roulette: both cards turn together around the wheel's center, the old one out, the new one in, settling where
        // the new one belongs; each grows or shrinks between the shown card's size and a waiting card's, and leans as they do.
        // A card still leaving from the last turn goes at once
        private void Spin(CardController old, CardController next, int direction) {
            ClearLeaving();
            var nextRect = (RectTransform)next.transform;
            Vector3 up = nextRect.up;
            float height = nextRect.rect.height * nextRect.lossyScale.y / ShownScaleOf(activeCard);
            float radius = height * RouletteRadius;
            Vector3 shownOffset = Roulette.ShownOffset(up, height);
            Vector3 home = nextRect.position - shownOffset; // On the wheel, before the shown card's own nudge
            // The wheel's center, under the card's bottom edge whichever way it hangs (as in ShowWheel)
            Vector3 center = home - up * (radius + nextRect.pivot.y * nextRect.rect.height * nextRect.lossyScale.y);
            Vector3 nextScale = nextRect.localScale;
            var oldRect = (RectTransform)old.transform;
            Vector3 oldHome = oldRect.position - shownOffset, oldScale = oldRect.localScale;
            if (old.TryGetComponent(out CanvasGroup oldGroup)) oldGroup.blocksRaycasts = false;
            leaving.Add(old);

            // waiting: 0 where the shown card is, 1 a place out on the wheel, on the ahead side or the other
            void Place(RectTransform rect, Vector3 from, Vector3 scale, float angle, float waiting, bool ahead) {
                rect.position = center + Quaternion.Euler(0f, 0f, angle) * (from - center) + Nudge(up, height, ahead, waiting) + shownOffset * (1f - waiting);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle * Roulette.waitingTilt);
                rect.localScale = scale * Mathf.Lerp(1f, Roulette.WaitingOfShown, waiting);
            }
            float tilt = -direction * RouletteAngle; // Scrolling on turns the wheel clockwise: the next card comes from the left
            bool fromAhead = direction > 0;
            Place(nextRect, home, nextScale, -tilt, 1f, fromAhead);
            DOTween.To(() => 0f, t => {
                if (nextRect != null) Place(nextRect, home, nextScale, -tilt * (1f - t), 1f - t, fromAhead);
                if (oldRect != null) Place(oldRect, oldHome, oldScale, tilt * t, t, !fromAhead);
                if (oldGroup != null) oldGroup.alpha = 1f - t;
            }, 1f, RouletteSeconds).SetEase(Roulette.ease).SetUpdate(true).SetTarget(this).OnComplete(ClearLeaving); // Real time: also while paused
        }

        /// <summary>A <see cref="RouletteTuning"/> slider moved: the preview showing takes its size again, and its wheel is laid out anew.</summary>
        public void RedrawRoulette() {
            if (activeCard == null || preview == null || previewIsCard) return;
            ClearLeaving();
            var rect = (RectTransform)preview.transform;
            rect.localScale = Vector3.one * PreviewScaleFor(activeCard);
            PlacePreview(activeCard, rect);
            UpdateTooltipContent(activeCard);
            ShowWheel(rect.position, rect.up, fadeIn: false);
        }

        /// <summary>
        /// The cards under the hovered creature changed (Fog of War switched, a hidden one shown): the card on show and the
        /// wheel again, as they are now.
        /// </summary>
        public void RefreshStack() {
            if (activeCard == null || preview == null || previewIsCard) return;
            ClearLeaving();
            CloseKeywordsFrom(0);
            if (browsed > activeCard.AttachedCards.Count) browsed = 0;
            var fresh = browsed == 0 ? activeCard.CreatePreview(transform) : activeCard.CreatePreviewOfAttached(browsed - 1, transform);
            PassThrough(fresh);
            if (fresh == null) return;
            Destroy(preview.gameObject);
            preview = fresh;
            RedrawRoulette();
        }

        // Switching Fog of War turns hidden cards face up or down: the stack shows again once the board has caught up
        private bool stackStale;
        private void OnFogOfWarChanged() => stackStale = true;

        private void OnDestroy() => FogOfWar.Changed -= OnFogOfWarChanged;

        /// <summary>Shows this card's preview now, as if hovered, and keeps it up until <see cref="HideTooltip"/> (the Roulette Gallery).</summary>
        public void Pin(CardController card) {
            HideTooltip();
            ShowNow(card);
            pinned = activeCard == card;
        }

        // Pinned (the Roulette Gallery): nothing else hovered replaces it, moving away or scrolling doesn't close it; the
        // gallery turns its stack, the mouse wheel included
        private bool pinned;

        /// <summary>A preview shown by <see cref="Pin"/>, up until hidden.</summary>
        public bool IsPinned => pinned;

        private void ClearLeaving() {
            DOTween.Kill(this, complete: false);
            foreach (var card in leaving) if (card != null) Destroy(card.gameObject);
            leaving.Clear();
            if (preview != null && !previewIsCard && preview.transform.localRotation != Quaternion.identity) {
                preview.transform.localRotation = Quaternion.identity;
                PlacePreview(activeCard, (RectTransform)preview.transform);
            }
        }

        /// <summary>A click away from the card, its preview and the explanations closes them all. Returns whether it did.</summary>
        public bool ClickAt(Vector2 pointer) {
            if (keywordPopups.Count == 0 || PointerOnTooltip(pointer, includeCard: true)) return false;
            HideTooltip();
            return true;
        }

        /// <summary>Whether the pointer is right on the preview or an open explanation (or the card itself), no margin.</summary>
        public bool PointerOnTooltip(Vector2 pointer, bool includeCard = false) {
            if (preview == null) return false;
            if (keywordPopups.Any(p => ScreenRect(p.Rect).Contains(pointer)) || PreviewScreenRect().Contains(pointer)) return true;
            return includeCard && CardScreenRect(activeCard) is Rect card && card.Contains(pointer);
        }

        // A small margin: slipping a few pixels off a popup, or crossing the gap between two, keeps everything open, while
        // moving back to the table closes it
        private const float AreaMargin = 20f;

        /// <summary>
        /// Whether the pointer is anywhere around what the tooltip shows: the card, its preview and the open explanations,
        /// with a margin. Only leaving all of it closes the tooltip.
        /// </summary>
        public bool PointerInArea(Vector2 pointer) {
            if (activeCard == null || preview == null) return false;
            var areas = keywordPopups.Select(p => ScreenRect(p.Rect)).Append(PreviewScreenRect()).ToList();
            var card = CardScreenRect(activeCard);
            if (card.HasValue) areas.Add(card.Value);
            return areas.Any(r => Rect.MinMaxRect(r.xMin - AreaMargin, r.yMin - AreaMargin, r.xMax + AreaMargin, r.yMax + AreaMargin).Contains(pointer));
        }

        // With cards tucked under it, the shown card's own size (a slider), the wheel's cards keeping theirs
        private static float ShownScaleOf(CardController card) => card != null && card.AttachedCards.Count > 0 ? Roulette.shownScale : 1f;

        // Twice a card's own size, and always well bigger than the card as it's shown (a pile window's cards are big already)
        private float PreviewScaleFor(CardController card) {
            var canvas = GetComponentInParent<Canvas>();
            float unit = (canvas != null ? canvas.scaleFactor : 1f) * CardColumns.CardHeight; // A preview at scale 1, on screen
            float scale = CardScreenRect(card) is Rect shown && unit > 0f ? Mathf.Max(PreviewScale, 1.3f * shown.height / unit) : PreviewScale;
            return scale * ShownScaleOf(card);
        }

        private static Rect? CardScreenRect(CardController card) {
            if (card == null) return null;
            var corners = new Vector3[4];
            ((RectTransform)card.transform).GetWorldCorners(corners);
            var camera = CanvasUtility.GetEventCamera(card);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// Follows the pointer: on a keyword of the preview or of a popup it opens that keyword's explanation next to it.
        /// Only another keyword changes what's open: off a keyword, or between popups, everything stays, so the pointer can
        /// wander on to the next explanation. Returns whether the pointer is on the preview or a popup.
        /// </summary>
        public bool TrackKeywords(Vector2 pointer) {
            if (preview == null) return false;
            int over = -2; // -1: the preview; 0 and up: a popup
            for (int i = keywordPopups.Count - 1; i >= 0 && over == -2; i--) {
                if (RectTransformUtility.RectangleContainsScreenPoint(keywordPopups[i].Rect, pointer, null)) over = i;
            }
            var previewRect = (RectTransform)preview.transform;
            if (over == -2 && TrackIcons(pointer, previewRect)) return true;
            if (over == -2 && RectTransformUtility.RectangleContainsScreenPoint(previewRect, pointer, PreviewCamera)) over = -1;
            if (over == -2) return false;

            TMP_Text text = over == -1 ? preview.RulesText : keywordPopups[over].Body;
            RectTransform parent = over == -1 ? previewRect : keywordPopups[over].Rect;
            int link = text != null ? TMP_TextUtilities.FindIntersectingLink(text, pointer, over == -1 ? PreviewCamera : null) : -1;
            if (link < 0) return true;

            string key = text.textInfo.linkInfo[link].GetLinkID();
            int next = over + 1;
            bool alreadyOpen = next < keywordPopups.Count && keywordPopups[next].Key == key;
            if (!alreadyOpen && Keywords.Exists(key) && next < MaxKeywordPopups) OpenKeyword(next, key, parent, text, link);
            return true;
        }

        // The icons above a creature's art (its statuses, its raised or lowered stats), on the board card or the enlarged
        // one: hovering one opens a popup saying what it is, like a keyword's
        private bool TrackIcons(Vector2 pointer, RectTransform previewRect) {
            foreach (var (card, camera) in new[] { (preview, PreviewCamera), (activeCard, CanvasUtility.GetEventCamera(activeCard)) }) {
                var icons = card != null ? card.StatusText : null;
                if (icons == null || string.IsNullOrEmpty(icons.text) || !icons.isActiveAndEnabled) continue;
                int link = TMP_TextUtilities.FindIntersectingLink(icons, pointer, camera);
                if (link < 0) continue;
                string key = icons.textInfo.linkInfo[link].GetLinkID();
                if (keywordPopups.Count > 0 && keywordPopups[0].Key == key) return true;
                var about = activeCard.DescribeIcon(key);
                if (about == null) return true;
                OpenKeyword(0, key, previewRect, icons, link, about.Value.Title, about.Value.Body);
                return true;
            }
            return false;
        }

        private void OpenKeyword(int index, string key, RectTransform parent, TMP_Text parentText, int linkIndex, string title = null, string body = null) {
            CloseKeywordsFrom(index);
            var popup = BuildKeywordPopup(key, title, body);

            // At the first free spot: below the text it was opened from, above it, or beside everything already shown,
            // level with the keyword. It never covers the preview or another popup, so the pointer can always reach it
            var info = parentText.textInfo.linkInfo[linkIndex];
            var character = parentText.textInfo.characterInfo[info.linkTextfirstCharacterIndex];
            float linkTop = RectTransformUtility.WorldToScreenPoint(CanvasUtility.GetEventCamera(parentText),
                parentText.transform.TransformPoint(character.topLeft)).y + 8f;
            var size = new Vector2(popup.Rect.rect.width * popup.Rect.lossyScale.x, popup.Rect.rect.height * popup.Rect.lossyScale.y);
            var shown = keywordPopups.Select(p => ScreenRect(p.Rect)).Append(PreviewScreenRect()).ToList();
            if (tooltipRect.gameObject.activeSelf) shown.Add(ScreenRect(tooltipRect)); // The active effects beside the preview
            var from = index == 0 ? PreviewScreenRect() : ScreenRect(parent);
            float level = Mathf.Clamp(linkTop, size.y, Screen.height);
            float left = shown.Min(r => r.xMin), right = shown.Max(r => r.xMax);
            var candidates = new List<Vector2>(); // Top-left corners
            if (index > 0) {
                candidates.Add(new Vector2(from.xMin, from.yMin - PopupGap));
                candidates.Add(new Vector2(from.xMin, from.yMax + PopupGap + size.y));
            }
            candidates.Add(new Vector2(from.xMax + PreviewGap, level));
            candidates.Add(new Vector2(from.xMin - PreviewGap - size.x, level));
            candidates.Add(new Vector2(right + PreviewGap, level));
            candidates.Add(new Vector2(left - PreviewGap - size.x, level));
            Vector2 topLeft = candidates.FirstOrDefault(c => Fits(new Rect(c.x, c.y - size.y, size.x, size.y), shown));
            if (topLeft == default) topLeft = candidates[0];

            popup.Rect.pivot = new Vector2(0f, 1f);
            popup.Rect.position = topLeft;
            KeepOnScreen(popup.Rect);
            keywordPopups.Add(popup);
        }

        // A parchment note like the card's text box: the keyword's name, then its explanation with its own keywords as links
        private KeywordPopup BuildKeywordPopup(string key, string titleText = null, string bodyText = null) {
            var entry = Keywords.Get(key) ?? new Keywords.Entry { Key = key, Title = titleText ?? key };
            if (titleText != null) entry = new Keywords.Entry { Key = key, Title = titleText };
            var background = UITheme.NewImage($"Keyword: {key}", transform, UITheme.Parchment, raycastTarget: true);
            UITheme.Round(background, UITheme.Parchment, 1.5f);
            UITheme.AddTrim(background.transform, UITheme.Gold, 1.5f);
            UITheme.AddHardShadow(background, 5f);
            var rect = background.rectTransform;

            float inner = PopupWidth - 2 * PopupPadding;
            var title = UITheme.NewText("Title", rect, entry.Title, 24f, UITheme.Ink, TextAlignmentOptions.TopLeft);
            title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            var body = UITheme.NewText("Body", rect, bodyText ?? Keywords.Describe(key), 19f, UITheme.Ink, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            float titleHeight = title.GetPreferredValues(entry.Title, inner, 0f).y;
            float bodyHeight = body.GetPreferredValues(body.text, inner, 0f).y;
            rect.sizeDelta = new Vector2(PopupWidth, PopupPadding * 2 + titleHeight + 6f + bodyHeight);
            PlaceInPopup(title.rectTransform, PopupPadding, titleHeight);
            PlaceInPopup(body.rectTransform, PopupPadding + titleHeight + 6f, bodyHeight);
            body.ForceMeshUpdate(); // Its links can be found right away
            return new KeywordPopup { Key = key, Rect = rect, Body = body };
        }

        // Overlay canvas: world space is screen pixels
        // The enlarged card only shows: the pointer goes through it to the card it covers (the user: the other side's middle
        // slot, under a middle card's preview, took nothing while it showed)
        private static void PassThrough(CardController shown) {
            if (shown != null && shown.TryGetComponent(out CanvasGroup group)) group.blocksRaycasts = false;
        }

        // Reading what's shown: an explanation, the shown card's text box (its keywords) or its details. Elsewhere on the shown
        // card, the card under it takes over
        private bool PointerReading(Vector2 pointer) {
            if (preview == null) return false;
            if (keywordPopups.Any(p => ScreenRect(p.Rect).Contains(pointer))) return true;
            // On one of its keywords (its explanation opens there): the shown card's text box alone isn't reading, it may
            // be over the card the pointer went for (a middle card's preview covers the other side's middle card)
            var text = preview.RulesText;
            return text != null && TMP_TextUtilities.FindIntersectingLink(text, pointer, PreviewCamera) >= 0;
        }

        // On what's shown now (its enlarged card): the card under it only takes over once the pointer rests there
        private bool PointerOnPreview(Vector2 pointer) => preview != null && !previewIsCard && PreviewScreenRect().Contains(pointer);

        private static Rect ScreenRect(RectTransform rect) {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static bool Fits(Rect rect, List<Rect> shown) =>
            rect.xMin >= 0f && rect.yMin >= 0f && rect.xMax <= Screen.width && rect.yMax <= Screen.height && !shown.Any(r => r.Overlaps(rect));

        private static void PlaceInPopup(RectTransform child, float top, float height) {
            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(1f, 1f);
            child.pivot = new Vector2(0.5f, 1f);
            child.sizeDelta = new Vector2(-2 * PopupPadding, height);
            child.anchoredPosition = new Vector2(0f, -top);
        }

        private void CloseKeywordsFrom(int index) {
            for (int i = keywordPopups.Count - 1; i >= index && i >= 0; i--) {
                if (keywordPopups[i].Rect != null) Destroy(keywordPopups[i].Rect.gameObject);
                keywordPopups.RemoveAt(i);
            }
        }
        #endregion

        /// <summary>
        /// Enables tooltip functionality for a card while disabling dragging
        /// </summary>
        public void EnableTooltipOnly(CardController card) {
            if (card == null) return;
            var canvasGroup = card.GetComponent<CanvasGroup>();
            if (canvasGroup != null) {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
        }
    }
}
