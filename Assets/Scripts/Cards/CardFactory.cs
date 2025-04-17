using static DebugLogger;
using System.Linq;
using System;
using UnityEngine.Events;
using UnityEngine;
using Enums;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.UI; // Needed for Button prefab type

// Changed from static to instance class
public class CardFactory {
    public ICard CreateCard(CardData cardData) {
        if (cardData == null) return null;

        Log($"Creating card: {cardData.cardName} with {cardData.effects.Count} effects", LogTag.Cards | LogTag.Initialization);

        ICard card = null;
        switch (cardData) {
            case CreatureData creatureData:
                var creature = new Creature(
                    creatureData.cardName,
                    creatureData.attack,
                    creatureData.health,
                    creatureData.cardId); // Pass cardId

                // Copy over the description
                creature.Description = creatureData.description;

                foreach (var effect in cardData.effects) {
                    var newEffect = new CardEffect {
                        effectType = effect.effectType,
                        trigger = effect.trigger,
                        actions = effect.actions.Select(a => new EffectAction {
                            actionType = a.actionType,
                            value = a.value,
                            targetType = a.targetType,
                            targetModifier = a.targetModifier,
                            modifierToApply = a.modifierToApply
                        }).ToList()
                    };
                    creature.Effects.Add(newEffect);
                }
                card = creature;
                break;

            case SpellData spellData:
                var spell = new Spell(
                    spellData.cardName,
                    spellData.defaultTargetType,
                    spellData.cardId); // Pass cardId

                // Copy over the description
                spell.Description = spellData.description;

                // Configure spell actions based on the spell data's effects
                ConfigureSpellActionsFromEffects(spell, spellData);

                card = spell;
                break;
        }

        return card;
    }

    private void ConfigureSpellActionsFromEffects(Spell spell, SpellData spellData) {
        // If there are explicit effects defined, use those
        if (spellData.effects != null && spellData.effects.Count > 0) {
            foreach (var effect in spellData.effects) {
                var newEffect = new CardEffect {
                    effectType = effect.effectType,
                    trigger = effect.trigger
                };

                foreach (var action in effect.actions) {
                    var newAction = new EffectAction {
                        actionType = action.actionType,
                        value = action.value,
                        targetType = action.targetType,
                        targetModifier = action.targetModifier,
                        modifierToApply = action.modifierToApply
                    };
                    newEffect.actions.Add(newAction);

                    // Also add the action to the spell's action list for direct execution
                    spell.AddAction(action.actionType, action.value, action.targetType, action.targetModifier);
                }

                spell.Effects.Add(newEffect);
            }
        }
        // Otherwise fall back to simple draw action
        else {
            var defaultEffect = new CardEffect {
                effectType = EffectType.Immediate,
                trigger = EffectTrigger.OnPlay,
                actions = new List<EffectAction> {
                    new EffectAction {
                        actionType = ActionType.Draw,
                        value = 1,
                        targetType = TargetType.Player
                    }
                }
            };
            spell.Effects.Add(defaultEffect);
        }
    }

    // Method remains largely the same, but is now an instance method
    public async Task<CardController> CreateCardControllerAsync(ICard card, IPlayer owner, Transform parent, CancellationToken cancellationToken = default) {
        if (card == null || parent == null) return null;

        Button cardPrefabButton = GameReferences.Instance.GetCardPrefab(); // Assuming GetCardPrefab returns Button
        if (cardPrefabButton == null) {
            LogError("Failed to get card prefab from game references", LogTag.Cards | LogTag.Initialization);
            return null;
        }

        // Add small delay to spread out card creation
        await Task.Delay(100, cancellationToken);

        var cardObj = GameObject.Instantiate(cardPrefabButton.gameObject, parent); // Instantiate the GameObject of the Button
        var controller = cardObj.GetComponent<CardController>();
        if (controller != null) {
            // CardData creation is now handled elsewhere (e.g., DeckViewUI) if needed for display
            // We just need the ICard reference here.
            var data = CreateCardDataForDisplay(card); // Assume this helper exists if needed, or pass CardData directly

            // Only cast to ICreature if the card is actually a creature
            ICreature creature = card as ICreature;

            controller.Setup(data, owner, creature);
            Log($"Created card controller for {card.Name}", LogTag.Cards | LogTag.Initialization);
        }
        return controller;
    }

    // Method remains largely the same, but is now an instance method
    public CardController CreateCardController(ICard card, IPlayer owner, Transform parent) {
        if (card == null || parent == null || GameReferences.Instance == null) return null;

        Button cardPrefabButton = GameReferences.Instance.GetCardPrefab(); // Assuming GetCardPrefab returns Button
        if (cardPrefabButton == null) {
            LogError("Failed to get card prefab from game references", LogTag.Cards | LogTag.Initialization);
            return null;
        }

        var cardObj = GameObject.Instantiate(cardPrefabButton.gameObject, parent); // Instantiate the GameObject of the Button
        var controller = cardObj.GetComponent<CardController>();
        if (controller != null) { // CardData creation is now handled elsewhere (e.g., DeckViewUI) if needed for display
            var data = CreateCardDataForDisplay(card); // Use the helper to get display data

            // Only cast to ICreature if the card is actually a creature
            ICreature creature = card as ICreature;

            controller.Setup(data, owner, creature);
            Log($"Created card controller for {card.Name}", LogTag.Cards | LogTag.Initialization);
        }
        return controller;
    }

    // This method converts runtime ICard back to a temporary CardData structure FOR DISPLAY PURPOSES.
    // Moved to DeckViewUI as it's the primary user. Renamed for clarity.
    private CardData CreateCardDataForDisplay(ICard card) {
        if (card == null) return null;

        // For creatures, copy ALL data including effects
        if (card is ICreature creature) {
            var creatureData = ScriptableObject.CreateInstance<CreatureData>();
            creatureData.cardId = card.CardId; // Copy the cardId
            creatureData.cardName = creature.Name;
            creatureData.attack = creature.Attack;
            creatureData.health = creature.Health;
            creatureData.description = creature.Description;  // Copy description
            creatureData.effects = creature.Effects.Select(e => new CardEffect {
                effectType = e.effectType,
                trigger = e.trigger,
                actions = e.actions.Select(a => new EffectAction {
                    actionType = a.actionType,
                    value = a.value,
                    targetType = a.targetType,
                    targetModifier = a.targetModifier,
                    modifierToApply = a.modifierToApply
                }).ToList()
            }).ToList();
            return creatureData;
        }

        // For spells, copy the data and effects
        if (card is Spell spell) {
            var spellData = ScriptableObject.CreateInstance<SpellData>();
            spellData.cardId = card.CardId;
            spellData.cardName = spell.Name;
            spellData.description = spell.Description;
            spellData.defaultTargetType = spell.DefaultTargetType;
            spellData.effects = spell.Effects.Select(e => new CardEffect {
                effectType = e.effectType,
                trigger = e.trigger,
                actions = e.actions.Select(a => new EffectAction {
                    actionType = a.actionType,
                    value = a.value,
                    targetType = a.targetType,
                    targetModifier = a.targetModifier,
                    modifierToApply = a.modifierToApply
                }).ToList()
            }).ToList();
            return spellData;
        }

        return null;
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