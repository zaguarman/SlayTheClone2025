using UnityEngine;
using System.Collections.Generic;
using static DebugLogger;
using static Enums;
using System.Linq;

public class TestSetup : MonoBehaviour {
    public List<CardData> CreateTestCards() {
        var cards = new List<CardData>();

        // Create diverse creature cards with interesting abilities
        cards.Add(CreateCreature("Shadowy Assassin", 3, 2, CreateRetaliateDamageEffect(1)));
        cards.Add(CreateCreature("Mystic Scholar", 1, 3, CreateCardDrawEffect(1)));
        cards.Add(CreateCreature("Venomous Spider", 2, 2, CreatePoisonEffect(1)));
        cards.Add(CreateCreature("Armored Guardian", 2, 5));
        cards.Add(CreateCreature("Soul Harvester", 4, 3, CreateLifeStealEffect(1)));
        cards.Add(CreateCreature("Mischievous Imp", 1, 1, CreateRandomDamageEffect(2)));
        cards.Add(CreateCreature("Ancient Golem", 3, 6));
        cards.Add(CreateCreature("Spirit Healer", 2, 3, CreateHealEffect(1)));
        cards.Add(CreateCreature("Battle Strategist", 3, 3, CreateBuffAlliesEffect()));
        cards.Add(CreateCreature("Shadowmancer", 2, 4, CreateWeakenEnemiesEffect()));

        // Create spell cards
        cards.Add(CreateSpell("Fireball", "Deal 3 damage and draw a card", TargetType.AllCreatures));
        cards.Add(CreateSpell("Storm", "Deal 1 damage to all enemies and heal friendlies", TargetType.EnemyCreatures));
        cards.Add(CreateSpell("Insight", "Draw 2 cards and heal for 1", TargetType.Player));
        cards.Add(CreateSpell("Mystic Barrier", "Gain 2 health and draw a card", TargetType.Player));
        cards.Add(CreateSpell("Shadow Strike", "Deal 2 damage to a creature and 1 to enemy player", TargetType.EnemyCreatures));

        Log($"Created {cards.Count} test cards: {cards.Count(c => c is CreatureData)} creatures and {cards.Count(c => c is SpellData)} spells",
            LogTag.Cards | LogTag.Initialization);
        return cards;
    }

    private CreatureData CreateCreature(string name, int attack, int health, CardEffect effect = null) {
        var creature = ScriptableObject.CreateInstance<CreatureData>();
        creature.cardName = name;
        creature.attack = attack;
        creature.health = health;

        // Add description based on creature attributes
        string description = $"{attack}/{health}";
        if (effect != null) {
            description += " - " + GetEffectDescription(effect);
            creature.effects.Add(effect);
        }
        creature.description = description;

        return creature;
    }

    private SpellData CreateSpell(string name, string description, TargetType defaultTarget) {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.cardName = name;
        spell.description = description;
        spell.defaultTargetType = defaultTarget;
        spell.cardType = CardType.Spell;

        return spell;
    }

    private string GetEffectDescription(CardEffect effect) {
        switch (effect.trigger) {
            case EffectTrigger.OnDamage:
                if (effect.actions.Any(a => a.actionType == ActionType.Damage && a.value > 0))
                    return "Retaliates when damaged";
                return "Triggers when damaged";

            case EffectTrigger.OnPlay:
                if (effect.actions.Any(a => a.actionType == ActionType.Draw))
                    return "Draws a card when played";
                if (effect.actions.Any(a => a.actionType == ActionType.Heal))
                    return "Heals when played";
                if (effect.actions.Any(a => a.actionType == ActionType.Damage))
                    return "Deals damage when played";
                return "Triggers when played";

            case EffectTrigger.OnDeath:
                return "Triggers when destroyed";

            case EffectTrigger.StartOfTurn:
                return "Triggers at start of turn";

            case EffectTrigger.EndOfTurn:
                return "Triggers at end of turn";

            default:
                return "Has special abilities";
        }
    }

    private CardEffect CreateRetaliateDamageEffect(int damageAmount) {
        return new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnDamage,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = damageAmount,
                    targetType = TargetType.Enemy // Targets the attacker
                }
            }
        };
    }

    private CardEffect CreateCardDrawEffect(int cards) {
        return new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Draw,
                    value = cards,
                    targetType = TargetType.Player // Draws for the player who owns this
                }
            }
        };
    }

    private CardEffect CreatePoisonEffect(int damagePerTurn) {
        return new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.EndOfTurn,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = damagePerTurn,
                    targetType = TargetType.EnemyCreatures // Damages enemy creatures
                }
            }
        };
    }

    private CardEffect CreateLifeStealEffect(int healAmount) {
        return new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnDamage,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Heal,
                    value = healAmount,
                    targetType = TargetType.Player // Heals the player who owns this
                }
            }
        };
    }

    private CardEffect CreateRandomDamageEffect(int damageAmount) {
        return new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = damageAmount,
                    targetType = TargetType.EnemyCreatures // Damages a random enemy creature
                }
            }
        };
    }

    private CardEffect CreateHealEffect(int healAmount) {
        return new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Heal,
                    value = healAmount,
                    targetType = TargetType.FriendlyCreatures // Heals friendly creatures
                }
            }
        };
    }

    private CardEffect CreateBuffAlliesEffect() {
        // Note: This doesn't actually buff allies in the current system,
        // but simulates it with a draw effect
        return new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Draw,
                    value = 1,
                    targetType = TargetType.Player
                }
            }
        };
    }

    private CardEffect CreateWeakenEnemiesEffect() {
        // Note: This doesn't actually weaken enemies in the current system,
        // but simulates it with a damage effect
        return new CardEffect {
            effectType = EffectType.Triggered,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = 1,
                    targetType = TargetType.EnemyCreatures
                }
            }
        };
    }
}