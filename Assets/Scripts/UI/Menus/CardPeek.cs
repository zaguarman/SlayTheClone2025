using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zeitgeist {
    /// <summary>
    /// A card shown enlarged beside the pointer while something naming it is hovered: a card's name in the keyword glossary,
    /// a row of the deck builder's deck. It floats over the menu, never laid out in it. The pointer goes through it, unless
    /// it's <see cref="Readable"/>: then it can be hovered to read its keywords and right-clicked (the deck builder takes
    /// the card out, the user, 2026-10-17), and it stays while the pointer is on it.
    /// </summary>
    public class CardPeek {
        private const float Scale = 1.8f;
        private const float Gap = 24f; // Screen pixels between the pointer and the card

        private readonly Transform parent;
        private readonly CardViews cardViews;

        /// <summary>The card shown, if any.</summary>
        public CardController Shown { get; private set; }
        public CardDataScriptableObject ShownCard { get; private set; }
        /// <summary>The point the shown card was placed beside.</summary>
        public Vector2 ShownAt { get; private set; }
        /// <summary>The shown card takes the pointer: its keywords explain themselves and it can be right-clicked.</summary>
        public bool Readable { get; set; }
        /// <summary>What a right click on the shown card does, when it's <see cref="Readable"/>.</summary>
        public System.Action<CardDataScriptableObject> RightClicked { get; set; }

        public CardPeek(Transform parent, CardController cardPrefab) {
            this.parent = parent;
            cardViews = CardViews.ForDisplay(cardPrefab);
        }

        /// <summary>Shows the card above and to the right of this screen point, or wherever it fits; null hides it.</summary>
        public void Show(CardDataScriptableObject card, Vector2 near) {
            if (card == ShownCard) return;
            Hide();
            if (card == null || parent == null) return;
            ShownCard = card;
            ShownAt = near;
            Shown = cardViews.Create(CardFactory.CreateCard(card.ToCardData()), null, parent);
            if (Shown == null) return;

            if (!Shown.TryGetComponent(out LayoutElement element)) element = Shown.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            Shown.GetComponent<CanvasGroup>().blocksRaycasts = Readable;
            if (Readable) {
                Shown.ShowsItself = true; // Its keywords pop up on it, as on a gallery card
                Shown.OnCardRightClicked.AddListener(_ => RightClicked?.Invoke(card));
            }
            var rect = (RectTransform)Shown.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.localScale = Vector3.one * Scale;
            rect.SetAsLastSibling();

            // Overlay canvas: world space is screen pixels
            rect.position = near + new Vector2(Gap, Gap);
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float width = corners[2].x - corners[0].x, height = corners[2].y - corners[0].y;
            var position = rect.position;
            if (corners[2].x > Screen.width) position.x = near.x - Gap - width;
            if (corners[2].y > Screen.height) position.y = Screen.height - height;
            position.y = Mathf.Max(position.y, 0f);
            rect.position = position;
        }

        /// <summary>The screen point is on the shown card (or within this margin of it).</summary>
        public bool Under(Vector2 point, float margin = 0f) {
            if (Shown == null) return false;
            var corners = new Vector3[4];
            ((RectTransform)Shown.transform).GetWorldCorners(corners);
            return point.x >= corners[0].x - margin && point.x <= corners[2].x + margin && point.y >= corners[0].y - margin && point.y <= corners[2].y + margin;
        }

        public void Hide() {
            if (Shown != null) Object.Destroy(Shown.gameObject);
            Shown = null;
            ShownCard = null;
        }
    }

    /// <summary>
    /// Shows a card with a <see cref="CardPeek"/> once the pointer rests on this object, after the same moment as a hovered
    /// card (<see cref="Tooltip.HoverDelay"/>), until it leaves. A <see cref="CardPeek.Readable"/> card stays while the
    /// pointer crosses over to it and is on it, and goes once the pointer has left both for a moment.
    /// </summary>
    public class CardPeekOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        private const float Grace = 0.3f; // The moment the pointer may be between the row and the card
        private const float Margin = 8f;

        private CardPeek peek;
        private CardDataScriptableObject card;

        public static void AddTo(GameObject target, CardPeek peek, CardDataScriptableObject card) {
            var hover = target.AddComponent<CardPeekOnHover>();
            hover.peek = peek;
            hover.card = card;
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (peek == null) return;
            StopAllCoroutines();
            StartCoroutine(ShowAfter(peek.Shown != null ? Tooltip.SwitchDelay : Tooltip.HoverDelay));
        }

        // Rebuilt while its card is shown (a copy was just taken out on the card): it watches the pointer for it
        private void Start() {
            if (peek != null && peek.Readable && peek.ShownCard == card) StartCoroutine(HideWhenLeft());
        }

        // Waits the delay, starting over whenever the wheel turns, so nothing pops up while scrolling
        private IEnumerator ShowAfter(float delay) {
            float ready = Time.unscaledTime + delay;
            while (Time.unscaledTime < ready) {
                if (Tooltip.WheelMoved) ready = Time.unscaledTime + Tooltip.HoverDelay;
                yield return null;
            }
            peek.Show(card, GameInput.PointerPosition);
        }

        // Scrolling hides the card for a moment
        private void Update() {
            if (!Tooltip.WheelMoved || peek == null || peek.ShownCard != card) return;
            peek.Hide();
            StopAllCoroutines();
            StartCoroutine(ShowAfter(Tooltip.HoverDelay));
        }

        public void OnPointerExit(PointerEventData eventData) {
            StopAllCoroutines();
            if (peek == null || peek.ShownCard != card) return;
            if (peek.Readable && isActiveAndEnabled) StartCoroutine(HideWhenLeft());
            else peek.Hide();
        }

        private IEnumerator HideWhenLeft() {
            float away = -1f;
            while (peek != null && peek.ShownCard == card) {
                var pointer = GameInput.PointerPosition;
                bool over = peek.Under(pointer, Margin) || RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, pointer);
                if (over) away = -1f;
                else if (away < 0f) away = Time.unscaledTime;
                else if (Time.unscaledTime - away > Grace) break;
                yield return null;
            }
            if (peek != null && peek.ShownCard == card) peek.Hide();
        }

        private void OnDisable() {
            StopAllCoroutines();
            if (peek != null && peek.ShownCard == card) peek.Hide();
        }
    }
}
