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
        [Min(0)] public int discardCost; // Additional cost: other cards from the hand, at random, given up to play it
        public bool singleUse; // Once played, gone for the rest of the combat (The Golden Gun's one bullet)
        [Min(0)] public int sacrifice; // Myth's cost: its side's weakest creatures die to play it
        [Min(0)] public int prophecy; // A spell that comes true this many Days later, at Dawn
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
            data.discardCost = discardCost;
            data.singleUse = singleUse;
            data.sacrifice = sacrifice;
            data.prophecy = prophecy;
            data.effects = CopyEffects();
        }

        // A copy of the effects list; the tokens they summon get their runtime data now, on the thread that reads the card
        protected List<CardEffect> CopyEffects() {
            var copies = effects.Select(effect => effect.Clone()).ToList();
            foreach (var action in copies.SelectMany(e => e.actions).Where(a => a.decoy != null)) {
                action.decoyData = (CreatureData)action.decoy.ToCardData();
            }
            return copies;
        }

#if UNITY_EDITOR
        // For card generators: replaces the card's effects with copies of the given ones
        public void SetEffects(IEnumerable<CardEffect> newEffects) => effects = newEffects.Select(effect => effect.Clone()).ToList();
#endif
    }
}
