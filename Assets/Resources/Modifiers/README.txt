Modifiers Folder

This folder contains ModifierData ScriptableObject assets that define different types of modifiers
that can be applied to creatures, battlefield slots, or other IModifiable objects.

To create a new modifier:
1. In Unity, right-click in the Project window > Create > Game Data > Modifier Data
2. Configure the modifier's properties:
   - Display Name: Human-readable name shown in UI
   - Description: Tooltip text explaining the modifier's effect
   - Icon: Visual representation for UI
   - Duration Type: Permanent or TurnBased
   - Base Duration: Number of turns the modifier lasts (if TurnBased)
   - Stacking Type: How multiple applications of the same modifier behave
   - Max Stacks: Maximum number of stacks allowed (if Stackable)
   - Stat Adjustments: List of stats affected and by how much
   - Tags: Optional categorization (e.g., "Buff", "Debuff", "Poison")

Example modifiers to create:
- Strength: +2 Attack, Permanent
- Weakness: -1 Attack, 2 turns
- Regeneration: +1 Health at start of turn, 3 turns
- Poison: -1 Health at end of turn, 2 turns, Stackable
- Armor: +2 Health, Permanent

To apply modifiers in code, use the ApplyModifierAction:
```csharp
// Apply a modifier to a creature
var modifierData = GameReferences.Instance.ModifierFactory.GetModifierData("strength");
actionsQueue.AddAction(new ApplyModifierAction(targetCreature, modifierData, sourceCreature));

// Or directly from an EffectAction in a card effect:
// Set actionType = ActionType.ApplyModifier
// Assign modifierToApply = [drag ModifierData asset here]
```
