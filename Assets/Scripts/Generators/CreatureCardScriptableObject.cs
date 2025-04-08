using UnityEngine;
using static Enums;

public class CreatureCardScriptableObject : CardDataScriptableObject {
    public int attack;
    public int health;

    protected override void OnEnable() {
        base.OnEnable(); // Call base implementation to handle cardId
        cardType = CardType.Creature;
    }

    public override CardData ToCardData() {
        CreatureData data = ScriptableObject.CreateInstance<CreatureData>();
        data.cardId = cardId; // Copy the cardId
        data.cardName = cardName;
        data.description = description;
        data.cardType = cardType;
        data.attack = attack;
        data.health = health;
        data.effects = CopyEffects();
        return data;
    }
}
