using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace SlayTheClone {
    /// <summary>
    /// Draws one board's intent arrows: the attacks, moves and pending damage of its player's creatures,
    /// the aim of its player's queued spells (from the card in the hand to a slot, or to a whole battlefield), plus the arrow that follows a creature being dragged. Arrows persist across queue changes,
    /// A spell for a whole side doesn't point into it (its middle is a slot, which reads as aiming at that slot): its arrow
    /// stops at the edge of that battlefield, and the whole battlefield glows in the spell's color while it's queued.
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
        private readonly Func<IPlayer, Transform> findBoardView; // Where a player's battlefield is on screen
        private readonly Func<ICard, CardController> findHandCard; // Where a card in the hand is on screen
        private readonly Func<IGameAction, bool> isHidden; // Fog of War: planned actions the screen doesn't show
        private readonly Dictionary<string, ArrowIndicator> activeArrows = new Dictionary<string, ArrowIndicator>();
        // Arrows that start at a card in the hand, which moves as the hand changes: they follow it every frame
        private readonly Dictionary<string, (ICard card, ITarget target)> handArrows = new Dictionary<string, (ICard, ITarget)>();
        private ArrowIndicator dragArrow;
        private readonly Dictionary<IPlayer, Image> sideGlows = new Dictionary<IPlayer, Image>(); // A side this player's queued spells play on

        public BattlefieldArrowManager(Transform parent, IGameMediator mediator, IActionsQueue actionsQueue, IPlayer owner,
            Func<BoardSlot, BattlefieldSlot> findSlotView, Func<IPlayer, Transform> findBoardView, Func<ICard, CardController> findHandCard,
            Func<IGameAction, bool> isHidden = null) {
            parentTransform = parent ?? throw new ArgumentNullException(nameof(parent));
            gameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            this.actionsQueue = actionsQueue ?? throw new ArgumentNullException(nameof(actionsQueue));
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.findSlotView = findSlotView ?? throw new ArgumentNullException(nameof(findSlotView));
            this.findBoardView = findBoardView ?? throw new ArgumentNullException(nameof(findBoardView));
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
            aimedSides.Clear();
            foreach (var action in actionsQueue.GetPendingActions()) {
                if (isHidden(action)) continue; // A Concealed Action: its creature's card shows ??? instead
                if (TryDescribeArrow(action, out string key, out var arrow)) {
                    wanted[key] = arrow;
                }
            }
            AddFormationArrows(wanted);
            ShowAimedSides();

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

            // A queued spell points from its card in the hand to where it's aimed: a slot, wherever the creature it homes on
            // is, or the whole battlefield of the side it hits
            if (action is PlayCardAction play && play.GetOwner() == owner && play.GetCard() is Spell spell) {
                var card = findHandCard(spell);
                if (card == null || !TryAimPoint(spell, play.GetTarget(), card.transform.position, out var end)) return false;

                key = $"spell_{spell.TargetId}_{play.GetTarget()?.TargetId}";
                handArrows[key] = (spell, play.GetTarget());
                arrow = (card.transform.position, end, SpellColor);
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
                if (card != null && TryAimPoint((Spell)entry.Value.card, entry.Value.target, card.transform.position, out var end)) {
                    arrow.Show(card.transform.position, end);
                }
            }
        }

        private readonly HashSet<IPlayer> aimedSides = new HashSet<IPlayer>();

        // Where a queued spell's arrow ends: the slot it's aimed at, or the near edge of the side it plays on, level with the card
        private bool TryAimPoint(Spell spell, ITarget target, Vector3 from, out Vector3 point) {
            point = default;
            if (EffectResolver.IsAimed(spell)) {
                var slot = target is HomingTarget homing ? homing.CurrentSlot : target as BoardSlot;
                var slotView = slot != null ? findSlotView(slot) : null;
                if (slotView == null) return false;
                point = slotView.transform.position;
                return true;
            }
            var side = SideOf(spell, target);
            var board = findBoardView(side) as RectTransform;
            if (board == null) return false;
            aimedSides.Add(side);

            var corners = new Vector3[4];
            board.GetWorldCorners(corners); // Bottom-left, top-left, top-right, bottom-right
            float inset = (corners[2].x - corners[0].x) * 0.04f;
            bool below = from.y < (corners[0].y + corners[1].y) * 0.5f;
            point = new Vector3(Mathf.Clamp(from.x, corners[0].x + inset, corners[2].x - inset), below ? corners[0].y : corners[1].y, from.z);
            return true;
        }

        // Not aimed: it plays on a side. The one it was dropped on, unless it does nothing there
        private IPlayer SideOf(Spell spell, ITarget target) {
            var side = target switch {
                IPlayer player => player,
                BoardSlot slot => slot.Owner,
                HomingTarget homing => homing.AimedSlot?.Owner,
                _ => null
            };
            if (side == null || !EffectResolver.Reaches(spell, owner, side)) {
                side = EffectResolver.Reaches(spell, owner, owner.Opponent) ? owner.Opponent : owner;
            }
            return side;
        }

        // The battlefields this player's queued spells play on glow in the spell color, pulsing gently
        private void ShowAimedSides() {
            foreach (var side in new[] { owner, owner.Opponent }) {
                bool aimed = aimedSides.Contains(side);
                sideGlows.TryGetValue(side, out var glow);
                if (!aimed) {
                    if (glow != null) {
                        DOTween.Kill(glow);
                        GameObject.Destroy(glow.gameObject);
                    }
                    sideGlows.Remove(side);
                    continue;
                }
                if (glow != null) continue;
                var board = findBoardView(side);
                if (board == null) continue;
                glow = UITheme.AddTrim(board, SpellColor, 1f, -6f, $"SpellAim_{owner.TargetId}");
                glow.raycastTarget = false;
                glow.transform.SetAsLastSibling();
                var fill = UITheme.NewImage("Fill", glow.transform, new Color(SpellColor.r, SpellColor.g, SpellColor.b, 0.08f));
                fill.raycastTarget = false;
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                DOTween.To(() => glow.color.a, a => glow.color = new Color(SpellColor.r, SpellColor.g, SpellColor.b, a), 0.35f, 0.8f)
                    .SetLoops(-1, LoopType.Yoyo).SetTarget(glow);
                sideGlows[side] = glow;
            }
        }

        /// <summary>Whether a queued spell of this player plays on that side, shown by its battlefield glowing.</summary>
        public bool ShowsSpellAimedAt(IPlayer side) => sideGlows.TryGetValue(side, out var glow) && glow != null;

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
            foreach (var glow in sideGlows.Values) {
                if (glow == null) continue;
                DOTween.Kill(glow);
                GameObject.Destroy(glow.gameObject);
            }
            sideGlows.Clear();
            if (dragArrow != null) GameObject.Destroy(dragArrow.gameObject);
        }
    }
}
