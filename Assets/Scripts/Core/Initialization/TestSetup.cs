using System.Collections.Generic;
using UnityEngine;
using static DebugLogger;
using static Enums;

public class TestSetup : MonoBehaviour {
    // Create test cards for development purposes
    public List<CardData> CreateTestCards() {
        var cards = new List<CardData>();

        // Add some creatures
        cards.Add(CreateCreature("Goblin Scout", 1, 1, "A small, agile scout."));
        cards.Add(CreateCreature("Knight", 3, 2, "A brave knight with shining armor."));
        cards.Add(CreateCreature("Ancient Dragon", 5, 5, "A powerful dragon that breathes fire."));
        cards.Add(CreateCreature("Grizzly Bear", 2, 3, "A wild bear with sharp claws."));
        cards.Add(CreateCreature("Forest Spirit", 1, 4, "A spirit that protects the forest."));
        cards.Add(CreateCreature("Shadow Assassin", 4, 1, "Strikes silently from the shadows."));

        // Add more powerful creatures
        cards.Add(CreateCreature("Flame Titan", 4, 4, "A huge elemental titan of flame."));
        cards.Add(CreateCreature("Earth Golem", 3, 5, "A massive creature of living stone."));
        cards.Add(CreateCreature("Frost Giant", 4, 5, "A towering giant from the frozen wastes."));

        // Add creatures with OnDamage effects
        var thorn = CreateCreature("Thorn Beast", 2, 3, "Covered in sharp thorns that damage attackers.");
        AddDamageEffect(thorn, 1, TargetType.AllCreatures);
        cards.Add(thorn);

        // Add creatures with OnDeath effects
        var exploder = CreateCreature("Exploding Imp", 1, 1, "Explodes when destroyed, damaging all creatures.");
        AddDeathEffect(exploder, 2, TargetType.AllCreatures);
        cards.Add(exploder);

        // Add some spells
        cards.Add(CreateDirectDamageSpell("Fireball", 3, TargetType.EnemyCreatures, "Deals 3 damage to target creature."));
        cards.Add(CreateDrawCardSpell("Arcane Intellect", 2, "Draw 2 cards."));
        cards.Add(CreateHealSpell("Healing Touch", 4, TargetType.FriendlyCreatures, "Heals a friendly creature for 4."));
        cards.Add(CreateDirectDamageSpell("Lightning Bolt", 2, TargetType.Enemy, "Deals 2 damage to the enemy player."));

        // Add more spells
        cards.Add(CreateDirectDamageSpell("Meteor Strike", 4, TargetType.AllCreatures, "Deals 4 damage to all creatures."));
        cards.Add(CreateHealSpell("Divine Blessing", 3, TargetType.Player, "Heals you for 3."));
        cards.Add(CreateWeatherSpell("Call Lightning", WeatherType.Rainy, "Changes weather to Rainy."));
        cards.Add(CreateWeatherSpell("Sunny Day", WeatherType.Sunny, "Changes weather to Sunny."));

        Log($"Created {cards.Count} test cards", LogTag.Cards | LogTag.Initialization);
        return cards;
    }

    private CreatureData CreateCreature(string name, int attack, int health, string description = "") {
        var creature = ScriptableObject.CreateInstance<CreatureData>();
        creature.cardName = name;
        creature.attack = attack;
        creature.health = health;
        creature.description = description;
        return creature;
    }

    private SpellData CreateDirectDamageSpell(string name, int damage, TargetType targetType, string description = "") {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.cardName = name;
        spell.description = description;
        spell.defaultTargetType = targetType;

        var effect = new CardEffect {
            effectType = EffectType.Immediate,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = damage,
                    targetType = targetType
                }
            }
        };

        spell.effects.Add(effect);
        return spell;
    }

    private SpellData CreateDrawCardSpell(string name, int cards, string description = "") {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.cardName = name;
        spell.description = description;
        spell.defaultTargetType = TargetType.Player;

        var effect = new CardEffect {
            effectType = EffectType.Immediate,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Draw,
                    value = cards,
                    targetType = TargetType.Player
                }
            }
        };

        spell.effects.Add(effect);
        return spell;
    }

    private SpellData CreateHealSpell(string name, int amount, TargetType targetType, string description = "") {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.cardName = name;
        spell.description = description;
        spell.defaultTargetType = targetType;

        var effect = new CardEffect {
            effectType = EffectType.Immediate,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Heal,
                    value = amount,
                    targetType = targetType
                }
            }
        };

        spell.effects.Add(effect);
        return spell;
    }

    private SpellData CreateWeatherSpell(string name, WeatherType weatherType, string description = "") {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.cardName = name;
        spell.description = description;
        spell.defaultTargetType = TargetType.Player;

        // Note: Since there's no direct weather action type, we'll have to handle this differently
        // For now, we're just creating the card data, but the actual weather change would be 
        // handled in gameplay code

        return spell;
    }

    private void AddDamageEffect(CreatureData creature, int damage, TargetType targetType) {
        var effect = new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnDamage,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = damage,
                    targetType = targetType
                }
            }
        };

        creature.effects.Add(effect);
    }

    private void AddDeathEffect(CreatureData creature, int damage, TargetType targetType) {
        var effect = new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnDeath,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = damage,
                    targetType = targetType
                }
            }
        };

        creature.effects.Add(effect);
    }
}