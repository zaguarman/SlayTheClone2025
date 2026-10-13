using System.Collections.Generic;
using System.Linq;
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
            if (card.IsFaceDown) { // Fog of War: a card in the enemy's hand
                ShowTooltip("<b>???</b>\nA card in the enemy's hand.");
                return;
            }
            if (card.IsAnonymous) { // Fog of War: nothing about what it is
                ShowTooltip(card.IsQueued ? "<b>???</b>\nSomething is being summoned here." : "<b>???</b>\nAn enemy creature. You don't know what it is.");
                return;
            }
            if (card == activeCard && preview != null) {
                released = false;
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
            CloseKeywordsFrom(0);
            released = false;
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

        #region Keyword popups
        // Slay the Spire style: hovering a keyword on the enlarged card opens its explanation beside it; keywords in that
        // explanation open theirs, and so on. The pointer can leave the card for its preview and popups; the tooltip stays
        // while it's over any of them and closes a moment after it's over none.
        private const float ReleaseGrace = 0.35f; // Seconds to cross from the card to its preview
        private const int MaxKeywordPopups = 6;
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

        /// <summary>The enlarged card shown, if any.</summary>
        public CardController Preview => preview;

        /// <summary>The explanation open for this keyword, if any: its text, whose keywords can be hovered in turn.</summary>
        public TMP_Text KeywordText(string key) => keywordPopups.FirstOrDefault(p => p.Key == key)?.Body;

        /// <summary>The pointer left the card: the tooltip stays while the pointer goes on to the preview or its popups.</summary>
        public void ReleaseCard(CardController card) {
            if (card == null || card != activeCard) return;
            released = true;
            lastHeldAt = Time.unscaledTime;
        }

        private void Update() {
            if (activeCard == null || preview == null) return;
            bool overTooltip = TrackKeywords(Input.mousePosition);
            if (!released || overTooltip) lastHeldAt = Time.unscaledTime;
            else if (Time.unscaledTime - lastHeldAt > ReleaseGrace) HideTooltip();
        }

        /// <summary>
        /// Follows the pointer: on a keyword of the preview or of a popup it opens that keyword's explanation next to it,
        /// closing explanations opened further along. Returns whether the pointer is on the preview or a popup.
        /// </summary>
        public bool TrackKeywords(Vector2 pointer) {
            if (preview == null) return false;
            int over = -2; // -1: the preview; 0 and up: a popup
            for (int i = keywordPopups.Count - 1; i >= 0 && over == -2; i--) {
                if (RectTransformUtility.RectangleContainsScreenPoint(keywordPopups[i].Rect, pointer, null)) over = i;
            }
            var previewRect = (RectTransform)preview.transform;
            if (over == -2 && RectTransformUtility.RectangleContainsScreenPoint(previewRect, pointer, null)) over = -1;
            if (over == -2) return false;

            TMP_Text text = over == -1 ? preview.RulesText : keywordPopups[over].Body;
            RectTransform parent = over == -1 ? previewRect : keywordPopups[over].Rect;
            int link = text != null ? TMP_TextUtilities.FindIntersectingLink(text, pointer, null) : -1;
            if (link < 0) {
                CloseKeywordsFrom(over + 2); // What it opened stays, so the pointer can go there
                return true;
            }

            string key = text.textInfo.linkInfo[link].GetLinkID();
            int next = over + 1;
            if (next < keywordPopups.Count && keywordPopups[next].Key == key) CloseKeywordsFrom(next + 1);
            else if (Keywords.Exists(key) && next < MaxKeywordPopups) OpenKeyword(next, key, parent, text, link);
            return true;
        }

        private void OpenKeyword(int index, string key, RectTransform parent, TMP_Text parentText, int linkIndex) {
            CloseKeywordsFrom(index);
            var popup = BuildKeywordPopup(key);

            // Beside the text it was opened from, level with the keyword; on the left when there's no room on the right
            var info = parentText.textInfo.linkInfo[linkIndex];
            var character = parentText.textInfo.characterInfo[info.linkTextfirstCharacterIndex];
            float linkTop = parentText.transform.TransformPoint(character.topLeft).y;
            var corners = new Vector3[4];
            parent.GetWorldCorners(corners);
            float width = popup.Rect.rect.width * popup.Rect.lossyScale.x;
            bool right = corners[2].x + PreviewGap + width <= Screen.width;
            popup.Rect.pivot = new Vector2(right ? 0f : 1f, 1f);
            popup.Rect.position = new Vector2(right ? corners[2].x + PreviewGap : corners[0].x - PreviewGap, linkTop + 8f);
            KeepOnScreen(popup.Rect);
            keywordPopups.Add(popup);
        }

        // A parchment note like the card's text box: the keyword's name, then its explanation with its own keywords as links
        private KeywordPopup BuildKeywordPopup(string key) {
            var entry = Keywords.Get(key);
            var background = UITheme.NewImage($"Keyword: {key}", transform, UITheme.Parchment, raycastTarget: true);
            UITheme.Round(background, UITheme.Parchment, 1.5f);
            UITheme.AddTrim(background.transform, UITheme.Gold, 1.5f);
            UITheme.AddHardShadow(background, 5f);
            var rect = background.rectTransform;

            float inner = PopupWidth - 2 * PopupPadding;
            var title = UITheme.NewText("Title", rect, entry.Title, 24f, UITheme.Ink, TextAlignmentOptions.TopLeft);
            title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            var body = UITheme.NewText("Body", rect, Keywords.Describe(key), 19f, UITheme.Ink, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            float titleHeight = title.GetPreferredValues(entry.Title, inner, 0f).y;
            float bodyHeight = body.GetPreferredValues(body.text, inner, 0f).y;
            rect.sizeDelta = new Vector2(PopupWidth, PopupPadding * 2 + titleHeight + 6f + bodyHeight);
            PlaceInPopup(title.rectTransform, PopupPadding, titleHeight);
            PlaceInPopup(body.rectTransform, PopupPadding + titleHeight + 6f, bodyHeight);
            body.ForceMeshUpdate(); // Its links can be found right away
            return new KeywordPopup { Key = key, Rect = rect, Body = body };
        }

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
