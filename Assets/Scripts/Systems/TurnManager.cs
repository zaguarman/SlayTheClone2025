using static DebugLogger;
using Enums;
using UnityEngine;
using System.Linq; // Add for ToList() extension method

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
        Log("TurnManager initialized", LogTag.Initialization | LogTag.Turns);
    }

    public void Initialize(GameManager gameManager, GameMediator gameMediator) {
        this.gameManager = gameManager;
        this.gameMediator = gameMediator;
        turnNumber = 0;
        Log("TurnManager initialized with explicit references", LogTag.Initialization | LogTag.Turns);
    }
    #endregion

    #region Methods
    public void EndTurn() {
        int previousTurn = turnNumber;
        turnNumber++;
        Log($"Ending turn {previousTurn}, starting turn {turnNumber}", LogTag.Turns);

        // 1. End of Previous Turn Effects
        Log("Step 1: Triggering END of turn effects for turn " + previousTurn, LogTag.Turns);
        TriggerTurnPhaseEffects(EffectTrigger.EndOfTurn);
        if (gameManager != null && gameManager.ActionsQueue != null) {
            gameManager.ActionsQueue.ResolveActions(); // Resolve effects
            Log("End of turn effects resolved", LogTag.Turns | LogTag.Effects);
        }

        // 2. Discard Phase
        Log("Step 2: Queuing Discard Actions for turn " + previousTurn, LogTag.Turns);
        if (gameManager != null && gameManager.ActionsQueue != null) {
            if (gameManager.Player1 != null) gameManager.ActionsQueue.AddAction(new DiscardHandAction(gameManager.Player1));
            if (gameManager.Player2 != null) gameManager.ActionsQueue.AddAction(new DiscardHandAction(gameManager.Player2));
            gameManager.ActionsQueue.ResolveActions(); // Resolve discards
            Log("Discard phase completed", LogTag.Turns | LogTag.Cards);
        }

        // 3. Draw Phase
        Log("Step 3: Queuing Draw Actions for turn " + turnNumber, LogTag.Turns);
        if (gameManager != null && gameManager.ActionsQueue != null) {
            if (gameManager.Player1 != null) gameManager.ActionsQueue.AddAction(new DrawCardsAction(gameManager.Player1, gameManager.Player1.CardsToDraw));
            if (gameManager.Player2 != null) gameManager.ActionsQueue.AddAction(new DrawCardsAction(gameManager.Player2, gameManager.Player2.CardsToDraw));
            gameManager.ActionsQueue.ResolveActions(); // Resolve draws
            Log("Draw phase completed", LogTag.Turns | LogTag.Cards);
        }

        // 4. Modifier Ticks
        Log("Step 4: Notifying Modifier Tick (End of Turn " + previousTurn + ")", LogTag.Turns | LogTag.Effects);
        if (gameMediator != null) {
            gameMediator.NotifyTurnEnded(previousTurn);
            Log("Modifier tick notifications sent", LogTag.Turns | LogTag.Effects);
        }

        // 5. Start of New Turn Effects
        Log("Step 5: Triggering START of turn effects for turn " + turnNumber, LogTag.Turns);
        TriggerTurnPhaseEffects(EffectTrigger.StartOfTurn);
        if (gameManager != null && gameManager.ActionsQueue != null) {
            gameManager.ActionsQueue.ResolveActions(); // Resolve effects
            Log("Start of turn effects resolved", LogTag.Turns | LogTag.Effects);
        }

        // 6. Notify UI/Game State
        Log("Step 6: Notifying game state changed (Turn ended)", LogTag.Turns);
        if (gameMediator != null) {
            gameMediator.NotifyGameStateChanged();
        }

        Log($"Turn {turnNumber} started.", LogTag.Turns);
        LogBattlefieldState(gameManager.Player1, "Player 1");
        LogBattlefieldState(gameManager.Player2, "Player 2");
    }
    // Consolidated method for triggering turn-phase effects
    private void TriggerTurnPhaseEffects(EffectTrigger trigger) {
         Log($"Triggering {trigger} effects", LogTag.Effects | LogTag.Turns);
         // Iterate through players and their battlefields
         if (gameManager != null) {
             TriggerEffectsForPlayer(gameManager.Player1, trigger);
             TriggerEffectsForPlayer(gameManager.Player2, trigger);
         }

         // TODO: Add logic for effects on players themselves or battlefield slots if needed
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

        // Use ToList() to avoid issues if effects modify the collection during iteration
        foreach (var slot in player.Battlefield.ToList()) {
            if (slot.IsOccupied() && slot.OccupyingCreature is Creature creature) {
                // Creature handles its own effects check now
                creature.HandleEffect(trigger, gameManager.ActionsQueue);
            }
             // TODO: Add check for effects on the slot itself via slot.ModifierController if slots can have triggered effects
        }
        // TODO: Add check for effects on the player object itself if players are IModifiable
    }

    // Remove obsolete methods related to specific effect triggers
    // private void TriggerEndOfTurnEffects() { ... } // Removed
    // private void TriggerStartOfTurnEffects() { ... } // Removed

    // Remove obsolete methods related to checking/handling effects directly
    // private bool HasEffectWithTrigger(Creature creature, EffectTrigger trigger) { ... } // Removed (Creature handles this)

    // Remove obsolete stun logic - to be handled by Modifier system if needed
    // private void ReduceStunDurationForAllCreatures() { ... } // Removed
    #endregion
}
