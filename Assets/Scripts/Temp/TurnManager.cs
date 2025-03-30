using static DebugLogger;
using static Enums;
using UnityEngine;

public class TurnManager : MonoBehaviour {
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

    private int turnNumber = 0;
    private GameManager gameManager;
    private GameMediator gameMediator;

    public int TurnNumber => turnNumber;

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

    public void EndTurn() {
        turnNumber++;
        Log($"Ending turn {turnNumber - 1}, starting turn {turnNumber}", LogTag.Turns);

        // 1. Trigger end of turn effects for the previous turn
        TriggerEndOfTurnEffects();

        // 2. Let ActionsQueue handle discards and draws
        if (gameManager?.ActionsQueue != null) {
            gameManager.ActionsQueue.ResolveActions();
        }

        // 3. Trigger start of turn effects for the new turn
        TriggerStartOfTurnEffects();

        // 4. Notify UI and other systems of turn change
        gameMediator?.NotifyGameStateChanged();
    }

    private void TriggerEndOfTurnEffects() {
        Log("Triggering end of turn effects", LogTag.Effects | LogTag.Turns);
        TriggerEffectsForPlayer(gameManager.Player1, EffectTrigger.EndOfTurn);
        TriggerEffectsForPlayer(gameManager.Player2, EffectTrigger.EndOfTurn);
    }

    private void TriggerStartOfTurnEffects() {
        Log("Triggering start of turn effects", LogTag.Effects | LogTag.Turns);
        TriggerEffectsForPlayer(gameManager.Player1, EffectTrigger.StartOfTurn);
        TriggerEffectsForPlayer(gameManager.Player2, EffectTrigger.StartOfTurn);
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
}
