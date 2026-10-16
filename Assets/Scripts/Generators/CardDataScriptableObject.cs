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
        [Min(0)] public int discardCost; // Additional cost: other cards from the hand given up to play it
        public bool discardChosen; // Picked by hand (calculated) rather than at random (chaotic)
        public bool singleUse; // Once played, gone for the rest of the combat (The Golden Gun's one bullet)
        [Min(0)] public int sacrifice; // Myth's cost: its side's weakest creatures die to play it
        [Min(0)] public int doomCost; // Doom N: its side's weakest creature not yet Doomed is Doomed N Days to play it
        [Min(0)] public int prophecy; // A spell that comes true this many Days later, at Dawn
        [Min(0)] public int prophecyLatest; // Above prophecy: it comes true at random, between the two
        public bool gamble; // Only one of its effects happens, at random (Casino)
        public bool homing; // Homing: aimed at an enemy creature it locks on and follows it; Heat Signature pulls it
        public bool ripening; // Ripening: +1 to its numbers for each Dawn it spends in your hand (up to +3)
        public bool policy; // Policy: it happens when played and again each Dawn, for the rest of the combat
        public bool upgraded; // An upgraded copy made in an adventure (CardUpgrades): never an asset
        public bool potion; // An adventure's potion: Single Use, carried from fight to fight, in the opening hand
        public bool combo; // Combo: +1 to its numbers for each other spell you play the same Day
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
            data.discardChosen = discardChosen;
            data.singleUse = singleUse;
            data.sacrifice = sacrifice;
            data.doomCost = doomCost;
            data.prophecy = prophecy;
            data.prophecyLatest = prophecyLatest;
            data.gamble = gamble;
            data.homing = homing;
            data.ripening = ripening;
            data.policy = policy;
            data.upgraded = upgraded;
            data.potion = potion;
            data.combo = combo;
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

        // A runtime copy's new effects (an upgraded card, see CardUpgrades): never saved to an asset
        public void ReplaceEffects(IEnumerable<CardEffect> newEffects) => effects = newEffects.Select(effect => effect.Clone()).ToList();

#if UNITY_EDITOR
        // For card generators: replaces the card's effects with copies of the given ones
        public void SetEffects(IEnumerable<CardEffect> newEffects) => effects = newEffects.Select(effect => effect.Clone()).ToList();
#endif
    }
}
