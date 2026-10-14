using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace SlayTheClone {
    /// <summary>
    /// Draws one board's battle arrows (see <see cref="ArrowIndicator"/>): its player's planned attacks and pending damage,
    /// the moves of its row (where each creature ends up), plus the arrow that follows a creature being dragged.
    /// Everything aimed at the same target, the same way, is one arrow: creatures attacking the same slot flow into one
    /// trunk and head, wider for each of them.
    /// Queued spells need no arrow: each shows as a ghost where it's aimed (see <see cref="BattlefieldUI"/>), and a whole
    /// battlefield one of them plays on glows in the spell's color while it's queued.
    /// Arrows persist across queue changes, drawing in when an action is queued and fading out when it's cancelled or resolved.
    /// </summary>
    public class BattlefieldArrowManager {
        public enum DragIntent { None, Attack, Move }

        // The screen's player's attacks are blue, the other side's red; damage a lighter shade of the same
        private static readonly Color OurAttackColor = new Color(0.25f, 0.55f, 1f);
        private static readonly Color OurDamageColor = new Color(0.45f, 0.8f, 1f);
        private static readonly Color TheirAttackColor = new Color(0.93f, 0.22f, 0.2f);
        private static readonly Color TheirDamageColor = new Color(1f, 0.55f, 0.15f);
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
        private readonly Func<IGameAction, bool> isHidden; // Fog of War: planned actions the screen doesn't show
        private readonly Func<bool> isResolving; // The Day is playing out: only the action happening now has its arrow
        private IGameAction current; // While the Day plays out: the action that ran last, shown until the next one starts
        private readonly Dictionary<string, ArrowIndicator> activeArrows = new Dictionary<string, ArrowIndicator>();
        private ArrowIndicator dragArrow;
        private readonly Dictionary<IPlayer, Image> sideGlows = new Dictionary<IPlayer, Image>(); // A side this player's queued spells play on
        private readonly HashSet<IPlayer> aimedSides = new HashSet<IPlayer>();

        // One arrow: where it comes from (one start per action merged into it), where it goes, and how it looks
        private class ArrowPlan {
            public readonly List<Vector3> Starts = new List<Vector3>();
            public readonly List<string> Labels = new List<string>(); // The attack's place in the order, at each start
            public Vector3 End;
            public Color Color;
            public ArrowIndicator.Style Style;
        }

        public BattlefieldArrowManager(Transform parent, IGameMediator mediator, IActionsQueue actionsQueue, IPlayer owner,
            Func<BoardSlot, BattlefieldSlot> findSlotView, Func<IPlayer, Transform> findBoardView, Func<IGameAction, bool> isHidden = null,
            Func<bool> isResolving = null) {
            parentTransform = parent ?? throw new ArgumentNullException(nameof(parent));
            gameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            this.actionsQueue = actionsQueue ?? throw new ArgumentNullException(nameof(actionsQueue));
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.findSlotView = findSlotView ?? throw new ArgumentNullException(nameof(findSlotView));
            this.findBoardView = findBoardView ?? throw new ArgumentNullException(nameof(findBoardView));
            this.isHidden = isHidden ?? (_ => false);
            this.isResolving = isResolving ?? (() => false);

            dragArrow = ArrowIndicator.Create(parentTransform);
            actionsQueue.OnActionExecuting.AddListener(OnActionExecuting);
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

        // While the Day plays out, the arrow of the action happening now replaces the rest: less to look at
        private void OnActionExecuting(IGameAction action) {
            if (!isResolving()) return;
            current = action;
            RefreshArrows();
        }

        /// <summary>Redraws the arrows: everything planned, or while the Day plays out, only what's happening now.</summary>
        public void RefreshArrows() {
            var wanted = new Dictionary<string, ArrowPlan>();
            attackNumbers = AttackNumbers(actionsQueue, isHidden);
            aimedSides.Clear();
            if (isResolving()) {
                if (current != null && !isHidden(current)) PlanArrow(current, wanted);
            }
            else {
                current = null;
                foreach (var action in actionsQueue.GetPendingActions()) {
                    if (isHidden(action)) continue; // A Concealed Action: its creature's card shows ??? instead
                    PlanArrow(action, wanted);
                }
                AddFormationArrows(wanted);
            }
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
                    arrow.SetLabels(plan.Labels);
                    continue;
                }
                // Endpoints move when creatures change slots, and an arrow gains or loses branches as others join or leave
                arrow.SetStyle(plan.Style);
                arrow.SetColor(plan.Color);
                arrow.Show(plan.Starts, plan.End);
                arrow.SetLabels(plan.Labels);
            }
        }

        // Adds the action's arrow to the plan, merged with any other arrow of the same kind aimed at the same target
        private void PlanArrow(IGameAction action, Dictionary<string, ArrowPlan> wanted) {
            // A queued spell shows as a ghost where it's aimed; the battlefield of a side it plays on as a whole glows
            if (action is PlayCardAction play && play.GetOwner() == owner && play.GetCard() is Spell spell) {
                var side = AimOf(spell, owner, play.GetTarget(), out var slot);
                if (side != null && slot == null) aimedSides.Add(side);
                return;
            }

            switch (action) {
                case BattlefieldCombatAction attack when IsOwn(attack.GetAttacker()) && attack.GetTargetSlot() != null:
                    AddBetweenSlots(wanted, $"attack_{attack.GetTargetSlot().TargetId}", attack.GetAttacker().Slot, attack.GetTargetSlot(),
                        AttackColor, ArrowIndicator.Style.Attack, attackNumbers.TryGetValue(attack, out int number) ? number.ToString() : null);
                    break;
                case DamageCreatureAction damage when IsOwn(damage.GetAttacker()) && damage.GetTarget()?.Slot != null:
                    AddBetweenSlots(wanted, $"damage_{damage.GetTarget().TargetId}", damage.GetAttacker().Slot, damage.GetTarget().Slot,
                        DamageColor, ArrowIndicator.Style.Damage);
                    break;
            }
        }

        private void AddBetweenSlots(Dictionary<string, ArrowPlan> wanted, string key, BoardSlot from, BoardSlot to, Color color, ArrowIndicator.Style style,
            string label = null) {
            var fromView = findSlotView(from);
            var toView = findSlotView(to);
            if (fromView == null || toView == null) return;
            var plan = Plan(wanted, key, toView.transform.position, color, style);
            if (plan.Starts.Contains(fromView.transform.position)) return;
            plan.Starts.Add(fromView.transform.position);
            plan.Labels.Add(label);
        }

        private Dictionary<IGameAction, int> attackNumbers = new Dictionary<IGameAction, int>();

        /// <summary>
        /// Where each planned attack the screen can see comes in the order they resolve, from 1: by layer, then Speed. At the
        /// same Speed one side's attacks follow the order its player planned them, and the two sides' share numbers, a tie
        /// (the first of each side is the same number), since which of those goes first is only settled as they resolve.
        /// </summary>
        public static Dictionary<IGameAction, int> AttackNumbers(IActionsQueue queue, Func<IGameAction, bool> hidden) {
            var numbers = new Dictionary<IGameAction, int>();
            int next = 1;
            var attacks = queue.GetPendingActions().OfType<BattlefieldCombatAction>()
                .Where(a => a.GetAttacker() != null && a.GetTargetSlot() != null && !(hidden?.Invoke(a) ?? false));
            foreach (var tied in attacks.GroupBy(a => (queue.LayerOfPending(a), a.GetAttacker().Speed))) {
                int most = 0;
                foreach (var side in tied.GroupBy(a => a.GetAttacker().Owner)) {
                    int i = 0;
                    foreach (var attack in side) numbers[attack] = next + i++;
                    most = Math.Max(most, i);
                }
                next += most;
            }
            return numbers;
        }

        private static ArrowPlan Plan(Dictionary<string, ArrowPlan> wanted, string key, Vector3 end, Color color, ArrowIndicator.Style style) {
            if (!wanted.TryGetValue(key, out var plan)) wanted[key] = plan = new ArrowPlan { End = end, Color = color, Style = style };
            return plan;
        }

        /// <summary>
        /// Where a queued spell is aimed: the slot it's aimed at (wherever the creature it homes on is now), or, for a
        /// spell that plays on a whole side, no slot and that side: the one it was dropped on, unless it does nothing
        /// there. Returns the side either way (null when the slot it was aimed at is gone).
        /// </summary>
        public static IPlayer AimOf(Spell spell, IPlayer caster, ITarget target, out BoardSlot slot) {
            slot = null;
            if (EffectResolver.IsAimed(spell)) {
                slot = target is HomingTarget homing ? homing.CurrentSlot : target as BoardSlot;
                return slot?.Owner;
            }
            var side = target switch {
                IPlayer player => player,
                BoardSlot aimed => aimed.Owner,
                HomingTarget homing => homing.AimedSlot?.Owner,
                _ => null
            };
            if (side == null || !EffectResolver.Reaches(spell, caster, side)) {
                side = EffectResolver.Reaches(spell, caster, caster.Opponent) ? caster.Opponent : caster;
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

        // Whose the arrows are, as the screen sees it (it changes when the sides are switched, F8)
        private bool IsOurs => owner.IsPlayer1 == FogOfWar.PlaysAsPlayer1;
        private Color AttackColor => IsOurs ? OurAttackColor : TheirAttackColor;
        private Color DamageColor => IsOurs ? OurDamageColor : TheirDamageColor;

        private bool IsOwn(ICreature creature) => creature != null && creature.Owner == owner && creature.Slot != null;
        #endregion

        public void Cleanup() {
            gameMediator.ActionsQueueChanged -= RefreshArrows;
            actionsQueue.OnActionExecuting.RemoveListener(OnActionExecuting);
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
