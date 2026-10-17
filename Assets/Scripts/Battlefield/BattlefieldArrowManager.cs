using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// Draws one board's battle arrows (see <see cref="ArrowIndicator"/>): its player's planned attacks and pending damage,
    /// the moves of its row (where each creature ends up), plus the arrow that follows a creature being dragged.
    /// Everything aimed at the same target, the same way, is one arrow: creatures attacking the same slot flow into one
    /// trunk and head, wider for each of them.
    /// Queued spells need no arrow: each shows as a ghost where it's aimed (see <see cref="BattlefieldUI"/>), and a whole
    /// battlefield one of them plays on glows in the spell's color while it's queued.
    /// Arrows persist across queue changes, drawing in when an action is queued and fading out when it's cancelled or resolved.
    /// Hovering an action in the queue (see <see cref="Focus"/>) puts the rest away: every board shows only what that action
    /// does and where, until the pointer leaves it.
    /// </summary>
    public class BattlefieldArrowManager {
        public enum DragIntent { None, Attack, Move }

        // The screen's player's attacks are blue, the other side's red; damage a lighter shade of the same
        internal static readonly Color OurAttackColor = new Color(0.25f, 0.55f, 1f);
        internal static readonly Color OurDamageColor = new Color(0.45f, 0.8f, 1f);
        private static readonly Color TheirAttackColor = new Color(0.93f, 0.22f, 0.2f);
        private static readonly Color TheirDamageColor = new Color(1f, 0.55f, 0.15f);
        internal static readonly Color MoveColor = new Color(0.3f, 0.85f, 0.45f);
        private static readonly Color MovedColor = new Color(0.62f, 0.8f, 0.66f); // A creature pushed or swapped by another's move
        private static readonly Color SpellColor = new Color(0.62f, 0.45f, 1f);
        internal static readonly Color NeutralColor = new Color(0.75f, 0.75f, 0.8f);

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
        private readonly Dictionary<IPlayer, Graphic> sideGlows = new Dictionary<IPlayer, Graphic>(); // A side this player's queued spells play on
        private readonly HashSet<IPlayer> aimedSides = new HashSet<IPlayer>();
        private readonly List<BattlefieldSlot> focusGlows = new List<BattlefieldSlot>(); // Where the focused action lands, when no arrow shows it

        /// <summary>The action hovered in the queue: while set, the boards show its arrows alone.</summary>
        public static IGameAction Focused { get; private set; }
        private static event Action FocusChanged;

        /// <summary>Shows only <paramref name="action"/>'s arrows on every board (the user: what did where); null brings the rest back.</summary>
        public static void Focus(IGameAction action) {
            if (Focused == action) return;
            Focused = action;
            FocusChanged?.Invoke();
        }

        // One arrow: where it comes from (one start per action merged into it), where it goes, and how it looks
        private class ArrowPlan {
            public readonly List<Vector3> Starts = new List<Vector3>();
            public readonly List<Vector3> Forks = new List<Vector3>(); // Its other targets: the arrow forks like a Y (Spray and Pray)
            public readonly List<string> Labels = new List<string>(); // The attack's place in the order, at each start
            public Vector3 End;
            public Color Color;
            public ArrowIndicator.Style Style;
        }

        public BattlefieldArrowManager(Transform parent, IGameMediator mediator, IActionsQueue actionsQueue, IPlayer owner,
            Func<BoardSlot, BattlefieldSlot> findSlotView, Func<IPlayer, Transform> findBoardView, Func<IGameAction, bool> isHidden = null,
            Func<bool> isResolving = null) {
            parentTransform = parent ?? throw new ArgumentNullException(nameof(parent));
            Focused = null; // A new game: nothing hovered in its queue yet (an old game's focus can outlive its panel)
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
            FocusChanged += RefreshArrows;
        }

        #region Drag arrow
        // The drag arrow shows only once the pointer is this far from where it started (the user: a short one, still over
        // the creature's own card, is only a stub and a head)
        private Vector3 dragStart;
        private float dragMinLength;
        private bool dragging;

        /// <summary>A creature picked up: its arrow will grow from here once the pointer has gone <paramref name="minLength"/> (world units).</summary>
        public void ShowDragArrow(Vector3 startPos, float minLength = 0f) {
            dragStart = startPos;
            dragMinLength = minLength;
            dragging = true;
            dragArrow.Hide();
        }

        public void UpdateDragArrow(Vector3 worldPos, DragIntent intent) {
            if (!dragging) return;
            if (Vector2.Distance(worldPos, dragStart) < dragMinLength) {
                if (dragArrow.IsVisible()) dragArrow.Hide();
                return;
            }
            if (!dragArrow.IsVisible()) dragArrow.Show(dragStart, worldPos);
            dragArrow.SetColor(intent == DragIntent.Attack ? AttackColor : intent == DragIntent.Move ? MoveColor : NeutralColor);
            dragArrow.UpdateEndPosition(worldPos);
        }

        public void HideDragArrow() {
            dragging = false;
            dragArrow.Hide();
        }
        #endregion

        #region Queued action arrows
        private void OnBattlefieldStateChanged(IPlayer player) => RefreshArrows();

        // While the Day plays out, the arrow of the action happening now replaces the rest: less to look at
        private void OnActionExecuting(IGameAction action) {
            if (!isResolving()) return;
            current = action;
            RefreshArrows();
        }

        /// <summary>Redraws the arrows: everything planned, or while the Day plays out, only what's happening now; the action
        /// hovered in the queue alone, while one is.</summary>
        public void RefreshArrows() {
            var wanted = new Dictionary<string, ArrowPlan>();
            attackNumbers = AttackNumbers(actionsQueue, isHidden);
            aimedSides.Clear();
            foreach (var glow in focusGlows) {
                if (glow != null) glow.SetFocused(false);
            }
            focusGlows.Clear();
            if (Focused != null) {
                if (!isHidden(Focused)) PlanFocused(Focused, wanted);
            }
            else if (isResolving()) {
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
                    arrow.SetForks(plan.Forks);
                    arrow.Show(plan.Starts, plan.End, drawIn: true);
                    arrow.SetLabels(plan.Labels);
                    continue;
                }
                // Endpoints move when creatures change slots, and an arrow gains or loses branches as others join or leave
                arrow.SetStyle(plan.Style);
                arrow.SetColor(plan.Color);
                arrow.SetForks(plan.Forks);
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
                case BattlefieldCombatAction attack when IsOwn(attack.GetAttacker()) && attack.GetTargetSlot() != null: {
                    string number = attackNumbers.TryGetValue(attack, out int n) ? n.ToString() : null;
                    // An attack that also hits beside its target (the user): its own arrow, forking like a Y, a prong to each
                    // creature it'll catch and a small one on to its target, straight down the middle. Planned only; playing
                    // out, each of those hits shows its own arrow as it lands
                    var splash = isResolving() ? new List<BoardSlot>() : SplashedBy(attack);
                    if (splash.Count == 0) {
                        AddBetweenSlots(wanted, $"attack_{attack.GetTargetSlot().TargetId}", attack.GetAttacker().Slot, attack.GetTargetSlot(),
                            AttackColor, ArrowIndicator.Style.Attack, number);
                        break;
                    }
                    var plan = AddBetweenSlots(wanted, $"spray_{attack.GetAttacker().TargetId}", attack.GetAttacker().Slot, attack.GetTargetSlot(),
                        AttackColor, ArrowIndicator.Style.Attack.Centered(), number);
                    if (plan == null) break;
                    foreach (var beside in splash) {
                        var view = findSlotView(beside);
                        if (view != null) plan.Forks.Add(view.transform.position);
                    }
                    break;
                }
                case DamageCreatureAction damage when IsOwn(damage.GetAttacker()) && damage.GetTarget()?.Slot != null:
                    AddBetweenSlots(wanted, $"damage_{damage.GetTarget().TargetId}", damage.GetAttacker().Slot, damage.GetTarget().Slot,
                        DamageColor, ArrowIndicator.Style.Damage);
                    break;
            }
        }

        // The hovered action, if it's this board's player's: an arrow from the creature doing it to where it lands, or where no
        // creature does it (a spell, a summon, a Slot Effect), the slot it lands on glowing, or the side a spell plays on
        private void PlanFocused(IGameAction action, Dictionary<string, ArrowPlan> wanted) {
            var target = TargetSlotOf(action);
            if ((QueueView.SideOf(action) ?? target?.Owner) != owner) return;
            switch (action) {
                case BattlefieldCombatAction:
                case DamageCreatureAction:
                    PlanArrow(action, wanted);
                    return;
                case MoveCreatureAction move when IsOwn(move.GetCreature()) && target != null:
                    AddBetweenSlots(wanted, $"move_{move.GetCreature().TargetId}_{target.TargetId}", move.GetCreature().Slot, target,
                        MoveColor, ArrowIndicator.Style.Move);
                    return;
                case PlayCardAction play when play.GetCard() is Spell spell:
                    FocusSpell(spell, play.GetOwner(), play.GetTarget());
                    return;
                case PlaySpellAction cast:
                    FocusSpell(cast.GetSpell(), cast.GetOwner(), cast.GetTarget());
                    return;
            }
            var from = QueueView.ActorOf(action)?.Slot;
            if (target == null) return;
            if (from != null && from != target) {
                AddBetweenSlots(wanted, $"focus_{target.TargetId}", from, target,
                    action is SwapPositionsAction ? MoveColor : DamageColor, ArrowIndicator.Style.Damage);
            }
            else Glow(target);
        }

        private void FocusSpell(Spell spell, IPlayer caster, ITarget target) {
            if (spell == null || caster == null) return;
            var side = AimOf(spell, caster, target, out var slot);
            if (slot != null) Glow(slot);
            else if (side != null) aimedSides.Add(side);
        }

        private void Glow(BoardSlot slot) {
            var view = findSlotView(slot);
            if (view == null) return;
            view.SetFocused(true);
            focusGlows.Add(view);
        }

        // Where an action lands on the board, if on a slot or a creature in one
        private static BoardSlot TargetSlotOf(IGameAction action) {
            object target = action switch {
                PlayCardAction play => play.GetTarget(),
                PlaySpellAction cast => cast.GetTarget(),
                SummonCreatureAction summon => summon.GetTargetSlot(),
                BattlefieldCombatAction attack => attack.GetTargetSlot(),
                UseAbilityAction use => use.TargetSlot,
                MoveCreatureAction move => move.GetToSlot(),
                DamageCreatureAction damage => damage.GetTarget(),
                HealCreatureAction heal => heal.GetTarget(),
                ModifyAction modify => modify.GetTarget(),
                ModifyArmorAction armor => armor.GetTargetCreature(),
                ApplyStatusEffectAction status => status.GetTargetCreature(),
                CleanseAction cleanse => cleanse.GetTargetCreature(),
                DestroyCreatureAction destroy => destroy.GetCreature(),
                DoomedSentenceAction sentence => sentence.GetCreature(),
                DelayDeploymentAction delay => delay.Creature,
                RushAction rush => rush.Creature,
                SwapStatsAction swapStats => swapStats.Creature,
                PlaceSlotEffectAction place => place.GetSlot(),
                CleanseSlotAction clear => clear.GetSlot(),
                RevealAction reveal => reveal.Slot,
                _ => null
            };
            return target switch {
                BoardSlot slot => slot,
                HomingTarget homing => homing.CurrentSlot ?? homing.AimedSlot,
                ICreature creature => creature.Slot,
                _ => null
            };
        }

        /// <summary>
        /// The other slots an attack will hit as it lands, as the board stands now: the creatures beside its target for Spray
        /// and Pray (a step away in the formation), and those its attacker's on-hit damage spreads to (with its neighbours).
        /// </summary>
        public static List<BoardSlot> SplashedBy(BattlefieldCombatAction attack) {
            var attacker = attack.GetAttacker();
            var target = attack.GetTargetSlot();
            var slots = new List<BoardSlot>();
            if (attacker == null || target?.Owner == null) return slots;
            var row = target.Owner.Battlefield;
            if (Abilities.Has(attacker, CreatureAbility.SprayAndPray)) {
                slots.AddRange(Formation.Neighbours(target.Index).Where(i => i >= 0 && i < row.Count && row[i].OccupyingCreature != null).Select(i => row[i]));
            }
            bool spreads = attacker.Effects.Any(e => e.trigger == EffectTrigger.OnDamage
                && e.actions.Any(a => a.actionType == ActionType.Damage && a.targetModifier.HasFlag(TargetModifier.WithAdjacent)));
            if (spreads && target.OccupyingCreature != null) {
                slots.AddRange(TargetingSystem.GetAdditionalTargets(attacker.Owner, target.OccupyingCreature, TargetModifier.WithAdjacent)
                    .OfType<ICreature>().Where(c => c.Slot != null).Select(c => c.Slot));
            }
            return slots.Where(s => s != target).Distinct().ToList();
        }

        private ArrowPlan AddBetweenSlots(Dictionary<string, ArrowPlan> wanted, string key, BoardSlot from, BoardSlot to, Color color, ArrowIndicator.Style style,
            string label = null) {
            var fromView = findSlotView(from);
            var toView = findSlotView(to);
            if (fromView == null || toView == null) return null;
            var plan = Plan(wanted, key, toView.transform.position, color, style);
            if (plan.Starts.Contains(fromView.transform.position)) return plan;
            plan.Starts.Add(fromView.transform.position);
            plan.Labels.Add(label);
            return plan;
        }

        private Dictionary<IGameAction, int> attackNumbers = new Dictionary<IGameAction, int>();

        /// <summary>
        /// Where each planned attack the screen can see comes in the order they resolve, from 1: by layer, then Speed. Every
        /// attack at the same Speed shares a number, on both sides: the game settles that tie as they resolve (Health + Armor,
        /// then Attack, then a coin flip), so the number never gives away the order the enemy planned.
        /// </summary>
        public static Dictionary<IGameAction, int> AttackNumbers(IActionsQueue queue, Func<IGameAction, bool> hidden) {
            var numbers = new Dictionary<IGameAction, int>();
            int next = 1;
            var attacks = queue.GetPendingActions().OfType<BattlefieldCombatAction>()
                .Where(a => a.GetAttacker() != null && a.GetTargetSlot() != null && !(hidden?.Invoke(a) ?? false));
            foreach (var tied in attacks.GroupBy(a => (queue.LayerOfPending(a), a.GetAttacker().Speed))) {
                foreach (var attack in tied) numbers[attack] = next;
                next++;
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

        // The battlefields this player's queued spells play on glow in the spell color, pulsing gently: a band round the
        // chevron of the mat, just outside it, and a faint wash inside
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
                var mat = board != null ? board.GetComponent<BattlefieldUI>() : null;
                if (mat == null) continue;
                var outline = mat.MatOutline();
                var band = mat.Chevron($"SpellAim_{owner.TargetId}", board.childCount);
                band.SetShape(outline, 4f, -6f);
                band.color = SpellColor;
                band.transform.SetAsLastSibling();
                var wash = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer));
                wash.layer = band.gameObject.layer;
                wash.transform.SetParent(band.transform, false);
                var fill = wash.AddComponent<ChevronGraphic>();
                fill.SetShape(outline);
                fill.color = new Color(SpellColor.r, SpellColor.g, SpellColor.b, 0.08f);
                glow = band;
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

        // Whose the arrows are, as the screen sees it (it changes when the sides are switched, S)
        private bool IsOurs => owner.IsPlayer1 == FogOfWar.PlaysAsPlayer1;
        private Color AttackColor => IsOurs ? OurAttackColor : TheirAttackColor;
        private Color DamageColor => IsOurs ? OurDamageColor : TheirDamageColor;

        private bool IsOwn(ICreature creature) => creature != null && creature.Owner == owner && creature.Slot != null;
        #endregion

        public void Cleanup() {
            Focused = null; // Its queue's rows are going: nothing stays focused
            gameMediator.ActionsQueueChanged -= RefreshArrows;
            actionsQueue.OnActionExecuting.RemoveListener(OnActionExecuting);
            gameMediator.BattlefieldStateChanged -= OnBattlefieldStateChanged;
            FocusChanged -= RefreshArrows;
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
