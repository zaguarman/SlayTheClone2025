using static DebugLogger;
using static Enums;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Interface remains the same
// public interface ITurnManager { ... }

// TurnManager depends on GameManager for access to other systems
public class TurnManager : MonoBehaviour, ITurnManager {
    // Singleton pattern removed
    // Implement IsInitialized property
    public bool IsInitialized { get; private set; }

    #region Fields & Properties
    private int turnNumber = 0;
    private IGameManager _gameManager; // Store as interface
    private IGameMediator _gameMediator; // Store as interface
    private IModifierManager _modifierManager; // Store as interface
    private IActionsQueue _actionsQueue; // Store as interface

    public int TurnNumber => turnNumber;
    #endregion

    #region Unity Lifecycle

    // No Start() method - Initialization driven by GameBootstrap calling Initialize
    public void Initialize(IGameManager gameManager, IGameMediator mediator)
    {
        if (gameManager == null || mediator == null)
        {
            LogError("TurnManager initialization failed: GameManager or GameMediator is null.", LogTag.Initialization | LogTag.Turns);
            enabled = false;
            return;
        }
        _gameManager = gameManager;
        _gameMediator = mediator;
        // Get other systems from GameManager *after* it's initialized
        _modifierManager = gameManager.ModifierManager;
        _actionsQueue = gameManager.ActionsQueue;

        if (_modifierManager == null || _actionsQueue == null)
        {
             LogError("TurnManager initialization failed: ModifierManager or ActionsQueue not ready via GameManager.", LogTag.Initialization | LogTag.Turns);
             enabled = false;
            return;
        }

        turnNumber = 0; // Reset turn number on initialization
        IsInitialized = true; // Mark as initialized
        Log("TurnManager Initialized with dependencies.", LogTag.Initialization | LogTag.Turns);
    }
    #endregion

    #region Methods
    public void EndTurn() {
        // --- Pre-checks ---
         if (_gameManager == null || _gameMediator == null || _modifierManager == null || _actionsQueue == null)
         {
             LogError("TurnManager.EndTurn called before fully initialized or dependencies lost!", LogTag.Turns | LogTag.Initialization);
             // Don't attempt re-initialization - this should be handled by GameBootstrap
             return; // Exit if not initialized
         }

        int endedTurn = turnNumber; // Store the turn number that just ended
        turnNumber++; // Increment turn number for the new turn
        Log($"--- Ending Turn {endedTurn}, Starting Turn {turnNumber} ---", LogTag.Turns);

        // --- Phase 1: End of Turn Processing (for the turn that just finished) ---
        Log($"[End of Turn {endedTurn}] Processing...", LogTag.Turns);
        // 1a. Process End-of-Turn Effects & Modifier Expirations (handled by ModifierManager)
        _modifierManager.ProcessEndOfTurn(endedTurn, _actionsQueue);
        // 1b. Resolve any actions queued by End-of-Turn effects/expirations
        Log($"[End of Turn {endedTurn}] Resolving actions queued by EOT effects...", LogTag.Turns | LogTag.Actions);
        _actionsQueue.ResolveActions();

        // --- Phase 2: Turn Transition & Mandatory Actions ---
        Log($"[Turn {turnNumber} Start] Queuing mandatory discard/draw actions...", LogTag.Turns | LogTag.Actions | LogTag.Cards);
        // 2a. Queue Discard & Draw Actions
        _gameManager.DiscardHand(_gameManager.Player1);
        _gameManager.DiscardHand(_gameManager.Player2);
        _gameManager.DrawCardsForPlayer(_gameManager.Player1, _gameManager.Player1.CardsToDraw);
        _gameManager.DrawCardsForPlayer(_gameManager.Player2, _gameManager.Player2.CardsToDraw);
        // 2b. Resolve the discard/draw actions immediately
        Log($"[Turn {turnNumber} Start] Resolving discard/draw actions...", LogTag.Turns | LogTag.Actions);
        _actionsQueue.ResolveActions();

        // --- Mediator Notification ---
        // Notify AFTER mandatory actions, BEFORE Start-of-Turn effects
        Log($"[Turn {turnNumber} Start] Notifying Mediator TurnEnded ({endedTurn})", LogTag.Turns);
        _gameMediator.NotifyTurnEnded(endedTurn); // Notify that the PREVIOUS turn number has ENDED

        // --- Phase 3: Start of Turn Processing (for the new turn) ---
        Log($"[Turn {turnNumber} Start] Processing Start-of-Turn effects...", LogTag.Turns | LogTag.Effects);
        // 3a. Process Start-of-Turn Effects (handled by ModifierManager)
        _modifierManager.ProcessStartOfTurn(turnNumber, _actionsQueue);
        // 3b. Resolve actions queued by Start-of-Turn effects
        Log($"[Turn {turnNumber} Start] Resolving actions queued by SOT effects...", LogTag.Turns | LogTag.Actions);
        _actionsQueue.ResolveActions();

        // --- Phase 4: Finalize ---
        // 4a. Notify UI/Game State Changed
        Log($"[Turn {turnNumber} Start] Notifying Game State Changed.", LogTag.Turns);
        _gameMediator.NotifyGameStateChanged();
        // 4b. Final Log
        Log($"--- Turn {turnNumber} Started ---", LogTag.Turns);
    }
    // REMOVED TriggerEndOfTurnEffects and TriggerStartOfTurnEffects - Responsibility moved to ModifierManager
    // REMOVED LogBattlefieldState, HasEffectWithTrigger, ReduceStunDuration - Not needed here anymore
    #endregion
}
