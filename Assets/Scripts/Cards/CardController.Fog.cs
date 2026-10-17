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
    /// <summary>A card on screen: its faces under the Fog of War (a part of CardController).</summary>
    public partial class CardController {

        #region Fog of War faces
        /// <summary>
        /// Fog of War: the player at the screen doesn't know what this creature is (being summoned by the other side, or stealthy).
        /// It shows as an empty black card with three question marks, and hovering it tells nothing.
        /// </summary>
        public void SetAnonymous(bool anonymous) {
            if (anonymous == IsAnonymous) return;
            IsAnonymous = anonymous;
            if (anonymous && unknownFace == null) unknownFace = CreateFace("UnknownFace", "FogOfWar/UnknownCreature");
            ShowFace(anonymous ? unknownFace : null);
        }

        /// <summary>Fog of War: a card in the enemy's hand. Only its back shows, and hovering it tells nothing.</summary>
        public void SetFaceDown(bool faceDown) {
            if (faceDown == IsFaceDown) return;
            IsFaceDown = faceDown;
            if (faceDown && backFace == null) backFace = CreateFace("CardBack", "FogOfWar/CardBack");
            ShowFace(faceDown ? backFace : null);
        }

        /// <summary>
        /// Fog of War: a decoy the player at the screen doesn't see through. The card shows the card it pretends to be, with
        /// that card's printed stats, and nothing of its own (null: its own face again).
        /// </summary>
        public void SetDisguise(CardData fake) {
            if (fake == disguise) return;
            if (disguise == null) realCardData = baseCardData;
            disguise = fake;
            baseCardData = fake ?? realCardData ?? baseCardData;
            UpdateUI();
        }

        public bool IsDisguised => disguise != null;
        private CardData disguise, realCardData;

        public bool IsAnonymous { get; private set; }
        public bool IsFaceDown { get; private set; }
        private GameObject unknownFace, backFace;
        private readonly List<GameObject> hiddenFace = new List<GameObject>();

        // Shows one of the Fog of War faces instead of the card's own (or the card's own again, for null). The card's parts are
        // switched off rather than covered: a queued card is translucent, and they would show through
        private void ShowFace(GameObject face) {
            foreach (var part in hiddenFace) if (part != null) part.SetActive(true);
            hiddenFace.Clear();
            if (unknownFace != null) unknownFace.SetActive(false);
            if (backFace != null) backFace.SetActive(false);
            if (face == null) return;

            foreach (Transform child in transform) {
                if (!child.gameObject.activeSelf) continue;
                hiddenFace.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }
            face.SetActive(true);
            face.transform.SetAsLastSibling();
        }

        // The whole card, drawn by Tools/generate_card_art.py
        private GameObject CreateFace(string objectName, string spritePath) {
            var face = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            face.layer = gameObject.layer;
            face.transform.SetParent(transform, false);
            var rect = (RectTransform)face.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = face.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(spritePath);
            image.color = image.sprite != null ? Color.white : UITheme.TableDark;
            image.raycastTarget = false;
            face.SetActive(false);
            return face;
        }
        #endregion
    }
}
