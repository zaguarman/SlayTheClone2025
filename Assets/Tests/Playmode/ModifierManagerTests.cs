using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Linq;
using static Enums;
using System;
using System.Collections.Generic;
using UnityEngine.Events; // For UnityAction

public class ModifierManagerTests
{
    private GameManager _gameManager;
    private TurnManager _turnManager;
    private ModifierManager _modifierManager;
    private IModifierFactory _factory;
    private IActionsQueue _actionsQueue;
    private Creature _testCreatureP1;
    private Creature _testCreatureP2;

    // Use the helper for setup
    [UnitySetUp]
    public IEnumerator Setup()
    {
        yield return TestSetupHelper.SetupSceneAndWait((gm, med, refs, tm, mm) =>
        {
            _gameManager = gm;
            _turnManager = tm;
            _modifierManager = mm;
            _factory = mm.ModifierFactory; // Get factory from manager
            _actionsQueue = gm.ActionsQueue; // Get actions queue
            _testCreatureP1 = TestSetupHelper.GetFirstAvailableCreature(gm.Player1, mm);
            _testCreatureP2 = TestSetupHelper.GetFirstAvailableCreature(gm.Player2, mm);
        });
    }

    [TearDown]
    public void Teardown()
    {
        // Cleanup logic if needed, often handled by scene reload
        _gameManager = null;
        _turnManager = null;
        _modifierManager = null;
        _factory = null;
        _actionsQueue = null;
        _testCreatureP1 = null;
        _testCreatureP2 = null;
    }

    // --- Existing Tests (Adapted) ---

    [UnityTest]
    public IEnumerator ModifierManager_ApplyFlatAttackModifier_IncreasesAttack()
    {
        // Arrange
        int initialAttack = _testCreatureP1.Attack;
        int modifierValue = 5;
        IModifier attackMod = _factory.CreateStatModifier("Test Attack Buff", "+5 Attack", ModifiableStat.Attack, ModifierCalculationType.Flat, modifierValue);

        // Act
        _modifierManager.ApplyModifier(_testCreatureP1, attackMod);
        yield return null;

        // Assert
        Assert.AreEqual(initialAttack + modifierValue, _testCreatureP1.Attack, $"Initial: {initialAttack}");
        Assert.IsTrue(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == attackMod.Id));
    }

    [UnityTest]
    public IEnumerator ModifierManager_ApplyTimedHealthModifier_ExpiresAfterTurns()
    {
        // Arrange
        int initialMaxHealth = _testCreatureP1.MaxHealth;
        int modifierValue = 10;
        int duration = 2;
        int startTurn = _turnManager.TurnNumber;
        IModifier timedHealthMod = _factory.CreateTimedStatModifier(
            "Test Timed Health Buff", "+10 Max Health (2 Turns)", ModifiableStat.Health, ModifierCalculationType.Flat, modifierValue, duration, startTurn
        );

        // Act & Assert Apply
        _modifierManager.ApplyModifier(_testCreatureP1, timedHealthMod);
        yield return null;
        Assert.AreEqual(initialMaxHealth + modifierValue, _testCreatureP1.MaxHealth);
        Assert.IsTrue(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == timedHealthMod.Id));

        // Act & Assert Turns Before Expiry
        for (int i = 0; i < duration - 1; i++)
        {
            _turnManager.EndTurn();
            yield return null; // Wait for ProcessEndOfTurn
            Assert.IsTrue(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == timedHealthMod.Id), $"Expired after {i + 1} turn(s).");
            Assert.AreEqual(initialMaxHealth + modifierValue, _testCreatureP1.MaxHealth);
        }

        // Act & Assert Expiry
        _turnManager.EndTurn();
        yield return null; // Wait for ProcessEndOfTurn
        Assert.IsFalse(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == timedHealthMod.Id));
        Assert.AreEqual(initialMaxHealth, _testCreatureP1.MaxHealth);
    }

    [UnityTest]
    public IEnumerator ModifierManager_ApplyParalyzeStatus_PreventsActionsAndExpires()
    {
        // Arrange
        int duration = 1;
        int startTurn = _turnManager.TurnNumber;
        IModifier paralyzeMod = _factory.CreateStatusEffectModifier(
            "Test Paralyze", "Paralyzed (1 Turn)", StatusEffectType.Paralyzed, duration, 0, startTurn
        );

        // Act & Assert Apply
        _modifierManager.ApplyModifier(_testCreatureP1, paralyzeMod);
        yield return null;
        Assert.IsTrue(_modifierManager.HasStatusEffect(_testCreatureP1, StatusEffectType.Paralyzed));
        Assert.IsTrue(_modifierManager.AreActionsPrevented(_testCreatureP1));
        Assert.IsTrue(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == paralyzeMod.Id));

        // Act & Assert Expiry
        _turnManager.EndTurn();
        yield return null; // Wait for ProcessEndOfTurn
        Assert.IsFalse(_modifierManager.HasStatusEffect(_testCreatureP1, StatusEffectType.Paralyzed));
        Assert.IsFalse(_modifierManager.AreActionsPrevented(_testCreatureP1));
        Assert.IsFalse(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == paralyzeMod.Id));
    }

    // --- NEW TESTS ---

    [UnityTest]
    public IEnumerator ModifierManager_ApplyPercentageAttackModifier_CalculatesCorrectly()
    {
        // Arrange
        int baseAttack = _testCreatureP1.BaseAttack; // Use base for reliable calculation start
        int percentageIncrease = 50; // +50%
        IModifier percentAttackMod = _factory.CreateStatModifier(
            "Test Percent Attack Buff", "+50% Attack", ModifiableStat.Attack, ModifierCalculationType.Percentage, percentageIncrease
        );
        int expectedAttack = (int)Math.Round(baseAttack * (1.0f + (percentageIncrease / 100.0f)));

        // Act
        _modifierManager.ApplyModifier(_testCreatureP1, percentAttackMod);
        yield return null;

        // Assert
        Assert.AreEqual(expectedAttack, _testCreatureP1.Attack, $"Base: {baseAttack}, Expected: {expectedAttack}");
        Assert.IsTrue(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == percentAttackMod.Id));
    }

    [UnityTest]
    public IEnumerator ModifierManager_ExplicitlyRemoveModifier_RevertsStat()
    {
        // Arrange
        int initialSpeed = _testCreatureP1.Speed;
        int modifierValue = -2;
        int duration = 5;
        int startTurn = _turnManager.TurnNumber;
        IModifier timedSpeedMod = _factory.CreateTimedStatModifier(
            "Test Slow Debuff", "-2 Speed (5 Turns)", ModifiableStat.Speed, ModifierCalculationType.Flat, modifierValue, duration, startTurn
        );

        // Act & Assert Apply
        _modifierManager.ApplyModifier(_testCreatureP1, timedSpeedMod);
        yield return null;
        int expectedSpeedAfterApply = Math.Max(0, initialSpeed + modifierValue);
        Assert.AreEqual(expectedSpeedAfterApply, _testCreatureP1.Speed);
        Assert.IsTrue(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == timedSpeedMod.Id));

        // Act: Explicit Remove
        _modifierManager.RemoveModifier(_testCreatureP1, timedSpeedMod);
        yield return null;

        // Assert: Removed Correctly
        Assert.IsFalse(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == timedSpeedMod.Id));
        Assert.AreEqual(initialSpeed, _testCreatureP1.Speed);
    }

    [UnityTest]
    public IEnumerator ModifierManager_StackFlatAndPercentageAttack_CalculatesCorrectly()
    {
        // Arrange
        int baseAttack = _testCreatureP1.BaseAttack;
        int flatValue = 5;
        int percentValue = 20; // +20%
        IModifier flatMod = _factory.CreateStatModifier("Flat Att +5", "+5 Attack", ModifiableStat.Attack, ModifierCalculationType.Flat, flatValue);
        IModifier percMod = _factory.CreateStatModifier("Perc Att +20%", "+20% Attack", ModifiableStat.Attack, ModifierCalculationType.Percentage, percentValue);
        // Expected: (Base + Flat) * Percentage
        int expectedAttack = (int)Math.Round((baseAttack + flatValue) * (1.0f + (percentValue / 100.0f)));

        // Act
        _modifierManager.ApplyModifier(_testCreatureP1, flatMod);
        _modifierManager.ApplyModifier(_testCreatureP1, percMod);
        yield return null; // Allow recalculation

        // Assert
        Assert.AreEqual(expectedAttack, _testCreatureP1.Attack, $"Base: {baseAttack}, Expected: {expectedAttack}");
        Assert.IsTrue(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == flatMod.Id));
        Assert.IsTrue(_modifierManager.HasModifier(_testCreatureP1, m => m.Id == percMod.Id));
    }

    [UnityTest]
    public IEnumerator ModifierManager_ApplyBurnStatus_DealsDamageAtEndOfTurnViaQueue()
    {
        // Arrange
        int initialHealth = _testCreatureP1.Health;
        int burnPotency = 3;
        int duration = 1;
        int startTurn = _turnManager.TurnNumber;
        IModifier burnMod = _factory.CreateStatusEffectModifier(
            "Test Burn", "Burn (3 Dmg/Turn)", StatusEffectType.Burned, duration, burnPotency, startTurn
        );

        // Act: Apply Modifier
        _modifierManager.ApplyModifier(_testCreatureP1, burnMod);
        yield return null;
        Assert.IsTrue(_modifierManager.HasStatusEffect(_testCreatureP1, StatusEffectType.Burned));

        // Act: End Turn (Burn should trigger and queue damage)
        _turnManager.EndTurn();
        yield return null; // Wait for EOT processing

        // Assert: Damage action should be in the queue
        // Note: Burn damage action might not be added yet if it relies on mediator->actionqueue flow
        // Instead, let's resolve the queue and check the health *afterwards*.
        Assert.AreEqual(1, _actionsQueue.GetPendingActionsCount(), "Expected 1 action (Burn Damage) in queue after EndTurn.");
        var queuedAction = _actionsQueue.GetPendingActions().First();
        Assert.IsInstanceOf<DamageCreatureAction>(queuedAction);
        var damageAction = queuedAction as DamageCreatureAction;
        Assert.AreEqual(_testCreatureP1, damageAction.GetTarget());
        Assert.AreEqual(burnPotency, damageAction.GetDamage());

        // Act: Resolve Actions
        _actionsQueue.ResolveActions();
        yield return null;

        // Assert: Health reduced
        Assert.AreEqual(initialHealth - burnPotency, _testCreatureP1.Health, "Creature health not reduced correctly by Burn.");

        // Act: End another turn (modifier should expire)
        _turnManager.EndTurn();
        yield return null;

        // Assert: Modifier expired
        Assert.IsFalse(_modifierManager.HasStatusEffect(_testCreatureP1, StatusEffectType.Burned));
        int healthAfterExpiry = _testCreatureP1.Health;

        // Act: Resolve actions again (should be empty for burn)
         _actionsQueue.ResolveActions();
        yield return null;

        // Assert: Health unchanged after expiry turn
        Assert.AreEqual(healthAfterExpiry, _testCreatureP1.Health, "Health changed after Burn expired.");
    }

}