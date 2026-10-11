using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class CreatureCardScriptableObject : CardDataScriptableObject {
        public int attack;
        public int health;

        protected override void OnEnable() {
            base.OnEnable(); // Call base implementation to handle cardId
            cardType = CardType.Creature;
        }

        public override CardData ToCardData() {
            CreatureData data = ScriptableObject.CreateInstance<CreatureData>();
            CopyCommonFieldsTo(data);
            data.attack = attack;
            data.health = health;
            return data;
        }
    }
}
