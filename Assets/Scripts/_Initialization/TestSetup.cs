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

        // Fireball - direct damage spell
        var fireball = ScriptableObject.CreateInstance<SpellData>();
        fireball.cardName = "Fireball";
        fireball.spellPower = 3;
        fireball.description = "Deal 3 damage to a creature or player.";
        fireball.defaultTargetType = TargetType.AllCreatures;

        fireball.effects.Add(new CardEffect {
            effectType = EffectType.Immediate,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = 3,
                    targetType = TargetType.AllCreatures
                }
            }
        });

        spells.Add(fireball);

        // Storm - damage all enemy creatures
        var storm = ScriptableObject.CreateInstance<SpellData>();
        storm.cardName = "Storm";
        storm.spellPower = 1;
        storm.description = "Deal 1 damage to all enemy creatures.";
        storm.defaultTargetType = TargetType.EnemyCreatures;

        storm.effects.Add(new CardEffect {
            effectType = EffectType.Immediate,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = 1,
                    targetType = TargetType.EnemyCreatures
                }
            }
        });

        spells.Add(storm);

        // Insight - draw cards
        var insight = ScriptableObject.CreateInstance<SpellData>();
        insight.cardName = "Insight";
        insight.spellPower = 0;
        insight.description = "Draw 2 cards.";
        insight.defaultTargetType = TargetType.Player;

        insight.effects.Add(new CardEffect {
            effectType = EffectType.Immediate,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Draw,
                    value = 2,
                    targetType = TargetType.Player
                }
            }
        });

        spells.Add(insight);

        return spells;
    }
}