using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Draws one board's intent arrows: the attacks, moves and pending damage of its player's creatures,
/// plus the arrow that follows a creature being dragged. Arrows persist across queue changes,
/// drawing in when an action is queued and fading out when it is cancelled or resolved.
/// </summary>
public class BattlefieldArrowManager {
    public enum DragIntent { None, Attack, Move }

    private static readonly Color AttackColor = new Color(0.93f, 0.22f, 0.2f);
    private static readonly Color DamageColor = new Color(1f, 0.55f, 0.15f);
    private static readonly Color MoveColor = new Color(0.3f, 0.85f, 0.45f);
    private static readonly Color NeutralColor = new Color(0.75f, 0.75f, 0.8f);

    private readonly Transform parentTransform;
    private readonly IGameMediator gameMediator;
    private readonly IActionsQueue actionsQueue;
    private readonly IPlayer owner; // Only actions started by this player's creatures are drawn here
    private readonly Dictionary<string, ArrowIndicator> activeArrows = new Dictionary<string, ArrowIndicator>();
    private ArrowIndicator dragArrow;

    public BattlefieldArrowManager(Transform parent, IGameMediator mediator, IActionsQueue actionsQueue, IPlayer owner) {
        parentTransform = parent ?? throw new ArgumentNullException(nameof(parent));
        gameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        this.actionsQueue = actionsQueue ?? throw new ArgumentNullException(nameof(actionsQueue));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

        dragArrow = ArrowIndicator.Create(parentTransform);
        gameMediator.AddActionsQueueChangedListener(RefreshArrows);
        gameMediator.AddBattlefieldStateChangedListener(OnBattlefieldStateChanged);
    }

    #region Drag arrow
    public void ShowDragArrow(Vector3 startPos) {
        dragArrow.SetColor(NeutralColor);
        dragArrow.Show(startPos, startPos);
    }

    public void UpdateDragArrow(Vector3 worldPos, DragIntent intent) {
        if (!dragArrow.IsVisible()) return;
        dragArrow.SetColor(intent == DragIntent.Attack ? AttackColor : intent == DragIntent.Move ? MoveColor : NeutralColor);
        dragArrow.UpdateEndPosition(worldPos);
    }

    public void HideDragArrow() => dragArrow.Hide();
    #endregion

    #region Queued action arrows
    private void OnBattlefieldStateChanged(IPlayer player) => RefreshArrows();

    private void RefreshArrows() {
        var wanted = new Dictionary<string, (Vector3 start, Vector3 end, Color color)>();
        foreach (var action in actionsQueue.GetPendingActions()) {
            if (TryDescribeArrow(action, out string key, out var arrow)) {
                wanted[key] = arrow;
            }
        }

        foreach (var key in activeArrows.Keys.Where(k => !wanted.ContainsKey(k)).ToList()) {
            if (activeArrows[key] != null) activeArrows[key].FadeOutAndDestroy();
            activeArrows.Remove(key);
        }

        foreach (var entry in wanted) {
            var (start, end, color) = entry.Value;
            if (activeArrows.TryGetValue(entry.Key, out var existing) && existing != null) {
                existing.Show(start, end); // Endpoints can move when creatures change slots
                continue;
            }

            var arrow = ArrowIndicator.Create(parentTransform);
            arrow.SetColor(color);
            arrow.Show(start, end, drawIn: true);
            activeArrows[entry.Key] = arrow;
        }
    }

    private bool TryDescribeArrow(IGameAction action, out string key, out (Vector3 start, Vector3 end, Color color) arrow) {
        key = null;
        arrow = default;

        switch (action) {
            case BattlefieldCombatAction attack when IsOwn(attack.GetAttacker()) && attack.GetTargetSlot() != null:
                key = $"combat_{attack.GetAttacker().TargetId}_{attack.GetTargetSlot().TargetId}";
                arrow = (CreaturePosition(attack.GetAttacker()), attack.GetTargetSlot().transform.position, AttackColor);
                return true;
            case DamageCreatureAction damage when IsOwn(damage.GetAttacker()) && damage.GetTarget()?.Slot != null:
                key = $"damage_{damage.GetAttacker().TargetId}_{damage.GetTarget().TargetId}";
                arrow = (CreaturePosition(damage.GetAttacker()), CreaturePosition(damage.GetTarget()), DamageColor);
                return true;
            case MoveCreatureAction move when move.GetPlayer() == owner && move.GetFromSlot() is BattlefieldSlot from && move.GetToSlot() is BattlefieldSlot to:
                key = $"move_{move.GetCreature()?.TargetId}_{to.TargetId}";
                arrow = (from.transform.position, to.transform.position, MoveColor);
                return true;
            default:
                return false;
        }
    }

    private bool IsOwn(ICreature creature) => creature != null && creature.Owner == owner && creature.Slot != null;

    private static Vector3 CreaturePosition(ICreature creature) => creature.Slot.transform.position;
    #endregion

    public void Cleanup() {
        gameMediator.RemoveActionsQueueChangedListener(RefreshArrows);
        gameMediator.RemoveBattlefieldStateChangedListener(OnBattlefieldStateChanged);
        foreach (var arrow in activeArrows.Values) {
            if (arrow != null) GameObject.Destroy(arrow.gameObject);
        }
        activeArrows.Clear();
        if (dragArrow != null) GameObject.Destroy(dragArrow.gameObject);
    }
}
