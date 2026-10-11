using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class CreatureCardScriptableObject : CardDataScriptableObject {
        public int attack;
        public int health;
        [Min(1)] public int speed = 1; // Faster creatures strike first

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
            return data;
        }
    }
}
