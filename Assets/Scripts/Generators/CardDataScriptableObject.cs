using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    // Base card scriptable object
    public abstract class CardDataScriptableObject : ScriptableObject {
        public string cardId;
        public string cardName;
        public string description;
        public CardType cardType;
        public Sprite artwork; // Shown in the card's art box; assigned by Tools > Cards > Assign Card Art
        [SerializeField] protected List<CardEffect> effects = new List<CardEffect>();

        // Initialize cardId if it's empty
        protected virtual void OnEnable() {
            if (string.IsNullOrEmpty(cardId)) {
                cardId = System.Guid.NewGuid().ToString();
            }
        }

        // Method to convert this scriptable object to a runtime CardData
        public abstract CardData ToCardData();

        // Copies the fields shared by every card type onto its runtime data
        protected void CopyCommonFieldsTo(CardData data) {
            data.cardId = cardId;
            data.cardName = cardName;
            data.description = description;
            data.cardType = cardType;
            data.artwork = artwork;
            data.effects = CopyEffects();
        }

        // Helper method to get a copy of the effects list
        protected List<CardEffect> CopyEffects() => effects.Select(effect => effect.Clone()).ToList();
    }
}
