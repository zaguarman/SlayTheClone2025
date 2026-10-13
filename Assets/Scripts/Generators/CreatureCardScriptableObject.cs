using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class CreatureCardScriptableObject : CardDataScriptableObject {
        public int attack;
        public int health;
        [Min(1)] public int speed = 1; // Faster creatures strike first
        [Min(0)] public int turnsUntilOperative; // Turns on the board before it can attack, move or use its effects
        public MovementKeyword movement; // Leapfrog, Swap (GDD 2.4 Movement Keywords)
        public CreatureAbility ability; // Its one ability ('Abilities Compendium')
        public bool gift; // Played into an empty enemy slot, it joins their side (still your card)
        public bool token; // Made by an effect, never in a deck: when it leaves play, it's gone
        public CreatureCardScriptableObject disguise; // What the other side sees it as until it's revealed

        protected override void OnEnable() {
            base.OnEnable(); // Call base implementation to handle cardId
            cardType = CardType.Creature;
        }

        public override CardData ToCardData() {
            CreatureData data = ScriptableObject.CreateInstance<CreatureData>();
            CopyCommonFieldsTo(data);
            data.attack = attack;
            data.health = health;
            data.speed = speed;
            data.turnsUntilOperative = turnsUntilOperative;
            data.movement = movement;
            data.ability = ability;
            data.gift = gift;
            data.token = token;
            data.disguise = disguise != null && disguise != this ? (CreatureData)disguise.ToCardData() : null;
            return data;
        }
    }
}
