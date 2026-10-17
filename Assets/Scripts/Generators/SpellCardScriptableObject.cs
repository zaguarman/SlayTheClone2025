using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    public class SpellCardScriptableObject : CardDataScriptableObject {
        public TargetType defaultTargetType;

        protected override void OnEnable() {
            base.OnEnable(); // Call base implementation to handle cardId
            cardType = CardType.Spell;
        }

        public override CardData ToCardData() {
            SpellData data = ScriptableObject.CreateInstance<SpellData>();
            CopyCommonFieldsTo(data);
            data.defaultTargetType = defaultTargetType;
            return data;
        }
    }
}
