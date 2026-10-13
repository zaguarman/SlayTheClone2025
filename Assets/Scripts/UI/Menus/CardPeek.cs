using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>
    /// A card shown enlarged beside the pointer while something naming it is hovered: a card's name in the keyword glossary,
    /// a row of the deck builder's deck. It floats over the menu, never laid out in it, and the pointer goes through it.
    /// </summary>
    public class CardPeek {
        private const float Scale = 1.8f;
        private const float Gap = 24f; // Screen pixels between the pointer and the card

        private readonly Transform parent;
        private readonly CardViews cardViews;

        /// <summary>The card shown, if any.</summary>
        public CardController Shown { get; private set; }
        public CardDataScriptableObject ShownCard { get; private set; }

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
            Shown = cardViews.Create(CardFactory.CreateCard(card.ToCardData()), null, parent);
            if (Shown == null) return;

            if (!Shown.TryGetComponent(out LayoutElement element)) element = Shown.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            Shown.GetComponent<CanvasGroup>().blocksRaycasts = false;
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

        public void Hide() {
            if (Shown != null) Object.Destroy(Shown.gameObject);
            Shown = null;
            ShownCard = null;
        }
    }

    /// <summary>Shows a card with a <see cref="CardPeek"/> while the pointer is on this object.</summary>
    public class CardPeekOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        private CardPeek peek;
        private CardDataScriptableObject card;

        public static void AddTo(GameObject target, CardPeek peek, CardDataScriptableObject card) {
            var hover = target.AddComponent<CardPeekOnHover>();
            hover.peek = peek;
            hover.card = card;
        }

        public void OnPointerEnter(PointerEventData eventData) => peek?.Show(card, eventData.position);

        public void OnPointerExit(PointerEventData eventData) => Stop();

        private void OnDisable() => Stop();

        private void Stop() {
            if (peek != null && peek.ShownCard == card) peek.Hide();
        }
    }
}
