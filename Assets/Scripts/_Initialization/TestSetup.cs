using System.Collections.Generic;
using UnityEngine;
using static Enums;
using static DebugLogger;

public class TestSetup : MonoBehaviour {
    public List<CardData> CreateTestCards() {
        var cards = new List<CardData>();

        // Add basic creatures
        cards.Add(CreateBasicCreature("Goblin", 2, 1, "A small but annoying creature."));
        cards.Add(CreateBasicCreature("Wolf", 3, 2, "Howls at the moon."));
        cards.Add(CreateBasicCreature("Troll", 4, 4, "Regenerates health over time."));
        cards.Add(CreateBasicCreature("Knight", 3, 3, "Devoted to protecting the realm."));

        // Add the venomous spider with end-of-turn damage effect
        cards.Add(CreateVenomousSpider());

        // Add spell cards
        cards.Add(CreateSpell("Fireball", "Deal 3 damage to a target.", TargetType.AllCreatures));
        cards.Add(CreateSpell("Heal", "Restore 3 health to a friendly creature.", TargetType.FriendlyCreatures));
        cards.Add(CreateSpell("Lightning Bolt", "Deal 2 damage to all enemy creatures.", TargetType.EnemyCreatures));
        cards.Add(CreateSpell("Dark Ritual", "Sacrifice a creature to draw 2 cards.", TargetType.FriendlyCreatures));

        return cards;
    }

    private CreatureData CreateBasicCreature(string name, int attack, int health, string description) {
        var creature = ScriptableObject.CreateInstance<CreatureData>();
        creature.cardName = name;
        creature.attack = attack;
        creature.health = health;
        creature.description = description;
        creature.cardType = CardType.Creature;
        return creature;
    }

    private CreatureData CreateVenomousSpider() {
        var spider = ScriptableObject.CreateInstance<CreatureData>();
        spider.cardName = "Venomous Spider";
        spider.attack = 2;
        spider.health = 3;
        spider.description = "At the end of each turn, deals 1 damage to all enemy creatures.";
        spider.cardType = CardType.Creature;

        // Create the end-of-turn effect
        var effect = new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.EndOfTurn,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = 1,
                    targetType = TargetType.EnemyCreatures
                }
            }
        };

        spider.effects.Add(effect);
        Log("Created Venomous Spider with end-of-turn damage effect", LogTag.Initialization | LogTag.Cards);

        return spider;
    }

    private SpellData CreateSpell(string name, string description, TargetType defaultTargetType) {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.cardName = name;
        spell.description = description;
        spell.cardType = CardType.Spell;
        spell.defaultTargetType = defaultTargetType;
        return spell;
    }
}