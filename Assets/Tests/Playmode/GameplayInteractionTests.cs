using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Linq;
using System.Collections.Generic; // For List

public class GameplayInteractionTests {
    private GameManager _gameManager;
    private TurnManager _turnManager;
    private ModifierManager _modifierManager;
    private IActionsQueue _actionsQueue;
    private IGameReferences _gameReferences;
    private IPlayer _player1;
    private IPlayer _player2;

    // Helper to get a specific occupied slot for a player
    private BattlefieldSlot GetOccupiedSlot(IPlayer player, int slotIndex) {
        if (player == null || player.Battlefield == null || slotIndex < 0 || slotIndex >= player.Battlefield.Count) {
            Assert.Fail($"Invalid parameters for GetOccupiedSlot: Player null? {player == null}, Index: {slotIndex}");
            return null;
        }
        var slot = player.Battlefield[slotIndex];
        if (!slot.IsOccupied()) {
            Assert.Fail($"Slot {slotIndex} for Player {(player.IsPlayer1 ? "1" : "2")} is not occupied.");
        }
        Assert.IsNotNull(slot.OccupyingCreature, $"Creature in Slot {slotIndex} is null despite being occupied.");
        return slot;
    }

    // Helper to get the first available empty slot for a player
    private BattlefieldSlot GetFirstEmptySlot(IPlayer player) {
        var emptySlot = player?.Battlefield?.FirstOrDefault(s => !s.IsOccupied());
        Assert.IsNotNull(emptySlot, $"No empty slot found for Player {(player == null ? "NULL" : (player.IsPlayer1 ? "1" : "2"))}. Ensure scene setup places creatures correctly.");
        return emptySlot;
    }

    // Use the helper for setup
    [UnitySetUp]
    public IEnumerator Setup() {
        // Use a lambda to capture the references into our member variables
        yield return TestSetupHelper.SetupSceneAndWait((gm, med, refs, tm, mm) => {
            _gameManager = gm;
            _turnManager = tm;
            _modifierManager = mm;
            _actionsQueue = gm.ActionsQueue;
            _gameReferences = refs;
            _player1 = gm.Player1;
            _player2 = gm.Player2;

            // Basic validation after setup
            Assert.IsNotNull(_player1, "Player 1 is null after setup.");
            Assert.IsNotNull(_player2, "Player 2 is null after setup.");
            Assert.IsTrue(_player1.Battlefield.Any(s => s.IsOccupied()), "Player 1 has no creatures after setup.");
            Assert.IsTrue(_player2.Battlefield.Any(s => s.IsOccupied()), "Player 2 has no creatures after setup.");
        });
    }

    [TearDown]
    public void Teardown() {
        // Cleanup is generally handled by scene reload in Play Mode tests
        _gameManager = null;
        _turnManager = null;
        _modifierManager = null;
        _actionsQueue = null;
        _gameReferences = null;
        _player1 = null;
        _player2 = null;
    }

    // --- Combat Tests ---

    [UnityTest]
    public IEnumerator Combat_CreatureAttacksCreature_DealsDamage() {
        // Arrange
        BattlefieldSlot attackerSlot = GetOccupiedSlot(_player1, 0); // P1 Creature 0
        BattlefieldSlot defenderSlot = GetOccupiedSlot(_player2, 0); // P2 Creature 0
        ICreature attacker = attackerSlot.OccupyingCreature;
        ICreature defender = defenderSlot.OccupyingCreature;
        int attackerAttack = attacker.Attack;
        int defenderInitialHealth = defender.Health;

        // Act: Queue the combat action
        _actionsQueue.AddAction(new BattlefieldCombatAction(attacker, defenderSlot));
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount(), "BattlefieldCombatAction not queued.");
        yield return null;

        // Act: Resolve the BattlefieldCombatAction (which queues DamageCreatureAction)
        _actionsQueue.ResolveActions();
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount(), "DamageCreatureAction not queued after resolving combat.");
        var damageAction = _actionsQueue.GetPendingActions().First() as DamageCreatureAction;
        Assert.IsNotNull(damageAction, "Queued action is not DamageCreatureAction.");
        Assert.AreEqual(defender, damageAction.GetTarget(), "Damage action target mismatch.");
        Assert.AreEqual(attackerAttack, damageAction.GetDamage(), "Damage action amount mismatch.");
        yield return null;

        // Act: Resolve the DamageCreatureAction
        _actionsQueue.ResolveActions();
        Assert.AreEqual(0, _actionsQueue.GetPendingActionsCount(), "Actions queue not empty after damage resolve.");
        yield return null;

        // Assert: Defender health reduced
        int expectedHealth = defenderInitialHealth - attackerAttack;
        // Clamp expected health to 0 if damage exceeds health
        expectedHealth = Mathf.Max(0, expectedHealth);
        Assert.AreEqual(expectedHealth, defender.Health, $"Defender health incorrect. Initial: {defenderInitialHealth}, Attack: {attackerAttack}");
    }

    [UnityTest]
    public IEnumerator Combat_CreatureAttacksEmptySlot_DealsDamageToPlayer() {
        // Arrange
        BattlefieldSlot attackerSlot = GetOccupiedSlot(_player1, 0); // P1 Creature 0
        BattlefieldSlot emptyDefenderSlot = GetFirstEmptySlot(_player2); // Find an empty P2 slot
        ICreature attacker = attackerSlot.OccupyingCreature;
        int attackerAttack = attacker.Attack;
        int player2InitialHealth = _player2.Health;

        // Act: Queue the combat action targeting the empty slot
        _actionsQueue.AddAction(new BattlefieldCombatAction(attacker, emptyDefenderSlot));
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount(), "BattlefieldCombatAction not queued.");
        yield return null;

        // Act: Resolve the BattlefieldCombatAction (queues DamagePlayerAction)
        _actionsQueue.ResolveActions();
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount(), "DamagePlayerAction not queued after resolving combat.");
        var damagePlayerAction = _actionsQueue.GetPendingActions().First() as DamagePlayerAction;
        Assert.IsNotNull(damagePlayerAction, "Queued action is not DamagePlayerAction.");
        Assert.AreEqual(_player2, damagePlayerAction.GetTargetPlayer(), "Damage action target mismatch.");
        Assert.AreEqual(attackerAttack, damagePlayerAction.GetDamage(), "Damage action amount mismatch.");
        yield return null;

        // Act: Resolve the DamagePlayerAction
        _actionsQueue.ResolveActions();
        Assert.AreEqual(0, _actionsQueue.GetPendingActionsCount(), "Actions queue not empty after player damage resolve.");
        yield return null;

        // Assert: Player 2 health reduced
        int expectedHealth = player2InitialHealth - attackerAttack;
        expectedHealth = Mathf.Max(0, expectedHealth); // Clamp health
        Assert.AreEqual(expectedHealth, _player2.Health, $"Player 2 health incorrect. Initial: {player2InitialHealth}, Attack: {attackerAttack}");
    }

    // --- Movement Tests ---

    [UnityTest]
    public IEnumerator Movement_MoveCreatureToEmptySlot_ChangesPosition() {
        // Arrange
        BattlefieldSlot startSlot = GetOccupiedSlot(_player1, 0);
        BattlefieldSlot endSlot = GetFirstEmptySlot(_player1); // Move to an empty slot on the same side
        ICreature creatureToMove = startSlot.OccupyingCreature;

        // Act: Queue move action
        _actionsQueue.AddAction(new MoveCreatureAction(creatureToMove, startSlot, endSlot, _player1));
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Act: Resolve move action
        _actionsQueue.ResolveActions();
        Assert.AreEqual(0, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Assert
        Assert.IsFalse(startSlot.IsOccupied(), "Start slot is still occupied.");
        Assert.IsTrue(endSlot.IsOccupied(), "End slot is not occupied.");
        Assert.AreSame(creatureToMove, endSlot.OccupyingCreature, "Creature is not in the end slot.");
        Assert.AreSame(endSlot, creatureToMove.Slot, "Creature's internal slot reference not updated.");
    }

    [UnityTest]
    public IEnumerator Movement_MoveCreatureToOccupiedSlot_SwapsPositions() {
        // Arrange
        BattlefieldSlot startSlot = GetOccupiedSlot(_player1, 0); // Creature C1 in Slot S0
        BattlefieldSlot targetSlot = GetOccupiedSlot(_player1, 1); // Creature C2 in Slot S1
        ICreature creatureToMove = startSlot.OccupyingCreature; // C1
        ICreature creatureInTargetSlot = targetSlot.OccupyingCreature; // C2

        // Act: Queue move action (C1 -> S1)
        _actionsQueue.AddAction(new MoveCreatureAction(creatureToMove, startSlot, targetSlot, _player1));
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Act: Resolve move action (Executor should handle the swap)
        _actionsQueue.ResolveActions();
        Assert.AreEqual(0, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Assert: Creatures have swapped places
        Assert.IsTrue(startSlot.IsOccupied(), "Start slot (S0) is unexpectedly empty.");
        Assert.IsTrue(targetSlot.IsOccupied(), "Target slot (S1) is unexpectedly empty.");
        Assert.AreSame(creatureInTargetSlot, startSlot.OccupyingCreature, "Creature C2 is not in the start slot (S0).");
        Assert.AreSame(creatureToMove, targetSlot.OccupyingCreature, "Creature C1 is not in the target slot (S1).");
        Assert.AreSame(startSlot, creatureInTargetSlot.Slot, "Creature C2's internal slot reference incorrect.");
        Assert.AreSame(targetSlot, creatureToMove.Slot, "Creature C1's internal slot reference incorrect.");
    }

    [UnityTest]
    public IEnumerator Movement_SwapCreaturesAction_SwapsPositions() {
        // Arrange
        BattlefieldSlot slot1 = GetOccupiedSlot(_player1, 0); // C1 in S0
        BattlefieldSlot slot2 = GetOccupiedSlot(_player1, 1); // C2 in S1
        ICreature creature1 = slot1.OccupyingCreature; // C1
        ICreature creature2 = slot2.OccupyingCreature; // C2

        // Act: Queue the specific SwapCreaturesAction
        _actionsQueue.AddAction(new SwapCreaturesAction(creature1, creature2, slot1, slot2));
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Act: Resolve swap action
        _actionsQueue.ResolveActions();
        Assert.AreEqual(0, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Assert: Creatures have swapped places
        Assert.IsTrue(slot1.IsOccupied(), "Slot S0 is unexpectedly empty.");
        Assert.IsTrue(slot2.IsOccupied(), "Slot S1 is unexpectedly empty.");
        Assert.AreSame(creature2, slot1.OccupyingCreature, "Creature C2 is not in slot S0.");
        Assert.AreSame(creature1, slot2.OccupyingCreature, "Creature C1 is not in slot S1.");
        Assert.AreSame(slot1, creature2.Slot, "Creature C2's internal slot reference incorrect.");
        Assert.AreSame(slot2, creature1.Slot, "Creature C1's internal slot reference incorrect.");
    }

    // --- Card Drawing Tests ---

    [UnityTest]
    public IEnumerator CardDrawing_DrawCard_IncreasesHandCount() {
        // Arrange
        int initialHandCount = _player1.Hand.Count;
        // Ensure player can draw (not full hand)
        if (initialHandCount >= Player.MAX_HAND_SIZE) {
            _player1.DiscardHand(); // Discard to make space if needed
            _actionsQueue.ResolveActions(); // Resolve discard
            yield return null;
            initialHandCount = 0;
            Assert.AreEqual(0, _player1.Hand.Count, "Hand discard failed before draw test.");
        }
        Assert.IsTrue(_gameManager.CardDealingService.CanDrawCard(_player1), "Player cannot draw card at start of test.");

        // Act: Queue draw action
        _actionsQueue.AddAction(new DrawCardsAction(_player1, 1));
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Act: Resolve draw action
        _actionsQueue.ResolveActions();
        Assert.AreEqual(0, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Assert
        Assert.AreEqual(initialHandCount + 1, _player1.Hand.Count, "Hand count did not increase by 1.");
    }

    [UnityTest]
    public IEnumerator CardDrawing_DrawCardWithFullHand_DoesNotIncreaseHandCount() {
        // Arrange: Force player hand to be full
        while (_player1.Hand.Count < Player.MAX_HAND_SIZE) {
            _gameManager.CardDealingService.DrawCardForPlayer(_player1); // Use direct draw for setup
            yield return null; // Allow UI updates if needed, though not strictly necessary here
        }
        Assert.AreEqual(Player.MAX_HAND_SIZE, _player1.Hand.Count, "Setup failed: Hand is not full.");
        int initialHandCount = _player1.Hand.Count;

        // Act: Queue draw action
        _actionsQueue.AddAction(new DrawCardsAction(_player1, 1));
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Act: Resolve draw action
        _actionsQueue.ResolveActions();
        Assert.AreEqual(0, _actionsQueue.GetPendingActionsCount());
        yield return null;

        // Assert
        Assert.AreEqual(initialHandCount, _player1.Hand.Count, "Hand count changed despite being full.");
    }
}
