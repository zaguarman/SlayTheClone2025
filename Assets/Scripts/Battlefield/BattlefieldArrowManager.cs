using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Draws one board's intent arrows: the attacks, moves and pending damage of its player's creatures,
    /// the aim of its player's queued spells (from the card in the hand), plus the arrow that follows a creature being dragged. Arrows persist across queue changes,
    /// drawing in when an action is queued and fading out when it is cancelled or resolved.
    /// </summary>
    public class BattlefieldArrowManager {
        public enum DragIntent { None, Attack, Move }

        private static readonly Color AttackColor = new Color(0.93f, 0.22f, 0.2f);
        private static readonly Color DamageColor = new Color(1f, 0.55f, 0.15f);
        private static readonly Color MoveColor = new Color(0.3f, 0.85f, 0.45f);
        private static readonly Color MovedColor = new Color(0.62f, 0.8f, 0.66f); // A creature pushed or swapped by another's move
        private static readonly Color SpellColor = new Color(0.62f, 0.45f, 1f);
        private static readonly Color NeutralColor = new Color(0.75f, 0.75f, 0.8f);

        private readonly Transform parentTransform;
        private readonly IGameMediator gameMediator;
        private readonly IActionsQueue actionsQueue;
        private readonly IPlayer owner; // Only actions started by this player's creatures are drawn here
        private readonly Func<BoardSlot, BattlefieldSlot> findSlotView; // Where a board slot is on screen
        private readonly Func<ICard, CardController> findHandCard; // Where a card in the hand is on screen
        private readonly Func<IGameAction, bool> isHidden; // Fog of War: planned actions the screen doesn't show
        private readonly Dictionary<string, ArrowIndicator> activeArrows = new Dictionary<string, ArrowIndicator>();
        // Arrows that start at a card in the hand, which moves as the hand changes: they follow it every frame
        private readonly Dictionary<string, (ICard card, ITarget target)> handArrows = new Dictionary<string, (ICard, ITarget)>();
        private ArrowIndicator dragArrow;

        public BattlefieldArrowManager(Transform parent, IGameMediator mediator, IActionsQueue actionsQueue, IPlayer owner,
            Func<BoardSlot, BattlefieldSlot> findSlotView, Func<ICard, CardController> findHandCard, Func<IGameAction, bool> isHidden = null) {
            parentTransform = parent ?? throw new ArgumentNullException(nameof(parent));
            gameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            this.actionsQueue = actionsQueue ?? throw new ArgumentNullException(nameof(actionsQueue));
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.findSlotView = findSlotView ?? throw new ArgumentNullException(nameof(findSlotView));
            this.findHandCard = findHandCard ?? throw new ArgumentNullException(nameof(findHandCard));
            this.isHidden = isHidden ?? (_ => false);

            dragArrow = ArrowIndicator.Create(parentTransform);
            gameMediator.ActionsQueueChanged += RefreshArrows;
            gameMediator.BattlefieldStateChanged += OnBattlefieldStateChanged;
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
            handArrows.Clear();
            foreach (var action in actionsQueue.GetPendingActions()) {
                if (isHidden(action)) continue; // A Concealed Action: its creature's card shows ??? instead
                if (TryDescribeArrow(action, out string key, out var arrow)) {
                    wanted[key] = arrow;
                }
            }
            AddFormationArrows(wanted);

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

            // A queued spell that is aimed at a slot points there from its card in the hand;
            // one homing on a creature points wherever that creature is
            if (action is PlayCardAction play && play.GetOwner() == owner && play.GetCard() is Spell spell
                && SlotAimedAt(play.GetTarget()) != null && EffectResolver.IsAimed(spell)) {
                var card = findHandCard(spell);
                var aimView = findSlotView(SlotAimedAt(play.GetTarget()));
                if (card == null || aimView == null) return false;

                key = $"spell_{spell.TargetId}_{play.GetTarget().TargetId}";
                handArrows[key] = (spell, play.GetTarget());
                arrow = (card.transform.position, aimView.transform.position, SpellColor);
                return true;
            }

            BoardSlot from, to;
            Color color;
            switch (action) {
                case BattlefieldCombatAction attack when IsOwn(attack.GetAttacker()) && attack.GetTargetSlot() != null:
                    key = $"combat_{attack.GetAttacker().TargetId}_{attack.GetTargetSlot().TargetId}";
                    (from, to, color) = (attack.GetAttacker().Slot, attack.GetTargetSlot(), AttackColor);
                    break;
                case DamageCreatureAction damage when IsOwn(damage.GetAttacker()) && damage.GetTarget()?.Slot != null:
                    key = $"damage_{damage.GetAttacker().TargetId}_{damage.GetTarget().TargetId}";
                    (from, to, color) = (damage.GetAttacker().Slot, damage.GetTarget().Slot, DamageColor);
                    break;
                default:
                    return false;
            }

            var fromView = findSlotView(from);
            var toView = findSlotView(to);
            if (fromView == null || toView == null) return false;

            arrow = (fromView.transform.position, toView.transform.position, color);
            return true;
        }

        /// <summary>Keeps the arrows of queued spells starting at their card, as the hand fans out, is rebuilt or is hovered.</summary>
        public void FollowHandCards() {
            foreach (var entry in handArrows) {
                if (!activeArrows.TryGetValue(entry.Key, out var arrow) || arrow == null) continue;
                var card = findHandCard(entry.Value.card);
                var aimView = findSlotView(SlotAimedAt(entry.Value.target));
                if (card != null && aimView != null) arrow.Show(card.transform.position, aimView.transform.position);
            }
        }

        private static BoardSlot SlotAimedAt(ITarget target) =>
            target is HomingTarget homing ? homing.CurrentSlot : target as BoardSlot;

        // Live Preview (GDD 2.4): move arrows show the final formation, where every creature of this row ends up once the
        // planned moves resolve, including the ones they push or swap, rather than each move on its own
        private void AddFormationArrows(Dictionary<string, (Vector3 start, Vector3 end, Color color)> wanted) {
            var moves = MovementRules.PlannedMovesOf(owner, actionsQueue, move => !isHidden(move));
            if (moves.Count == 0) return;

            var movers = new HashSet<ICreature>(moves.Select(m => m.Creature));
            var preview = MovementRules.Preview(owner.Battlefield, moves);
            foreach (var entry in preview.FinalSlots) {
                var creature = entry.Key;
                if (creature.Slot == null || creature.Slot == entry.Value) continue;
                var fromView = findSlotView(creature.Slot);
                var toView = findSlotView(entry.Value);
                if (fromView == null || toView == null) continue;
                wanted[$"move_{creature.TargetId}_{entry.Value.TargetId}"] =
                    (fromView.transform.position, toView.transform.position, movers.Contains(creature) ? MoveColor : MovedColor);
            }
        }

        private bool IsOwn(ICreature creature) => creature != null && creature.Owner == owner && creature.Slot != null;
        #endregion

        public void Cleanup() {
            gameMediator.ActionsQueueChanged -= RefreshArrows;
            gameMediator.BattlefieldStateChanged -= OnBattlefieldStateChanged;
            foreach (var arrow in activeArrows.Values) {
                if (arrow != null) GameObject.Destroy(arrow.gameObject);
            }
            activeArrows.Clear();
            if (dragArrow != null) GameObject.Destroy(dragArrow.gameObject);
        }
    }
}
