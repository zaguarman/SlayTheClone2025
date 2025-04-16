using UnityEngine;
using Enums;

public class SpellCardScriptableObject : CardDataScriptableObject {
    public TargetType defaultTargetType;

    protected override void OnEnable() {
        base.OnEnable(); // Call base implementation to handle cardId
        cardType = CardType.Spell;
    }

    public override CardData ToCardData() {
        SpellData data = ScriptableObject.CreateInstance<SpellData>();
        data.cardId = cardId; // Copy the cardId
        data.cardName = cardName;
        data.description = description;
        data.cardType = cardType;
        data.defaultTargetType = defaultTargetType;
        data.effects = CopyEffects();
        return data;
    }
}