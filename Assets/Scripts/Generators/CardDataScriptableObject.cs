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
        public Faction faction;
        public Sprite artwork; // Shown in the card's art box; assigned by Tools > Cards > Assign Card Art
        public int priority; // Printed Priority (Action): a spell's Spell Action, a creature's attacks (GDD 3.7). Usually 0
        [SerializeField] protected List<CardEffect> effects = new List<CardEffect>();
        [TextArea(2, 6)] public string designNotes; // Editor only: what the card should become once the GDD mechanics it stands in for exist
        [TextArea(2, 6)] public string artDescription; // What the card's picture shows: the brief for its art

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
            data.faction = faction;
            data.artwork = artwork;
            data.priority = priority;
            data.effects = CopyEffects();
        }

        // Helper method to get a copy of the effects list
        protected List<CardEffect> CopyEffects() => effects.Select(effect => effect.Clone()).ToList();

#if UNITY_EDITOR
        // For card generators: replaces the card's effects with copies of the given ones
        public void SetEffects(IEnumerable<CardEffect> newEffects) => effects = newEffects.Select(effect => effect.Clone()).ToList();
#endif
    }
}
