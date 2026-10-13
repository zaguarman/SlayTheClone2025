using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace SlayTheClone {
    /// <summary>
    /// Draws one board's battle arrows (see <see cref="ArrowIndicator"/>): its player's planned attacks and pending damage,
    /// the moves of its row (where each creature ends up), the aim of its queued spells (from the card in the hand to a
    /// slot, or to a whole battlefield), plus the arrow that follows a creature being dragged.
    /// Everything aimed at the same target, the same way, is one arrow: creatures attacking the same slot flow into one
    /// trunk and head, wider for each of them, as do spells aimed at the same place.
    /// A spell for a whole side doesn't point into it (its middle is a slot, which reads as aiming at that slot): its arrow
    /// stops at the edge of that battlefield, and the whole battlefield glows in the spell's color while it's queued.
    /// Arrows persist across queue changes, drawing in when an action is queued and fading out when it's cancelled or resolved.
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
        // Arrows that start at cards in the hand, which move as the hand changes: they follow them every frame
        private readonly Dictionary<string, List<(ICard card, ITarget target)>> handArrows = new Dictionary<string, List<(ICard, ITarget)>>();
        private ArrowIndicator dragArrow;
        private readonly Dictionary<IPlayer, Image> sideGlows = new Dictionary<IPlayer, Image>(); // A side this player's queued spells play on
        private readonly HashSet<IPlayer> aimedSides = new HashSet<IPlayer>();

        // One arrow: where it comes from (one start per action merged into it), where it goes, and how it looks
        private class ArrowPlan {
            public readonly List<Vector3> Starts = new List<Vector3>();
            public Vector3 End;
            public Color Color;
            public ArrowIndicator.Style Style;
        }

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
            dragArrow.SetStyle(ArrowIndicator.Style.Drag);
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
            var wanted = new Dictionary<string, ArrowPlan>();
            handArrows.Clear();
            aimedSides.Clear();
            foreach (var action in actionsQueue.GetPendingActions()) {
                if (isHidden(action)) continue; // A Concealed Action: its creature's card shows ??? instead
                PlanArrow(action, wanted);
            }
            AddFormationArrows(wanted);
            ShowAimedSides();

            foreach (var key in activeArrows.Keys.Where(k => !wanted.ContainsKey(k)).ToList()) {
                if (activeArrows[key] != null) activeArrows[key].FadeOutAndDestroy();
                activeArrows.Remove(key);
            }

            foreach (var entry in wanted) {
                var plan = entry.Value;
                if (!activeArrows.TryGetValue(entry.Key, out var arrow) || arrow == null) {
                    arrow = ArrowIndicator.Create(parentTransform);
                    activeArrows[entry.Key] = arrow;
                    arrow.SetStyle(plan.Style);
                    arrow.SetColor(plan.Color);
                    arrow.Show(plan.Starts, plan.End, drawIn: true);
                    continue;
                }
                // Endpoints move when creatures change slots, and an arrow gains or loses branches as others join or leave
                arrow.SetStyle(plan.Style);
                arrow.SetColor(plan.Color);
                arrow.Show(plan.Starts, plan.End);
            }
        }

        // Adds the action's arrow to the plan, merged with any other arrow of the same kind aimed at the same target
        private void PlanArrow(IGameAction action, Dictionary<string, ArrowPlan> wanted) {
            // A queued spell points from its card in the hand to where it's aimed: a slot, wherever the creature it homes on
            // is, or the whole battlefield of the side it hits
            if (action is PlayCardAction play && play.GetOwner() == owner && play.GetCard() is Spell spell) {
                var card = findHandCard(spell);
                if (card == null || !TryAim(spell, play.GetTarget(), out var end, out string aimKey, out bool wholeSide)) return;
                string key = $"spell_{aimKey}";
                var plan = Plan(wanted, key, end, SpellColor, wholeSide ? ArrowIndicator.Style.Spell.ToEdge() : ArrowIndicator.Style.Spell);
                plan.Starts.Add(card.transform.position);
                if (!handArrows.TryGetValue(key, out var cards)) handArrows[key] = cards = new List<(ICard, ITarget)>();
                cards.Add((spell, play.GetTarget()));
                return;
            }

            switch (action) {
                case BattlefieldCombatAction attack when IsOwn(attack.GetAttacker()) && attack.GetTargetSlot() != null:
                    AddBetweenSlots(wanted, $"attack_{attack.GetTargetSlot().TargetId}", attack.GetAttacker().Slot, attack.GetTargetSlot(),
                        AttackColor, ArrowIndicator.Style.Attack);
                    break;
                case DamageCreatureAction damage when IsOwn(damage.GetAttacker()) && damage.GetTarget()?.Slot != null:
                    AddBetweenSlots(wanted, $"damage_{damage.GetTarget().TargetId}", damage.GetAttacker().Slot, damage.GetTarget().Slot,
                        DamageColor, ArrowIndicator.Style.Damage);
                    break;
            }
        }

        private void AddBetweenSlots(Dictionary<string, ArrowPlan> wanted, string key, BoardSlot from, BoardSlot to, Color color, ArrowIndicator.Style style) {
            var fromView = findSlotView(from);
            var toView = findSlotView(to);
            if (fromView == null || toView == null) return;
            var plan = Plan(wanted, key, toView.transform.position, color, style);
            if (!plan.Starts.Contains(fromView.transform.position)) plan.Starts.Add(fromView.transform.position);
        }

        private static ArrowPlan Plan(Dictionary<string, ArrowPlan> wanted, string key, Vector3 end, Color color, ArrowIndicator.Style style) {
            if (!wanted.TryGetValue(key, out var plan)) wanted[key] = plan = new ArrowPlan { End = end, Color = color, Style = style };
            return plan;
        }

        /// <summary>Keeps the arrows of queued spells starting at their cards, as the hand fans out, is rebuilt or is hovered.</summary>
        public void FollowHandCards() {
            foreach (var entry in handArrows) {
                if (!activeArrows.TryGetValue(entry.Key, out var arrow) || arrow == null || entry.Value.Count == 0) continue;
                var starts = new List<Vector3>();
                foreach (var (card, _) in entry.Value) {
                    var view = findHandCard(card);
                    if (view != null) starts.Add(view.transform.position);
                }
                var (firstCard, firstTarget) = entry.Value[0];
                if (starts.Count > 0 && TryAim((Spell)firstCard, firstTarget, out var end, out _, out _)) arrow.Show(starts, end);
            }
        }

        // Where a queued spell's arrow ends: the slot it's aimed at, or the near edge of the side it plays on, level with
        // the middle of that battlefield's near edge (not a slot)
        private bool TryAim(Spell spell, ITarget target, out Vector3 point, out string aimKey, out bool wholeSide) {
            point = default;
            aimKey = null;
            wholeSide = false;
            if (EffectResolver.IsAimed(spell)) {
                var slot = target is HomingTarget homing ? homing.CurrentSlot : target as BoardSlot;
                var slotView = slot != null ? findSlotView(slot) : null;
                if (slotView == null) return false;
                point = slotView.transform.position;
                aimKey = slot.TargetId;
                return true;
            }
            var side = SideOf(spell, target);
            if (!(findBoardView(side) is RectTransform board)) return false;
            aimedSides.Add(side);
            wholeSide = true;
            aimKey = $"side_{side.TargetId}";

            var corners = new Vector3[4];
            board.GetWorldCorners(corners); // Bottom-left, top-left, top-right, bottom-right
            var hand = findHandCard(spell);
            float fromY = hand != null ? hand.transform.position.y : 0f;
            bool below = fromY < (corners[0].y + corners[1].y) * 0.5f;
            // Between the middle slot and the next one, so it can't be read as aiming at a slot
            float x = (corners[0].x + corners[2].x) * 0.5f + (corners[2].x - corners[0].x) * 0.1f;
            point = new Vector3(x, below ? corners[0].y : corners[1].y, corners[0].z);
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
        // planned moves resolve, including the ones they push or swap, rather than each move on its own. Along the row they
        // bow to the right of where they go, so a swap's two arrows pass each other instead of overlapping
        private void AddFormationArrows(Dictionary<string, ArrowPlan> wanted) {
            var moves = MovementRules.PlannedMovesOf(owner, actionsQueue, move => !isHidden(move));
            if (moves.Count == 0) return;

            var movers = new HashSet<ICreature>(moves.Select(m => m.Creature));
            var preview = MovementRules.Preview(owner.Battlefield, moves);
            foreach (var entry in preview.FinalSlots) {
                var creature = entry.Key;
                if (creature.Slot == null || creature.Slot == entry.Value) continue;
                AddBetweenSlots(wanted, $"move_{creature.TargetId}_{entry.Value.TargetId}", creature.Slot, entry.Value,
                    movers.Contains(creature) ? MoveColor : MovedColor, ArrowIndicator.Style.Move);
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
