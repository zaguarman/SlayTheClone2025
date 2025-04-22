using static DebugLogger;
using static Enums;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TurnManager : MonoBehaviour, ITurnManager {
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
         // Ensure GameManager instance is available before proceeding
        if (gameManager == null)
        {
            gameManager = GameManager.Instance; // Try to get it again
            if (gameManager == null)
            {
                LogError("GameManager instance is null in TurnManager.EndTurn! Cannot proceed.", LogTag.Turns);
                return;
            }
        }
         if (gameMediator == null)
         {
            gameMediator = GameMediator.Instance; // Try to get it again
             if (gameMediator == null) {
                LogError("GameMediator instance is null in TurnManager.EndTurn! Cannot proceed.", LogTag.Turns);
                 return;
             }
         }

        turnNumber++; // Increment turn FIRST
        Log($"--- Ending Turn {turnNumber - 1}, Starting Turn {turnNumber} ---", LogTag.Turns);

        // 1. Process End-of-Turn Effects & Expirations
        Log($"Turn {turnNumber}: Processing End-of-Turn effects for Turn {turnNumber - 1}...", LogTag.Turns);
        TriggerEndOfTurnEffects(); // Handles creature effects AND ModifierManager processing

        // 2. Resolve Actions (Discard/Draw are queued by ActionsQueue.ResolveActions)
        Log($"Turn {turnNumber}: Resolving main action queue...", LogTag.Turns | LogTag.Actions);
        gameManager.ActionsQueue?.ResolveActions(); // Resolve actions including mandatory discard/draw

        // --- Mediator Notification ---
        // Notify AFTER EOT effects resolve, but BEFORE SOT effects trigger
        Log($"Turn {turnNumber}: Notifying Mediator TurnEnded ({turnNumber - 1})", LogTag.Turns);
        gameMediator?.NotifyTurnEnded(turnNumber - 1); // Notify that the previous turn number has ENDED

        // 3. Trigger Start-of-Turn Effects for the NEW turn
        Log($"Turn {turnNumber}: Processing Start-of-Turn effects for Turn {turnNumber}...", LogTag.Turns | LogTag.Effects);
        TriggerStartOfTurnEffects();

        // 4. Resolve Actions Queued by Start-of-Turn Effects
        Log($"Turn {turnNumber}: Resolving actions queued by Start-of-Turn effects...", LogTag.Turns | LogTag.Actions);
        gameManager.ActionsQueue?.ResolveActions();

        // 5. Notify UI/Game State Changed
        Log($"Turn {turnNumber}: Notifying Game State Changed.", LogTag.Turns);
        gameMediator?.NotifyGameStateChanged();

        // Log final state
        Log($"--- Turn {turnNumber} Started ---", LogTag.Turns);
        // LogBattlefieldState(gameManager.Player1, "Player 1"); // Logging can be verbose, optional
        // LogBattlefieldState(gameManager.Player2, "Player 2");
    }
    private void TriggerEndOfTurnEffects() {
        Log($"[Turn {turnNumber-1} End] Triggering EndOfTurn effects.", LogTag.Effects | LogTag.Turns);
        // Ensure gameManager reference is valid
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gameManager == null) { LogError("GameManager null in TriggerEndOfTurnEffects", LogTag.Turns); return; }

        TriggerEffectsForPlayer(gameManager.Player1, EffectTrigger.EndOfTurn);
        TriggerEffectsForPlayer(gameManager.Player2, EffectTrigger.EndOfTurn);

        // Process timed modifiers expiration - Pass the turn number that just ENDED
        gameManager.ModifierManager?.ProcessEndOfTurn(turnNumber - 1);

        // Reduce stun duration for all creatures at the end of turn
        // ReduceStunDurationForAllCreatures(); // Assuming ModifierManager handles this now
    }

     private void TriggerStartOfTurnEffects() {
         Log($"[Turn {turnNumber} Start] Triggering StartOfTurn effects.", LogTag.Effects | LogTag.Turns);
        // Ensure gameManager reference is valid
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gameManager == null) { LogError("GameManager null in TriggerStartOfTurnEffects", LogTag.Turns); return; }

         LogBattlefieldState(gameManager.Player1, "Player 1");
         LogBattlefieldState(gameManager.Player2, "Player 2");

         TriggerEffectsForPlayer(gameManager.Player1, EffectTrigger.StartOfTurn);
         TriggerEffectsForPlayer(gameManager.Player2, EffectTrigger.StartOfTurn);
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
         // Ensure gameManager reference is valid
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gameManager == null) { LogError("GameManager null in TriggerEffectsForPlayer", LogTag.Turns); return; }

         foreach (var slot in player.Battlefield) {
             if (slot.IsOccupied() && slot.OccupyingCreature != null) {
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
        return creature.Effects.Any(effect => effect.trigger == trigger);
     }

    private void ReduceStunDurationForAllCreatures() {
        Log("Reducing stun duration for all creatures", LogTag.Creatures | LogTag.Effects | LogTag.Turns);
        // to be implemented in the future, ignore for now
    }
    #endregion
}
