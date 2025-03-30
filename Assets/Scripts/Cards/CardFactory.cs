using static DebugLogger;
using System.Linq;
using System;
using UnityEngine.Events;
using UnityEngine;
using static Enums;

public static class CardFactory {
    public static ICard CreateCard(CardData cardData) {
        if (cardData == null) return null;

        Log($"Creating card: {cardData.cardName} with {cardData.effects.Count} effects", LogTag.Cards | LogTag.Initialization);

        ICard card = null;
        switch (cardData) {
            case CreatureData creatureData:
                var creature = new Creature(creatureData.cardName, creatureData.attack, creatureData.health);
                // Copy over the description
                creature.Description = creatureData.description;

                foreach (var effect in cardData.effects) {
                    var newEffect = new CardEffect {
                        effectType = effect.effectType,
                        trigger = effect.trigger,
                        actions = effect.actions.Select(a => new EffectAction {
                            actionType = a.actionType,
                            value = a.value,
                            targetType = a.targetType
                        }).ToList()
                    };
                    creature.Effects.Add(newEffect);
                }
                card = creature;
                break;

            case SpellData spellData:
                var spell = new Spell(spellData.cardName, spellData.defaultTargetType);
                // Copy over the description
                spell.Description = spellData.description;

                // Configure spell actions based on the spell data's effects
                ConfigureSpellActionsFromEffects(spell, spellData);

                card = spell;
                break;
        }

        return card;
    }

    private static void ConfigureSpellActionsFromEffects(Spell spell, SpellData spellData) {
        // If there are explicit effects defined, use those
        if (spellData.effects != null && spellData.effects.Count > 0) {
            foreach (var effect in spellData.effects) {
                foreach (var action in effect.actions) {
                    spell.AddAction(action.actionType, action.value, action.targetType);
                }
            }
        }
        // Otherwise fall back to simple draw action
        else {
            spell.AddAction(ActionType.Draw, 1, TargetType.Player);
        }
    }

    public static CardController CreateCardController(ICard card, IPlayer owner, Transform parent) {
        if (card == null || parent == null) return null;

        var cardPrefab = GameReferences.Instance.GetCardPrefab();
        if (cardPrefab == null) {
            LogError("Failed to get card prefab from game references", LogTag.Cards | LogTag.Initialization);
            return null;
        }

        var cardObj = GameObject.Instantiate(cardPrefab, parent);
        var controller = cardObj.GetComponent<CardController>();
        if (controller != null) {
            var data = CreateCardData(card);

            // Only cast to ICreature if the card is actually a creature
            ICreature creature = card as ICreature;

            controller.Setup(data, owner, creature);
            Log($"Created card controller for {card.Name}", LogTag.Cards | LogTag.Initialization);
        }
        return controller;
    }

    public static CardData CreateCardData(ICard card) {
        if (card == null) return null;

        // For creatures, copy ALL data including effects
        if (card is ICreature creature) {
            var creatureData = ScriptableObject.CreateInstance<CreatureData>();
            creatureData.cardName = creature.Name;
            creatureData.attack = creature.Attack;
            creatureData.health = creature.Health;
            creatureData.description = creature.Description;  // Copy description

            // Copy effects from the creature to the new data
            creatureData.effects = creature.Effects.Select(e => new CardEffect {
                effectType = e.effectType,
                trigger = e.trigger,
                actions = e.actions.Select(a => new EffectAction {
                    actionType = a.actionType,
                    value = a.value,
                    targetType = a.targetType
                }).ToList()
            }).ToList();

            return creatureData;
        }

        // For spells, create SpellData
        if (card is Spell spell) {
            var spellData = ScriptableObject.CreateInstance<SpellData>();
            spellData.cardName = spell.Name;
            spellData.defaultTargetType = spell.DefaultTargetType;
            spellData.description = spell.Description;  // Copy description

            return spellData;
        }

        return null;
    }

    // TODO
    public static void SetupCardEventHandlers(
        CardController controller,
        UnityAction<CardController> onBeginDrag = null,
        UnityAction<CardController> onEndDrag = null,
        UnityAction<CardController> onDrop = null,
        Action onPointerEnter = null,
        Action onPointerExit = null) {
        if (controller == null) return;

        CleanupCardEventHandlers(controller);

        if (onBeginDrag != null)
            controller.OnBeginDragEvent.AddListener(onBeginDrag);

        if (onEndDrag != null)
            controller.OnEndDragEvent.AddListener(onEndDrag);

        if (onDrop != null)
            controller.OnCardDropped.AddListener(onDrop);

        if (onPointerEnter != null)
            controller.OnPointerEnterHandler += onPointerEnter;

        if (onPointerExit != null)
            controller.OnPointerExitHandler += onPointerExit;

        Log($"Set up event handlers for card {controller.name}", LogTag.Cards | LogTag.UI);
    }

    public static void CleanupCardEventHandlers(CardController controller) {
        if (controller == null) return;

        controller.OnBeginDragEvent.RemoveAllListeners();
        controller.OnEndDragEvent.RemoveAllListeners();
        controller.OnCardDropped.RemoveAllListeners();
        controller.OnPointerEnterHandler = null;
        controller.OnPointerExitHandler = null;

        Log($"Cleaned up event handlers for card {controller.name}", LogTag.Cards | LogTag.UI);
    }
}