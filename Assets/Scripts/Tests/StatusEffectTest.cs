using UnityEngine;
using static Enums;

public class StatusEffectTest : MonoBehaviour
{
    public void CreateBurnedStatusCard()
    {
        if (GameManager.Instance?.Player1 == null) return;

        // Create a card that applies Burned status
        var burnCard = ScriptableObject.CreateInstance<CreatureData>();
        burnCard.cardName = "Scorching Spider";
        burnCard.description = "Burns a random enemy creature for 3 damage per turn for 2 turns.";
        burnCard.attack = 3;
        burnCard.health = 3;

        var effect = new CardEffect
        {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnPlay,
            actions = new System.Collections.Generic.List<EffectAction>
            {
                new EffectAction
                {
                    actionType = ActionType.ApplyStatus,
                    value = (int)StatusEffectType.Burned, // Cast enum to int
                    targetType = TargetType.EnemyCreatures,
                    targetModifier = TargetModifier.Random
                }
            }
        };

        burnCard.effects.Add(effect);

        // Create the card and add to player's hand
        var creature = CardFactory.CreateCard(burnCard);
        GameManager.Instance.Player1.AddToHand(creature);

        Debug.Log("Added Scorching Spider card to Player 1's hand");
    }

    public void CreateParalyzedStatusCard()
    {
        if (GameManager.Instance?.Player1 == null) return;

        // Create a card that applies Paralyzed status
        var paralyzeCard = ScriptableObject.CreateInstance<CreatureData>();
        paralyzeCard.cardName = "Venomous Spider";
        paralyzeCard.description = "Paralyzes an enemy creature for 2 turns when played.";
        paralyzeCard.attack = 2;
        paralyzeCard.health = 2;

        var effect = new CardEffect
        {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnPlay,
            actions = new System.Collections.Generic.List<EffectAction>
            {
                new EffectAction
                {
                    actionType = ActionType.ApplyStatus,
                    value = (int)StatusEffectType.Paralyzed, // Cast enum to int
                    targetType = TargetType.EnemyCreatures
                }
            }
        };

        paralyzeCard.effects.Add(effect);

        // Create the card and add to player's hand
        var creature = CardFactory.CreateCard(paralyzeCard);
        GameManager.Instance.Player1.AddToHand(creature);

        Debug.Log("Added Venomous Spider card to Player 1's hand");
    }
}
