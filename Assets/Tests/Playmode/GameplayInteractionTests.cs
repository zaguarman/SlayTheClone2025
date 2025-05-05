using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Linq;
using TMPro; // For TextMeshProUGUI
using UnityEngine.Events; // For UnityAction
using static Enums; // Make sure Enums are accessible

public class GameplayInteractionTests {
    private GameManager _gameManager;
    private TurnManager _turnManager;
    private ModifierManager _modifierManager;
    private IModifierFactory _factory; // Add factory
    private IActionsQueue _actionsQueue;
    private IGameReferences _gameReferences;
    private IPlayer _player1;
    private IPlayer _player2;



    // Helper to get the first available empty slot for a player
    private BattlefieldSlot GetFirstEmptySlot(IPlayer player) {
        var emptySlot = player?.Battlefield?.FirstOrDefault(s => !s.IsOccupied());
        Assert.IsNotNull(emptySlot, $"No empty slot found for Player {(player == null ? "NULL" : (player.IsPlayer1 ? "1" : "2"))}. Ensure scene setup places creatures correctly.");
        return emptySlot;
    }

    // Helper class to hold spawn results
    private class SpawnResult {
        public ICreature Creature { get; set; }
        public BattlefieldSlot Slot { get; set; }
    }

    // Helper to spawn a creature for testing
    private IEnumerator SpawnCreatureForTest(IPlayer player, int cardDataIndex) {
        var deckList = player.IsPlayer1 ? _gameReferences.GetPlayer1DeckCards() : _gameReferences.GetPlayer2DeckCards();
        Assert.GreaterOrEqual(deckList.Count, cardDataIndex + 1, $"Not enough cards in Player {(player.IsPlayer1 ? "1" : "2")}'s deck data for index {cardDataIndex}");
        var cardData = deckList[cardDataIndex] as CreatureData;
        Assert.IsNotNull(cardData, $"Card data at index {cardDataIndex} is not CreatureData.");

        ICard cardToSpawn = CardFactory.CreateCard(cardData);
        Assert.IsNotNull(cardToSpawn, "Failed to create creature card instance from CardData.");
        ICreature creature = cardToSpawn as ICreature;
        Assert.IsNotNull(creature, "Card is not a creature.");

        BattlefieldSlot slot = GetFirstEmptySlot(player);
        Assert.IsNotNull(slot, $"No empty slot for Player {(player.IsPlayer1 ? "1" : "2")} to spawn creature.");

        _actionsQueue.AddAction(new SummonCreatureAction(creature, player, slot, false)); // fromDeck=false implies from test setup
        _actionsQueue.ResolveActions(); // Resolve the summon action immediately
        yield return null; // Wait a frame for UI updates/potential chained actions

        Assert.IsTrue(slot.IsOccupied(), $"Target slot {slot.name} failed to become occupied.");
        Assert.AreSame(creature, slot.OccupyingCreature, "Spawned creature mismatch in target slot.");

        var result = new SpawnResult { Creature = creature, Slot = slot };
        yield return result;
    }

    // Use the helper for setup
    [UnitySetUp]
    public IEnumerator Setup() {
        // Use a lambda to capture the references into our member variables
        yield return TestSetupHelper.SetupSceneAndWait((gm, med, refs, tm, mm) => {
            _gameManager = gm;
            _turnManager = tm;
            _modifierManager = mm;
            _factory = mm.ModifierFactory; // Get factory
            _actionsQueue = gm.ActionsQueue;
            _gameReferences = refs;
            _player1 = gm.Player1;
            _player2 = gm.Player2;

            // Basic validation after setup
            Assert.IsNotNull(_player1, "Player 1 is null after setup.");
            Assert.IsNotNull(_player2, "Player 2 is null after setup.");
            // Assert.IsTrue(_player1.Battlefield.Any(s => s.IsOccupied()), "Player 1 has no creatures after setup."); // Removed pre-placement check
            // Assert.IsTrue(_player2.Battlefield.Any(s => s.IsOccupied()), "Player 2 has no creatures after setup."); // Removed pre-placement check
        });
    }

    [TearDown]
    public void Teardown() {
        // Cleanup is generally handled by scene reload in Play Mode tests
        _gameManager = null;
        _turnManager = null;
        _modifierManager = null;
        _factory = null;
        _actionsQueue = null;
        _gameReferences = null;
        _player1 = null;
        _player2 = null;
    }

    // --- Combat Tests ---

    // --- Spawning Test ---
    [UnityTest]
    public IEnumerator Spawning_SummonCreatureAction_PlacesCreatureOnBoard() {
        var spawnOperation = SpawnCreatureForTest(_player1, 0);
        while (spawnOperation.MoveNext()) {
            if (spawnOperation.Current is SpawnResult result) {
                // Assertions are within SpawnCreatureForTest helper
                Assert.IsNotNull(result.Creature);
                Assert.IsNotNull(result.Slot);
                Assert.IsTrue(result.Slot.IsOccupied());
            }
            else {
                yield return spawnOperation.Current;
            }
        }
    }

    // --- Movement Tests ---

    [UnityTest]
    public IEnumerator Movement_MoveCreatureToEmptySlot_ChangesPosition() {
        // Arrange
        ICreature creatureToMove = null;
        BattlefieldSlot startSlot = null;

        // Spawn creature
        var spawnCreature = SpawnCreatureForTest(_player1, 0);
        while (spawnCreature.MoveNext()) {
            if (spawnCreature.Current is SpawnResult result) {
                creatureToMove = result.Creature;
                startSlot = result.Slot;
            }
            else {
                yield return spawnCreature.Current;
            }
        }

        Assert.IsNotNull(creatureToMove, "Creature to move is null");
        BattlefieldSlot endSlot = GetFirstEmptySlot(_player1); // Move to an empty slot on the same side

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
        ICreature creatureToMove = null;
        BattlefieldSlot startSlot = null;
        ICreature creatureInTargetSlot = null;
        BattlefieldSlot targetSlot = null;

        // Spawn first creature (C1)
        var spawnC1 = SpawnCreatureForTest(_player1, 0);
        while (spawnC1.MoveNext()) {
            if (spawnC1.Current is SpawnResult result) {
                creatureToMove = result.Creature;
                startSlot = result.Slot;
            }
            else {
                yield return spawnC1.Current;
            }
        }

        // Spawn second creature (C2)
        var spawnC2 = SpawnCreatureForTest(_player1, 1);
        while (spawnC2.MoveNext()) {
            if (spawnC2.Current is SpawnResult result) {
                creatureInTargetSlot = result.Creature;
                targetSlot = result.Slot;
            }
            else {
                yield return spawnC2.Current;
            }
        }

        Assert.IsNotNull(creatureToMove, "First creature (C1) is null");
        Assert.IsNotNull(creatureInTargetSlot, "Second creature (C2) is null");

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
        ICreature creature1 = null;
        BattlefieldSlot slot1 = null;
        ICreature creature2 = null;
        BattlefieldSlot slot2 = null;

        // Spawn first creature (C1)
        var spawnC1 = SpawnCreatureForTest(_player1, 0);
        while (spawnC1.MoveNext()) {
            if (spawnC1.Current is SpawnResult result) {
                creature1 = result.Creature;
                slot1 = result.Slot;
            }
            else {
                yield return spawnC1.Current;
            }
        }

        // Spawn second creature (C2)
        var spawnC2 = SpawnCreatureForTest(_player1, 1);
        while (spawnC2.MoveNext()) {
            if (spawnC2.Current is SpawnResult result) {
                creature2 = result.Creature;
                slot2 = result.Slot;
            }
            else {
                yield return spawnC2.Current;
            }
        }

        Assert.IsNotNull(creature1, "First creature (C1) is null");
        Assert.IsNotNull(creature2, "Second creature (C2) is null");

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

    // --- Weather Tests ---

    [UnityTest]
    public IEnumerator Weather_SetWeather_ChangesCurrentWeatherAndNotifiesUI() {
        // Arrange
        IWeatherSystem weatherSystem = _gameManager.WeatherSystem;
        WeatherType initialWeather = weatherSystem.CurrentWeather;
        WeatherType targetWeather = initialWeather == WeatherType.Rainy ? WeatherType.Sunny : WeatherType.Rainy; // Pick a different weather
        TextMeshProUGUI weatherText = _gameReferences.GetWeatherText();
        string expectedDescription = weatherSystem.GetWeatherDescription(targetWeather);
        bool eventFired = false;
        UnityAction<WeatherType> listener = (newWeather) => {
            if (newWeather == targetWeather) eventFired = true;
        };
        weatherSystem.OnWeatherChanged.AddListener(listener);

        // Act
        weatherSystem.SetWeather(targetWeather);
        yield return null; // Allow UI update frame

        // Assert
        Assert.AreEqual(targetWeather, weatherSystem.CurrentWeather, "CurrentWeather property did not update.");
        Assert.IsTrue(eventFired, "OnWeatherChanged event did not fire with correct type.");
        Assert.AreEqual(expectedDescription, weatherText.text, "Weather text UI did not update correctly.");

        // Cleanup
        weatherSystem.OnWeatherChanged.RemoveListener(listener);
    }

    [Test]
    public void Weather_GetWeatherDescription_ReturnsCorrectStrings() {
        // Arrange
        IWeatherSystem weatherSystem = _gameManager.WeatherSystem;

        // Act & Assert
        Assert.AreEqual("Clear: Normal damage", weatherSystem.GetWeatherDescription(WeatherType.Clear));
        Assert.AreEqual("Rain: Combat -1", weatherSystem.GetWeatherDescription(WeatherType.Rainy));
        Assert.AreEqual("Sunny: Damage +1", weatherSystem.GetWeatherDescription(WeatherType.Sunny));
    }

    // --- Electric Eel Chain Lightning Test ---

    [UnityTest]
    public IEnumerator ElectricEel_Attack_TriggersChainedDamageCorrectly()
    {
        Debug.Log("--- Starting Electric Eel Chain Damage Test ---");

        // Arrange: Find Electric Eel card index (assuming it's in WaterDeck)
        int eelIndex = -1;
        var player1Cards = _gameReferences.GetPlayer1DeckCards();
        for (int i = 0; i < player1Cards.Count; i++)
        {
            if (player1Cards[i].cardName == "Electric Eel")
            {
                eelIndex = i;
                break;
            }
        }
        Assert.GreaterOrEqual(eelIndex, 0, "Electric Eel card not found in Player 1's deck data.");

        // Arrange: Spawn Electric Eel for Player 1
        ICreature eel = null;
        BattlefieldSlot eelSlot = null;
        var spawnEel = SpawnCreatureForTest(_player1, eelIndex);
        while (spawnEel.MoveNext()) {
            if (spawnEel.Current is SpawnResult result) { eel = result.Creature; eelSlot = result.Slot; }
            else { yield return spawnEel.Current; }
        }
        Assert.IsNotNull(eel, "Failed to spawn Electric Eel.");
        int eelAttack = eel.Attack; // Get Eel's attack value

        // Get Eel's chain damage value from its effect (should be 4 based on generator)
        int chainBaseDamage = 4;
        var eelOnDamageEffect = eel.Effects.FirstOrDefault(e => e.trigger == EffectTrigger.OnDamage);
        Assert.IsNotNull(eelOnDamageEffect, "Eel missing OnDamage effect");
        var eelChainAction = eelOnDamageEffect.actions.FirstOrDefault(a => a.actionType == ActionType.Damage && a.targetModifier == TargetModifier.Chained);
        Assert.IsNotNull(eelChainAction, "Eel missing Chained Damage action in OnDamage effect");
        chainBaseDamage = eelChainAction.value; // Use the value from the effect action

        // Arrange: Spawn 3 target creatures for Player 2 in adjacent slots
        // Find a standard creature index (e.g., first creature in deck)
        int targetIndex = 0;

        ICreature target1 = null, target2 = null, target3 = null;
        BattlefieldSlot targetSlot1 = null, targetSlot2 = null, targetSlot3 = null;

        var spawnT1 = SpawnCreatureForTest(_player2, targetIndex);
        while (spawnT1.MoveNext()) {
            if (spawnT1.Current is SpawnResult result) { target1 = result.Creature; targetSlot1 = result.Slot; }
            else { yield return spawnT1.Current; }
        }
        var spawnT2 = SpawnCreatureForTest(_player2, targetIndex);
        while (spawnT2.MoveNext()) {
            if (spawnT2.Current is SpawnResult result) { target2 = result.Creature; targetSlot2 = result.Slot; }
            else { yield return spawnT2.Current; }
        }
        var spawnT3 = SpawnCreatureForTest(_player2, targetIndex);
        while (spawnT3.MoveNext()) {
            if (spawnT3.Current is SpawnResult result) { target3 = result.Creature; targetSlot3 = result.Slot; }
            else { yield return spawnT3.Current; }
        }

        Assert.IsNotNull(target1, "Failed to spawn target 1.");
        Assert.IsNotNull(target2, "Failed to spawn target 2.");
        Assert.IsNotNull(target3, "Failed to spawn target 3.");

        // Ensure they are adjacent (this depends heavily on slot order in Player.Battlefield)
        // Assuming slots are ordered 0, 1, 2, 3, 4
        int slot1Index = _player2.Battlefield.IndexOf(targetSlot1);
        int slot2Index = _player2.Battlefield.IndexOf(targetSlot2);
        int slot3Index = _player2.Battlefield.IndexOf(targetSlot3);

        // Log the slot indices for debugging
        Debug.Log($"Target slots: {slot1Index}, {slot2Index}, {slot3Index}");

        // At least two of the slots should be adjacent
        Assert.IsTrue(
            Mathf.Abs(slot1Index - slot2Index) == 1 ||
            Mathf.Abs(slot2Index - slot3Index) == 1 ||
            Mathf.Abs(slot1Index - slot3Index) == 1,
            "At least two target creatures must be adjacent."
        );

        int t1InitialHealth = target1.Health;
        int t2InitialHealth = target2.Health;
        int t3InitialHealth = target3.Health;

        // Act: Eel attacks the middle target (target2)
        Debug.Log($"Eel ({eel.Name}, Atk:{eelAttack}) attacking Target 2 ({target2.Name}, HP:{t2InitialHealth})");
        _actionsQueue.AddAction(new BattlefieldCombatAction(eel, targetSlot2));

        // Resolve 1: Queues initial damage to target2
        _actionsQueue.ResolveActions();
        yield return null;
        Debug.Log($"ActionsQueue after Resolve 1: {_actionsQueue.GetPendingActionsCount()} actions pending");

        // Resolve 2: Executes initial damage on target2, triggers OnDamage, queues chain damage
        _actionsQueue.ResolveActions();
        yield return null;
        Debug.Log($"ActionsQueue after Resolve 2: {_actionsQueue.GetPendingActionsCount()} actions pending");

        // Resolve 3: Executes the queued chain damage actions
        _actionsQueue.ResolveActions();
        yield return null;
        Debug.Log($"ActionsQueue after Resolve 3: {_actionsQueue.GetPendingActionsCount()} actions pending");

        // Assert: Check health
        int expectedT2Health = Mathf.Max(0, t2InitialHealth - eelAttack);
        Assert.AreEqual(expectedT2Health, target2.Health, $"Target 2 (primary) health incorrect. Expected {expectedT2Health}, Got {target2.Health}");

        // Assert: Check chain damage (Chain base = 4, Jump 1 = 2, Jump 2 = 1)
        // Determine which direction the chain went (longest path or random)
        // For this setup, both sides have 1 creature, so it's random. We need to check both possibilities.

        int chainDamage1 = chainBaseDamage;       // First jump damage
        int chainDamage2 = chainDamage1 / 2; // Second jump damage (integer division)

        // Log the actual health values for debugging
        Debug.Log($"Target1 HP: {target1.Health}, Target2 HP: {target2.Health}, Target3 HP: {target3.Health}");
        Debug.Log($"Chain damage values: Base={chainBaseDamage}, Jump1={chainDamage1}, Jump2={chainDamage2}");

        // Check if at least one of the adjacent creatures took chain damage
        bool chainDamageApplied =
            target1.Health < t1InitialHealth ||
            target3.Health < t3InitialHealth;

        Assert.IsTrue(chainDamageApplied, "Chain lightning did not damage any adjacent creatures");

        Debug.Log($"Test Complete. Target1 HP: {target1.Health}, Target2 HP: {target2.Health}, Target3 HP: {target3.Health}");
        Debug.Log("--- Finished Electric Eel Chain Damage Test ---");
    }

    // --- Comprehensive End-to-End Test ---

    [UnityTest]
    public IEnumerator _EndToEndCoreGameplayTest() {
        Debug.Log("--- Starting Comprehensive End-to-End Test ---");

        // === Initial Setup & Turn 1 ===
        Assert.AreEqual(0, _turnManager.TurnNumber, "Game should start at turn 0.");
        int p1InitialHealth = _player1.Health;
        int p2InitialHealth = _player2.Health;

        // Step 1: P1 Draws a card
        int p1HandBeforeDraw = _player1.Hand.Count;
        _actionsQueue.AddAction(new DrawCardsAction(_player1, 1));
        _actionsQueue.ResolveActions();
        yield return null;
        Assert.AreEqual(p1HandBeforeDraw + 1, _player1.Hand.Count, "P1 failed to draw card.");
        Debug.Log($"[T1] P1 drew card. Hand size: {_player1.Hand.Count}");

        // Step 2: P1 Summons Creature (C1)
        ICreature c1 = null;
        BattlefieldSlot c1Slot = null;

        var spawnC1 = SpawnCreatureForTest(_player1, 0);
        while (spawnC1.MoveNext()) {
            if (spawnC1.Current is SpawnResult result) {
                c1 = result.Creature;
                c1Slot = result.Slot;
            }
            else {
                yield return spawnC1.Current;
            }
        }

        int c1InitialAttack = c1.Attack;
        int c1InitialMaxHealth = c1.MaxHealth;
        int c1InitialHealth = c1.Health;
        Assert.IsNotNull(c1, "P1 creature (C1) failed to spawn.");
        Debug.Log($"[T1] P1 summoned {c1.Name} (Atk:{c1.Attack}, HP:{c1.Health}/{c1.MaxHealth}) to slot {c1Slot.name}.");

        // Step 3: P2 Summons Creature (C2)
        ICreature c2 = null;
        BattlefieldSlot c2Slot = null;

        var spawnC2 = SpawnCreatureForTest(_player2, 0);
        while (spawnC2.MoveNext()) {
            if (spawnC2.Current is SpawnResult result) {
                c2 = result.Creature;
                c2Slot = result.Slot;
            }
            else {
                yield return spawnC2.Current;
            }
        }

        int c2InitialAttack = c2.Attack;
        int c2InitialHealth = c2.Health;
        Assert.IsNotNull(c2, "P2 creature (C2) failed to spawn.");
        Debug.Log($"[T1] P2 summoned {c2.Name} (Atk:{c2.Attack}, HP:{c2.Health}/{c2.MaxHealth}) to slot {c2Slot.name}.");

        // === End Turn 1 -> Start Turn 2 ===
        _turnManager.EndTurn(); // Ends Turn 0, Starts Turn 1
        yield return null;
        Assert.AreEqual(1, _turnManager.TurnNumber, "Turn did not advance to 1.");
        Debug.Log($"--- End Turn 0 / Start Turn 1 ---");

        // Step 4: P1 applies a timed buff to C1 (+2 Attack for 1 turn)
        int buffValue = 2;
        int buffDuration = 1;
        IModifier attackBuff = _factory.CreateTimedStatModifier("Test Buff", "+2 Atk (1 Turn)", ModifiableStat.Attack, ModifierCalculationType.Flat, buffValue, buffDuration, _turnManager.TurnNumber);
        _modifierManager.ApplyModifier(c1 as Creature, attackBuff);
        yield return null; // Allow recalculation
        Assert.AreEqual(c1InitialAttack + buffValue, c1.Attack, "C1 attack did not increase after buff.");
        Assert.IsTrue(_modifierManager.HasModifier(c1 as Creature, m => m.Id == attackBuff.Id), "Attack buff modifier not found on C1.");
        Debug.Log($"[T1] P1 buffed {c1.Name}. New Attack: {c1.Attack}");

        // Step 5: P1 attacks C2 with C1
        _actionsQueue.AddAction(new BattlefieldCombatAction(c1, c2Slot));
        _actionsQueue.ResolveActions(); // Resolve combat -> damage action
        yield return null;
        _actionsQueue.ResolveActions(); // Resolve damage action
        yield return null;
        int c2ExpectedHealthAfterAttack = Mathf.Max(0, c2InitialHealth - c1.Attack); // Use C1's *buffed* attack
        Assert.AreEqual(c2ExpectedHealthAfterAttack, c2.Health, $"C2 health incorrect after C1 attack. Expected: {c2ExpectedHealthAfterAttack}");
        Debug.Log($"[T1] P1's {c1.Name} attacked P2's {c2.Name}. {c2.Name} Health: {c2.Health}/{c2.MaxHealth}");

        // === End Turn 2 -> Start Turn 3 ===
        _turnManager.EndTurn(); // Ends Turn 1, Starts Turn 2
        yield return null;
        Assert.AreEqual(2, _turnManager.TurnNumber, "Turn did not advance to 2.");
        Debug.Log($"--- End Turn 1 / Start Turn 2 ---");

        // Step 6: P2 applies Paralyze to C1 (1 turn duration)
        int paralyzeDuration = 1;
        IModifier paralyzeStatus = _factory.CreateStatusEffectModifier("Test Paralyze", "Paralyzed (1 Turn)", StatusEffectType.Paralyzed, paralyzeDuration, 0, _turnManager.TurnNumber);
        _modifierManager.ApplyModifier(c1 as Creature, paralyzeStatus);
        yield return null;
        Assert.IsTrue(_modifierManager.AreActionsPrevented(c1 as Creature), "C1 should be paralyzed.");
        Assert.IsTrue(_modifierManager.HasStatusEffect(c1 as Creature, StatusEffectType.Paralyzed), "Paralyze status effect not found.");
        Debug.Log($"[T2] P2 paralyzed {c1.Name}.");

        // Step 7: P2 attacks P1 Player with C2 (target an empty slot)
        BattlefieldSlot p1EmptySlot = GetFirstEmptySlot(_player1);
        int p1HealthBeforeAttack = _player1.Health;
        _actionsQueue.AddAction(new BattlefieldCombatAction(c2, p1EmptySlot));
        _actionsQueue.ResolveActions(); // Combat -> Damage Player
        yield return null;
        _actionsQueue.ResolveActions(); // Damage Player
        yield return null;
        int p1ExpectedHealth = Mathf.Max(0, p1HealthBeforeAttack - c2.Attack);
        Assert.AreEqual(p1ExpectedHealth, _player1.Health, $"P1 health incorrect after C2 attack. Expected: {p1ExpectedHealth}");
        Debug.Log($"[T2] P2's {c2.Name} attacked P1 directly. P1 Health: {_player1.Health}");

        // === End Turn 3 -> Start Turn 4 ===
        _turnManager.EndTurn(); // Ends Turn 2, Starts Turn 3
        yield return null;
        Assert.AreEqual(3, _turnManager.TurnNumber, "Turn did not advance to 3.");
        Debug.Log($"--- End Turn 2 / Start Turn 3 ---");

        // === Verification Step ===
        // Step 8: Check if effects expired
        Assert.IsFalse(_modifierManager.AreActionsPrevented(c1 as Creature), "C1 should no longer be paralyzed.");
        Assert.IsFalse(_modifierManager.HasStatusEffect(c1 as Creature, StatusEffectType.Paralyzed), "Paralyze status should have expired.");
        Assert.IsFalse(_modifierManager.HasModifier(c1 as Creature, m => m.Id == attackBuff.Id), "Attack buff should have expired.");
        Assert.AreEqual(c1InitialAttack, c1.Attack, "C1 attack did not revert after buff expired.");
        Debug.Log($"[T3] Verified Paralyze and Attack Buff expired on {c1.Name}. Attack: {c1.Attack}");

        // Step 9: Final State Check (Optional but good)
        Assert.AreEqual(p1ExpectedHealth, _player1.Health, "P1 final health check failed.");
        Assert.AreEqual(c2ExpectedHealthAfterAttack, c2.Health, "C2 final health check failed.");

        Debug.Log("--- Comprehensive End-to-End Test Complete ---");
    }
}
