using System.Collections.Generic;
using UnityEngine;
using static Enums;

public class TestSetup : MonoBehaviour {
    public List<CardData> CreateTestCards() {
        var cards = new List<CardData>();

        // Add creatures
        cards.AddRange(CreateTestCreatures());

        // Add spells
        cards.AddRange(CreateTestSpells());

        return cards;
    }

    private List<CardData> CreateTestCreatures() {
        var creatures = new List<CardData>();

        // Create a basic test creature
        var basicCreature = ScriptableObject.CreateInstance<CreatureData>();
        basicCreature.cardName = "Warrior";
        basicCreature.attack = 2;
        basicCreature.health = 3;
        basicCreature.description = "A basic warrior.";
        creatures.Add(basicCreature);

        // Create a creature with a damage effect
        var effectCreature = ScriptableObject.CreateInstance<CreatureData>();
        effectCreature.cardName = "Fire Elemental";
        effectCreature.attack = 3;
        effectCreature.health = 2;
        effectCreature.description = "Deals 1 damage when damaged.";

        var damageEffect = new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnDamage,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = 1,
                    targetType = TargetType.Enemy
                }
            }
        };

        effectCreature.effects.Add(damageEffect);
        creatures.Add(effectCreature);

        return creatures;
    }

    private List<CardData> CreateTestSpells() {
        var spells = new List<CardData>();

        // Fireball - direct damage spell + draw a card
        var fireball = ScriptableObject.CreateInstance<SpellData>();
        fireball.cardName = "Fireball";
        fireball.description = "Deal 3 damage to target. Draw a card.";
        fireball.defaultTargetType = TargetType.AllCreatures;

        spells.Add(fireball);

        // Storm - damage all enemy creatures + heal friendly creatures
        var storm = ScriptableObject.CreateInstance<SpellData>();
        storm.cardName = "Storm";
        storm.description = "Deal 1 damage to all enemy creatures. Heal all friendly creatures for 1.";
        storm.defaultTargetType = TargetType.EnemyCreatures;

        spells.Add(storm);

        // Insight - draw cards + heal player
        var insight = ScriptableObject.CreateInstance<SpellData>();
        insight.cardName = "Insight";
        insight.description = "Draw 2 cards. Heal yourself for 1.";
        insight.defaultTargetType = TargetType.Player;

        spells.Add(insight);

        // Note: We don't need to add effects here anymore - the actions are configured in CardFactory.ConfigureSpellActions

        return spells;
    }
}