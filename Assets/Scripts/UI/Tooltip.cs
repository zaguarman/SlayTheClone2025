using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>
    /// Manages a single reusable card tooltip for the game.
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

            // Add CanvasGroup for fade control
            CanvasGroup canvasGroup = tooltipObj.AddComponent<CanvasGroup>();

            // Create background panel
            GameObject bgPanel = new GameObject("Background");
            bgPanel.transform.SetParent(tooltipObj.transform, false);

            // Add Image component to background
            Image background = bgPanel.AddComponent<Image>();
            UITheme.StylePanel(background);

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
            tooltipText.fontSize = 16;
            tooltipText.color = UITheme.Parchment;
            tooltipText.enableWordWrapping = true;

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

        // Card hover preview: an enlarged live copy of the hovered card, placed next to it
        private const float PreviewScale = 2f;
        private const float PreviewGap = 10f;      // Screen pixels between the card and its preview
        private const float DetailsWidth = 300f;

        // References to tooltip components
        private TextMeshProUGUI tooltipText;
        private RectTransform tooltipRect;
        private CanvasGroup canvasGroup;
        private Vector2 textPivot;

        // Tracking the active card
        private CardController activeCard;
        private CardController preview;

        private void Awake() {
            // Try to get references if not already assigned
            if (tooltipText == null)
                tooltipText = GetComponentInChildren<TextMeshProUGUI>();

            if (tooltipRect == null)
                tooltipRect = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            textPivot = tooltipRect.pivot;

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

        /// <summary>
        /// Shows the hovered card enlarged next to it, plus its active effects when it has any
        /// </summary>
        public void ShowTooltip(CardController card) {
            if (card == null || card.GetBaseCardData() == null) return;
            if (card.IsAnonymous) { // Fog of War: nothing about what it is
                ShowTooltip(card.IsQueued ? "<b>???</b>\nSomething is being summoned here." : "<b>???</b>\nAn enemy creature. You don't know what it is.");
                return;
            }
            if (card == activeCard && preview != null) {
                UpdateTooltipContent(card);
                return;
            }

            HideTooltip();
            activeCard = card;
            canvasGroup.alpha = 1;
            gameObject.SetActive(true);

            preview = card.CreatePreview(transform);
            if (preview != null) {
                preview.transform.localScale = Vector3.one * PreviewScale;
                PlacePreview(card, (RectTransform)preview.transform);
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
            bool hasDetails = !string.IsNullOrEmpty(details) && preview != null;
            tooltipRect.gameObject.SetActive(hasDetails);
            if (!hasDetails) return;

            tooltipText.text = details;
            float textHeight = tooltipText.GetPreferredValues(details, DetailsWidth - 20f, 0f).y;
            tooltipRect.sizeDelta = new Vector2(DetailsWidth, textHeight + 20f);
            PlaceDetailsBesidePreview();
        }

        /// <summary>Hides the tooltip if it is showing this card (a destroyed preview must not hide the next card's).</summary>
        public void HideTooltipFor(CardController card) {
            if (card != null && card == activeCard) HideTooltip();
        }

        /// <summary>
        /// Hides the tooltip
        /// </summary>
        public void HideTooltip() {
            activeCard = null;
            if (preview != null) {
                Destroy(preview.gameObject);
                preview = null;
            }
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
            previewRect.pivot = new Vector2(0.5f, showAbove ? 0f : 1f);
            previewRect.position = new Vector2((cardMin.x + cardMax.x) * 0.5f, showAbove ? cardMax.y + PreviewGap : cardMin.y - PreviewGap);
            KeepOnScreen(previewRect);
        }

        // To the right of the preview, or to its left when there is no room
        private void PlaceDetailsBesidePreview() {
            var corners = new Vector3[4];
            ((RectTransform)preview.transform).GetWorldCorners(corners); // Overlay canvas: world space is screen pixels
            tooltipRect.pivot = new Vector2(0f, 1f);
            tooltipRect.position = new Vector2(corners[2].x + PreviewGap, corners[2].y);

            tooltipRect.GetWorldCorners(corners);
            if (corners[2].x > Screen.width) {
                ((RectTransform)preview.transform).GetWorldCorners(corners);
                tooltipRect.pivot = new Vector2(1f, 1f);
                tooltipRect.position = new Vector2(corners[0].x - PreviewGap, corners[1].y);
            }
            KeepOnScreen(tooltipRect);
        }

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

        private void FollowMouse() {
            Vector2 mousePos = Input.mousePosition;
            tooltipRect.position = new Vector2(mousePos.x, mousePos.y + 40);
            KeepOnScreen(tooltipRect);
        }

        /// <summary>
        /// Text tooltips follow the mouse; card previews stay next to their card
        /// </summary>
        private void LateUpdate() {
            if (gameObject.activeSelf && activeCard == null) {
                FollowMouse();
            }
        }

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
