using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zeitgeist {
    /// <summary>
    /// The table between the two hands: both battlefields and the river between them. A card that plays on either side
    /// alike (a Trend, or a spell that does something to both sides) can be dropped anywhere here, not only on a
    /// battlefield, and both battlefields light up while it's carried over the table. It lies under the battlefields, so
    /// everything dropped on them is theirs; it only gets what lands between them. Made and sized by <see cref="BoardSkin"/>.
    /// </summary>
    public class PlayArea : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler {
        private BattlefieldUI player1Battlefield;
        private BattlefieldUI player2Battlefield;

        /// <summary>An invisible area that takes drops, under everything else on the table.</summary>
        public static PlayArea Ensure(RectTransform canvasRoot, BattlefieldUI player1, BattlefieldUI player2) {
            var existing = canvasRoot.Find("PlayArea");
            var area = existing != null ? existing.GetComponent<PlayArea>() : null;
            if (area == null) {
                var image = UITheme.NewImage("PlayArea", canvasRoot, Color.clear);
                image.raycastTarget = true; // Clear, but it takes the drops that land on it
                area = image.gameObject.AddComponent<PlayArea>();
            }
            area.player1Battlefield = player1;
            area.player2Battlefield = player2;
            return area;
        }

        // The battlefield of the side the card was played from
        private BattlefieldUI OwnBattlefield(CardController card) =>
            card?.Player == null ? null : card.Player.IsPlayer1 ? player1Battlefield : player2Battlefield;

        private static CardController Carried(PointerEventData eventData) =>
            eventData?.pointerDrag != null ? eventData.pointerDrag.GetComponent<CardController>() : null;

        public void OnDrop(PointerEventData eventData) {
            var card = Carried(eventData);
            var battlefield = OwnBattlefield(card);
            if (battlefield != null && battlefield.PlaysAnywhere(card)) battlefield.PlayAnywhere(card);
        }

        public void OnPointerEnter(PointerEventData eventData) {
            var card = Carried(eventData);
            var battlefield = OwnBattlefield(card);
            if (battlefield != null && battlefield.PlaysAnywhere(card)) LightUp(true);
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (Carried(eventData) != null) LightUp(false);
        }

        private void LightUp(bool lit) {
            player1Battlefield?.LightUp(lit);
            player2Battlefield?.LightUp(lit);
        }
    }
}
