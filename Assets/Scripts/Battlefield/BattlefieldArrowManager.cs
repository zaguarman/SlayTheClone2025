using UnityEngine;
using System.Collections.Generic;
using static DebugLogger;
using System.Linq;

public class BattlefieldArrowManager {
    private readonly Transform parentTransform;
    private readonly GameManager gameManager; // Keep for now, will be replaced with interface later
    private readonly IGameReferences gameReferences;
    private readonly IGameMediator gameMediator;
    private ArrowIndicator dragArrowIndicator;
    private Dictionary<string, ArrowIndicator> activeArrows = new Dictionary<string, ArrowIndicator>();
    private bool isUpdating = false;
    private int lastProcessedActionCount = 0;
    private HashSet<string> lastProcessedActionKeys = new HashSet<string>();

    public BattlefieldArrowManager(Transform parent, IGameMediator mediator, IGameReferences references) {
        this.parentTransform = parent;
        this.gameManager = GameManager.Instance; // Temporary, will be injected later
        this.gameReferences = references ?? throw new System.ArgumentNullException(nameof(references));
        this.gameMediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
        SetupDragArrow();
        RegisterEvents();
    }

    private void RegisterEvents() {
        gameMediator.AddActionsQueueChangedListener(OnActionsQueueChanged);
        gameMediator.AddBattlefieldStateChangedListener(OnBattlefieldStateChanged);
    }

    private void OnActionsQueueChanged() {
        if (gameManager.ActionsQueue == null) return;

        var pendingActions = gameManager.ActionsQueue.GetPendingActions();
        int currentActionCount = pendingActions.Count();

        // Get the keys of all current actions
        HashSet<string> currentActionKeys = new HashSet<string>();
        foreach (var action in pendingActions) {
            string actionKey = GetActionKey(action);
            if (actionKey != null) {
                currentActionKeys.Add(actionKey);
            }
        }

        // Update if the count changed OR if the action keys changed
        if (currentActionCount != lastProcessedActionCount || !SetEquals(currentActionKeys, lastProcessedActionKeys)) {
            lastProcessedActionCount = currentActionCount;
            lastProcessedActionKeys = currentActionKeys;
            UpdateArrowsFromActionsQueue();
        }
    }

    // Helper method to check if two sets are equal
    private bool SetEquals(HashSet<string> set1, HashSet<string> set2) {
        if (set1.Count != set2.Count)
            return false;

        foreach (var item in set1) {
            if (!set2.Contains(item))
                return false;
        }

        return true;
    }

    private void OnBattlefieldStateChanged(IPlayer player) {
        UpdateArrowsFromActionsQueue();
    }

    private void SetupDragArrow() {
        dragArrowIndicator = ArrowIndicator.Create(parentTransform);
        dragArrowIndicator.Hide();
    }

    public void ShowDragArrow(Vector3 startPos) {
        startPos.z = 0;
        dragArrowIndicator.Show(startPos, startPos);
    }

    public void UpdateDragArrow(Vector3 worldPos) {
        if (dragArrowIndicator != null && dragArrowIndicator.IsVisible()) {
            worldPos.z = 0;
            dragArrowIndicator.UpdateEndPosition(worldPos);
        }
    }

    public void HideDragArrow() {
        if (dragArrowIndicator != null) {
            dragArrowIndicator.Hide();
        }
    }

    private void UpdateArrowsFromActionsQueue() {
        if (isUpdating) return;

        isUpdating = true;
        Log("Starting update of arrows from actions queue", LogTag.Actions);

        ClearExistingArrows();

        if (gameManager.ActionsQueue == null) {
            LogWarning("ActionsQueue is null!", LogTag.Actions);
            isUpdating = false;
            return;
        }

        var pendingActions = gameManager.ActionsQueue.GetPendingActions();
        Log($"Number of pending actions: {pendingActions.Count()}", LogTag.Actions);

        ProcessQueuedActions(pendingActions);

        isUpdating = false;
    }

    private void ClearExistingArrows() {
        foreach (var arrow in activeArrows.Values) {
            if (arrow != null) {
                Object.Destroy(arrow.gameObject);
            }
        }
        activeArrows.Clear();
    }

    private void ProcessQueuedActions(IReadOnlyCollection<IGameAction> actions) {
        foreach (var action in actions) {
            string actionKey = GetActionKey(action);
            if (actionKey == null) continue;

            if (!activeArrows.ContainsKey(actionKey)) {
                CreateArrowForAction(action, actionKey);
            }
        }
    }

    private string GetActionKey(IGameAction action) {
        return action switch {
            BattlefieldCombatAction markCombatAction => $"combat_{markCombatAction.GetAttacker()?.TargetId}_{markCombatAction.GetTargetSlot()?.TargetId}",
            DamageCreatureAction damageAction => $"damage_{damageAction.GetAttacker()?.TargetId}_{damageAction.GetTarget()?.TargetId}",
            MoveCreatureAction moveAction => $"move_{moveAction.GetCreature()?.TargetId}_{moveAction.GetToSlot()?.TargetId}",
            _ => null
        };
    }

    private void CreateArrowForAction(IGameAction action, string actionKey) {
        switch (action) {
            case BattlefieldCombatAction markCombatAction:
                CreateArrowForMarkCombatAction(markCombatAction, actionKey);
                break;
            case DamageCreatureAction damageAction:
                CreateArrowForDamageAction(damageAction, actionKey);
                break;
            case MoveCreatureAction moveAction:
                CreateArrowForMoveAction(moveAction, actionKey);
                break;
        }
    }

    private void CreateArrowForMarkCombatAction(BattlefieldCombatAction action, string actionKey) {
        var attacker = action.GetAttacker();
        var targetSlot = action.GetTargetSlot();

        if (attacker == null || targetSlot == null) return;

        var arrow = ArrowIndicator.Create(parentTransform);
        Vector3 startPos = GetCreaturePosition(attacker);
        Vector3 endPos = targetSlot.transform.position;

        startPos.z = 0;
        endPos.z = 0;

        arrow.Show(startPos, endPos);
        arrow.SetColor(Color.red);
        activeArrows[actionKey] = arrow;

        Log($"Created combat targeting arrow from {attacker.Name} to slot {targetSlot.TargetId}", LogTag.Actions | LogTag.UI);
    }

    private void CreateArrowForDamageAction(DamageCreatureAction damageAction, string actionKey) {
        var attacker = damageAction.GetAttacker();
        var target = damageAction.GetTarget();

        if (attacker == null || target == null) return;

        var arrow = ArrowIndicator.Create(parentTransform);
        Vector3 startPos = GetCreaturePosition(attacker);
        Vector3 endPos = GetCreaturePosition(target);

        startPos.z = 0;
        endPos.z = 0;

        arrow.Show(startPos, endPos);
        arrow.SetColor(Color.red);
        activeArrows[actionKey] = arrow;

        Log($"Created damage arrow from {attacker.Name} to {target.Name}", LogTag.Actions | LogTag.UI);
    }

    private void CreateArrowForMoveAction(MoveCreatureAction moveAction, string actionKey) {
        var creature = moveAction.GetCreature();
        if (creature == null) return;

        var arrow = ArrowIndicator.Create(parentTransform);
        Vector3 startPos = GetSlotPosition(moveAction.GetFromSlot());
        Vector3 endPos = GetSlotPosition(moveAction.GetToSlot());

        startPos.z = 0;
        endPos.z = 0;

        arrow.Show(startPos, endPos);
        arrow.SetColor(Color.green);
        activeArrows[actionKey] = arrow;

        Log($"Created move arrow for {creature.Name} to slot {moveAction.GetToSlot()}", LogTag.Actions | LogTag.UI);
    }

    private Vector3 GetCreaturePosition(ICreature creature) {
        if (creature == null) return Vector3.zero;

        // Get position from the creature's slot if available
        if (creature.Slot != null) {
            var slotPos = creature.Slot.transform.position;
            Log($"Getting position from slot {creature.Slot.TargetId} for {creature.Name}", LogTag.UI);
            return slotPos;
        }

        // Fallback to card controller position
        var cardController = gameReferences.GetPlayer1BattlefieldUI().GetCardController(creature) ??
                            gameReferences.GetPlayer2BattlefieldUI().GetCardController(creature);

        if (cardController != null) {
            return cardController.transform.position;
        }

        LogWarning($"Could not find position for creature {creature.Name}", LogTag.UI);
        return Vector3.zero;
    }

    private Vector3 GetPlayerTargetPosition(IPlayer player) {
        var playerUI = player.IsPlayer1() ?
            gameReferences.GetPlayer1UI() :
            gameReferences.GetPlayer2UI();

        if (playerUI != null) {
            return playerUI.transform.position;
        }

        LogWarning($"Could not find UI for Player {(player.IsPlayer1() ? "1" : "2")}", LogTag.Actions);
        return Vector3.zero;
    }

    private Vector3 GetSlotPosition(ITarget slotIndex) {
        var player1Battlefield = gameReferences.GetPlayer1BattlefieldUI();
        var player2Battlefield = gameReferences.GetPlayer2BattlefieldUI();

        Transform slotTransform = null;

        // Check player1's battlefield first
        if (player1Battlefield != null) {
            var slots = player1Battlefield.GetComponentsInChildren<BattlefieldSlot>();
            var slot = slots.FirstOrDefault(s => s.TargetId == slotIndex.TargetId);
            if (slot != null) {
                slotTransform = slot.transform;
            }
        }

        // If not found, check player2's battlefield
        if (slotTransform == null && player2Battlefield != null) {
            var slots = player2Battlefield.GetComponentsInChildren<BattlefieldSlot>();
            var slot = slots.FirstOrDefault(s => s.TargetId == slotIndex.TargetId);
            if (slot != null) {
                slotTransform = slot.transform;
            }
        }

        return slotTransform != null ? slotTransform.position : Vector3.zero;
    }

    public void Cleanup() {
        gameMediator.RemoveActionsQueueChangedListener(OnActionsQueueChanged);
        ClearExistingArrows();
        if (dragArrowIndicator != null) {
            Object.Destroy(dragArrowIndicator.gameObject);
        }
        lastProcessedActionCount = 0;
        lastProcessedActionKeys.Clear();
    }
}