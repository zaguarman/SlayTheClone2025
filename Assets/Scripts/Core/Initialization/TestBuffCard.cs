using UnityEngine;
using System.Collections.Generic;
using static Enums;

public class TestBuffCard : MonoBehaviour {
    public void CreateBuffCards() {
        // Create a test buff spell for attack only
        var buffSpell = ScriptableObject.CreateInstance<SpellData>();
        buffSpell.cardName = "Attack Potion";
        buffSpell.description = "Give a creature +2 attack";
        buffSpell.defaultTargetType = TargetType.FriendlyCreatures;

        var effect = new CardEffect {
            effectType = EffectType.Immediate,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Buff,
                    value = 2,
                    targetType = TargetType.FriendlyCreatures,
                    buffAttack = true,
                    buffHealth = false
                }
            }
        };

        buffSpell.effects.Add(effect);

        // Create a creature with a buff effect
        var buffCreature = ScriptableObject.CreateInstance<CreatureData>();
        buffCreature.cardName = "War Chief";
        buffCreature.description = "When played, gives all friendly creatures +1/+1";
        buffCreature.attack = 3;
        buffCreature.health = 3;

        var creatureEffect = new CardEffect {
            effectType = EffectType.Immediate,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Buff,
                    value = 1,
                    targetType = TargetType.FriendlyCreatures
                }
            }
        };

        buffCreature.effects.Add(creatureEffect);

        // Add these cards to the game
        var gameManager = GameManager.Instance;
        if (gameManager != null && gameManager.cardDealingService != null) {
            var player1 = gameManager.Player1;
            if (player1 != null) {
                // Create the cards
                var spell = CardFactory.CreateCard(buffSpell);
                var creature = CardFactory.CreateCard(buffCreature);

                // Add to player's hand
                player1.AddToHand(spell);
                player1.AddToHand(creature);

                Debug.Log("Added buff test cards to Player 1's hand");
            }
        }
    }
}
