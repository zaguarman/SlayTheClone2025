using static DebugLogger;
using static Enums;
using UnityEngine;

public class TurnManager : MonoBehaviour {
    #region Singleton
    private static TurnManager instance;
    public static TurnManager Instance {
        get {
            if (instance == null) {
                var go = new GameObject("TurnManager");
                instance = go.AddComponent<TurnManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Fields & Properties
    private int turnNumber = 0;
    private GameManager gameManager;
    private GameMediator gameMediator;
    public int TurnNumber => turnNumber;
    #endregion

    #region Unity Lifecycle
    private void Awake() {
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start() {
        gameManager = GameManager.Instance;
        gameMediator = GameMediator.Instance;
        turnNumber = 0;
    }
    #endregion

    #region Methods
    public void EndTurn() {
        turnNumber++;
        Log($"Ending turn {turnNumber - 1}, starting turn {turnNumber}", LogTag.Turns);

        // 1. Trigger end of turn effects for the previous turn
        Log("Step 1: Triggering end of turn effects", LogTag.Turns);
        TriggerEndOfTurnEffects();

        // 2. Let ActionsQueue handle discards and draws
        Log("Step 2: Resolving actions queue for discards and draws", LogTag.Turns);
        if (gameManager?.ActionsQueue != null) {
            gameManager.ActionsQueue.ResolveActions();
        }

        // 3. Trigger start of turn effects for the new turn
        Log("Step 3: Triggering start of turn effects", LogTag.Turns);
        TriggerStartOfTurnEffects();

        // 3b. Resolve any actions queued by start of turn effects
        Log("Step 3b: Resolving actions queued by start of turn effects", LogTag.Turns);
        if (gameManager?.ActionsQueue != null) {
            gameManager.ActionsQueue.ResolveActions();
        }

        // 4. Notify UI and other systems of turn change
        Log("Step 4: Notifying game state changed", LogTag.Turns);
        gameMediator?.NotifyGameStateChanged();

        // Log the final battlefield state after all effects
        Log("Final battlefield state after turn change:", LogTag.Turns);
        LogBattlefieldState(gameManager.Player1, "Player 1");
        LogBattlefieldState(gameManager.Player2, "Player 2");
    }
    private void TriggerEndOfTurnEffects() {
        Log("Triggering end of turn effects", LogTag.Effects | LogTag.Turns);
        TriggerEffectsForPlayer(gameManager.Player1, EffectTrigger.EndOfTurn);
        TriggerEffectsForPlayer(gameManager.Player2, EffectTrigger.EndOfTurn);

        // Reduce stun duration for all creatures at the end of turn
        ReduceStunDurationForAllCreatures();
    }

    private void TriggerStartOfTurnEffects() {
        Log("Triggering start of turn effects", LogTag.Effects | LogTag.Turns);

        // Log the current state of the battlefield before triggering effects
        LogBattlefieldState(gameManager.Player1, "Player 1");
        LogBattlefieldState(gameManager.Player2, "Player 2");

        TriggerEffectsForPlayer(gameManager.Player1, EffectTrigger.StartOfTurn);
        TriggerEffectsForPlayer(gameManager.Player2, EffectTrigger.StartOfTurn);

        // Note: We don't resolve the actions queue here anymore.
        // It's now handled in the EndTurn method right after this method is called.
    }

    private void LogBattlefieldState(IPlayer player, string playerName) {
        if (player == null) return;

        Log($"Battlefield state for {playerName}:", LogTag.Creatures | LogTag.Turns);
        foreach (var slot in player.Battlefield) {
            if (slot.IsOccupied() && slot.OccupyingCreature != null) {
                var creature = slot.OccupyingCreature;
                Log($"  Slot {player.Battlefield.IndexOf(slot) + 1}: {creature.Name}, Health: {creature.Health}/{(creature as Creature)?.BaseHealth}", LogTag.Creatures | LogTag.Turns);
            } else {
                Log($"  Slot {player.Battlefield.IndexOf(slot) + 1}: Empty", LogTag.Creatures | LogTag.Turns);
            }
        }
    }

    private void TriggerEffectsForPlayer(IPlayer player, EffectTrigger trigger) {
        if (player == null) return;

        foreach (var slot in player.Battlefield) {
            if (slot.IsOccupied() && slot.OccupyingCreature != null) {
                // Handle the effect if the creature has one with this trigger
                var creature = slot.OccupyingCreature as Creature;
                if (creature != null && HasEffectWithTrigger(creature, trigger)) {
                    Log($"Processing {trigger} effects for {creature.Name}", LogTag.Effects | LogTag.Turns);
                    creature.HandleEffect(trigger, gameManager.ActionsQueue);
                }
            }
        }
    }

    private bool HasEffectWithTrigger(Creature creature, EffectTrigger trigger) {
        if (creature?.Effects == null) return false;

        foreach (var effect in creature.Effects) {
            if (effect.trigger == trigger) {
                return true;
            }
        }
        return false;
    }

    private void ReduceStunDurationForAllCreatures() {
        Log("Reducing stun duration for all creatures", LogTag.Creatures | LogTag.Effects | LogTag.Turns);
        // to be implemented in the future, ignore for now
    }
    #endregion
}
