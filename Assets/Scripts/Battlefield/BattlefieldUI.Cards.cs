using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>The battlefield: playing cards onto it and where they land (a part of BattlefieldUI).</summary>
    public partial class BattlefieldUI {

        #region Card Handling
        /// <summary>Where a dragged card lands on this battlefield: one of its slots, the whole side, or nowhere.</summary>
        public enum DropAim { None, Slot, Side }

        /// <summary>
        /// What dropping this card here would aim at. Creatures on the board move or attack into a slot; creatures from the
        /// hand are summoned into a slot of their own side (a Gift into an empty one of the other side); aimed spells hit a slot on either side; other spells
        /// already know their targets (all enemy creatures, a Trend...), so they play wherever on the table they're dropped. Only what it aims at lights up.
        /// </summary>
        public DropAim AimOf(CardController card) {
            if (card == null || Player == null) return DropAim.None;
            // A queued creature's ghost is still a card in the hand
            if (!IsCardFromHand(card) && !card.IsQueued) return card.GetLinkedCreature() != null ? DropAim.Slot : DropAim.None;

            switch (card.GetLinkedCardInstance()) {
                case ICreature gift when gift.IsGift:
                    return card.Player != Player ? DropAim.Slot : DropAim.None;
                case ICreature:
                    return card.Player == Player ? DropAim.Slot : DropAim.None;
                case Spell spell when EffectResolver.IsAimed(spell):
                    return DropAim.Slot;
                case Spell:
                    return DropAim.Side;
                default:
                    return DropAim.None;
            }
        }

        protected override void HandlePointerEnter(PointerEventData eventData) {
            var dragged = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<CardController>() : null;
            if (AimOf(dragged) == DropAim.Side) LightUp(true);
            if (PlaysAnywhere(dragged)) table.GetBattlefield(Player.Opponent)?.LightUp(true); // Either side will do: both light up
        }

        protected override void HandlePointerExit(PointerEventData eventData) {
            var dragged = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<CardController>() : null;
            LightUp(false); // Off this mat: its glow goes (the zone's own image used to reset it)
            if (PlaysAnywhere(dragged)) table.GetBattlefield(Player.Opponent)?.LightUp(false);
        }

        private bool matLit;

        /// <summary>Lights the mat up as where a carried card would play, or puts it back.</summary>
        public void LightUp(bool lit) {
            matLit = lit;
            var fill = transform.Find(MatFillName)?.GetComponent<ChevronGraphic>();
            if (fill != null) fill.color = lit ? UITheme.MatValid : sideEligible ? UITheme.MatEligible : UITheme.Mat;
        }

        // A side the carried card can be played on glows softly from the moment it's picked up (the user, 2026-10-17), and
        // fully under the pointer (see LightUp)
        private bool sideEligible;

        private void SetSideEligible(bool eligible) {
            if (sideEligible == eligible) return;
            sideEligible = eligible;
            LightUp(matLit);
        }



        /// <summary>
        /// Whether this card plays wherever it's dropped on the table: a spell that isn't aimed at a slot (its targets are
        /// already set: all enemy creatures, its caster, a Trend...). See <see cref="PlayArea"/>.
        /// </summary>
        public bool PlaysAnywhere(CardController card) =>
            card?.Player != null && (IsCardFromHand(card) || card.IsQueued) && card.GetLinkedCardInstance() is Spell spell && !EffectResolver.IsAimed(spell);

        /// <summary>Plays a card that plays anywhere, dropped somewhere on the table: on a side it does something to.</summary>
        public void PlayAnywhere(CardController card) {
            if (!PlaysAnywhere(card) || GameManager == null) return;
            PlayFromHand(card, SideFor((Spell)card.GetLinkedCardInstance(), card.Player, card.Player));
        }

        // The side a spell that plays anywhere is played on: the one it was dropped on if it does something there, else
        // its caster's, else the other one
        private static IPlayer SideFor(Spell spell, IPlayer caster, IPlayer droppedOn) =>
            EffectResolver.Reaches(spell, caster, droppedOn) ? droppedOn : EffectResolver.Reaches(spell, caster, caster) ? caster : caster.Opponent;

        protected override void HandleCardDropped(CardController card) {
            if (card == null || !CanAcceptCard(card)) return;
            if (GameManager == null) return;

            var aim = AimOf(card);
            if (aim == DropAim.Side) {
                PlayFromHand(card, SideFor((Spell)card.GetLinkedCardInstance(), card.Player, Player));
                return;
            }
            if (PlayOnTarget(card)) return; // A card being played lands where the board picked, whatever took the drop
            var targetSlot = aim == DropAim.Slot ? GetTargetSlot() : null;
            if (targetSlot == null) return;
            DropOn(card, targetSlot);
        }

        #region Where a card being played lands
        // The user: a card picked up from the hand (or a queued one moved) lands in the slot under the pointer, or failing
        // that the nearest one it can go to within Drop Reach of it (a slider), whatever covers the board there (the hand's
        // own area reaches over the back row): the slots come first. Right on a slot it can't go to, nowhere. The card only
        // settles into the slot while the pointer is on the slot itself: near it, the slot lights up and the card follows the
        // pointer (the user: no pull like a gridlock)
        private static float PlayTolerance => Tuning.dropReach;
        private static readonly List<BattlefieldUI> boards = new List<BattlefieldUI>();

        /// <summary>The slot the card being played would land in now (lit, the card settling into it), if any.</summary>
        public static BattlefieldSlot PlayTarget { get; private set; }
        private static CardController playTargetCard;
        private static int playTargetFrame = -1;

        /// <summary>Whether the card is being played: carried from a hand, or a queued one moved to aim it elsewhere.</summary>
        public static bool IsPlayed(CardController card) => card != null && (card.IsQueued || FromHand(card));

        /// <summary>Plays the carried card in the slot picked for it, if there is one; true if it did.</summary>
        public static bool PlayOnTarget(CardController card) {
            if (card == null || card != playTargetCard || PlayTarget == null) return false;
            var slot = PlayTarget;
            ClearPlayTarget();
            var board = slot.GetComponentInParent<BattlefieldUI>();
            if (board == null) return false;
            board.DropOn(card, slot);
            return true;
        }

        private static void ClearPlayTarget() {
            if (PlayTarget != null && playTargetCard != null) PlayTarget.ShowPlayTarget(playTargetCard, false);
            PlayTarget = null;
            playTargetCard = null;
        }

        // Once a frame, whichever board asks first
        private static void UpdatePlayTarget() {
            if (playTargetFrame == Time.frameCount) return;
            playTargetFrame = Time.frameCount;
            var card = CardController.Dragged;
            if (card != null && card.DragPointer == null) return; // Not moved yet (or a simulated drag): entering a slot sets it
            var (target, onIt) = IsPlayed(card) ? FindPlayTarget(card, card.DragPointer.Value) : (null, false);
            if (target != PlayTarget || (target != null && card != playTargetCard)) {
                ClearPlayTarget();
                if (target == null) return;
                PlayTarget = target;
                playTargetCard = card;
                target.ShowPlayTarget(card, true);
            }
            if (target != null) target.SnapCard(card, true); // Its size only: it keeps following the pointer (the user)
        }

        /// <summary>The pointer is on this slot, carrying a card being played (the slot's own events): it lands here if it can.</summary>
        public static void PointerOnSlot(BattlefieldSlot slot, CardController card, bool on) {
            if (card == null || card.DragPointer != null) return; // A real drag: picked each frame, near slots too
            if (!on) {
                if (PlayTarget == slot) ClearPlayTarget();
                return;
            }
            var board = slot.GetComponentInParent<BattlefieldUI>();
            if (board == null || board.AimOf(card) != DropAim.Slot || !board.CanAimAt(card, slot.Model)) return;
            ClearPlayTarget();
            PlayTarget = slot;
            playTargetCard = card;
            slot.ShowPlayTarget(card, true);
            slot.SnapCard(card, true);
        }

        // The slot it would land in, and whether the pointer is on that slot itself (not only near it)
        private static (BattlefieldSlot slot, bool onIt) FindPlayTarget(CardController card, Vector2 pointer) {
            if (pointer.x < 0f || pointer.y < 0f || pointer.x > Screen.width || pointer.y > Screen.height) return (null, false);
            BattlefieldSlot best = null;
            float nearest = float.MaxValue;
            float reach = 1f + 2f * PlayTolerance; // In half a slot's size from its middle
            var corners = new Vector3[4];
            foreach (var board in boards) {
                if (board == null || !board.IsInitialized || board.AimOf(card) != DropAim.Slot) continue;
                var eventCamera = CanvasUtility.GetEventCamera(board);
                foreach (var slot in board.battlefieldSlots) {
                    if (slot == null || slot.Model == null) continue;
                    ((RectTransform)slot.transform).GetWorldCorners(corners);
                    Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
                    Vector2 max = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
                    var half = (max - min) / 2f;
                    if (half.x <= 0f || half.y <= 0f) continue;
                    var off = pointer - (min + half);
                    float dx = Mathf.Abs(off.x) / half.x, dy = Mathf.Abs(off.y) / half.y;
                    bool fits = board.CanAimAt(card, slot.Model);
                    if (dx <= 1f && dy <= 1f) return (fits ? slot : null, fits);
                    if (!fits || dx > reach || dy > reach) continue;
                    float distance = Mathf.Max(dx, dy);
                    if (distance < nearest) {
                        nearest = distance;
                        best = slot;
                    }
                }
            }
            return (best, false);
        }
        #endregion

        /// <summary>The card released on this slot: played there from the hand, or a creature's attack or move (tests call it directly).</summary>
        /// <summary>Whether this card, carried from the hand, can be played on this slot: for an aimed spell, only where it lands on something.</summary>
        public bool CanAimAt(CardController card, BoardSlot slot) =>
            PlayRestrictions.Why(card?.GetLinkedCardInstance(), card?.Player, GameManager?.TurnManager?.TurnNumber ?? 0) == null // The DMCA
            && (!(card?.GetLinkedCardInstance() is Spell spell) || !EffectResolver.IsAimed(spell)
                || EffectResolver.CanAimAt(spell, card.Player, slot, GameManager?.ActionsQueue));

        public void DropOn(CardController card, BattlefieldSlot targetSlot) {
            if ((IsCardFromHand(card) || card.IsQueued) && !CanAimAt(card, targetSlot.Model)) {
                Log($"{card.GetLinkedCardInstance()?.Name} can't be aimed at {targetSlot.Model}", LogTag.Actions | LogTag.Cards);
                return;
            }
            if (IsCardFromHand(card) || card.IsQueued) {
                HandleCardFromHand(card, targetSlot);
            }
            else {
                HandleCardFromBattlefield(card, targetSlot);
            }
            Tooltip.Existing?.SkipHoverAfterDrop(); // The card under the pointer doesn't lift until the pointer leaves it (the user)
        }

        private bool IsCardFromHand(CardController card) => FromHand(card);

        private static bool FromHand(CardController card) {
            if (card.OriginalParent == null) return false;

            Transform parent = card.OriginalParent;
            while (parent != null) {
                if (parent.GetComponent<HandUI>() != null) return true;
                parent = parent.parent;
            }
            return false;
        }

        // The card is always played by the player whose hand it came from; the slot only decides where it lands:
        // creatures are summoned into it, spells resolve against its creature or its owner (see Spell.ResolveTarget).
        private void HandleCardFromHand(CardController card, BattlefieldSlot targetSlot) {
            var cardToPlay = card.GetLinkedCardInstance();
            // A creature dropped on one queued here replaces it (the user): that one goes back to the hand. One summoned by an
            // effect, not played, can't be taken back: the drop is ignored
            if (cardToPlay is ICreature && queuedGhosts.TryGetValue(targetSlot, out var ghost) && ghost.GetLinkedCardInstance() is ICard queuedThere
                && queuedThere != cardToPlay) {
                // A queued creature dragged onto another queued one: they trade slots (the user), nobody goes back to the hand
                var mine = QueuedPlayOf(cardToPlay);
                var theirs = QueuedPlayOf(queuedThere);
                if (card.IsQueued && mine?.GetTarget() is BoardSlot from && from != targetSlot.Model && theirs != null && from.Owner == targetSlot.Model.Owner) {
                    GameManager.ActionsQueue.CancelPlay(queuedThere);
                    GameManager.ActionsQueue.AddAction(new PlayCardAction(queuedThere, theirs.GetOwner(), from));
                }
                else if (!GameManager.ActionsQueue.CancelPlay(queuedThere)) {
                    Log($"Slot already has a creature on its way that can't be taken back; ignoring drop of {cardToPlay.Name}", LogTag.Actions | LogTag.Cards);
                    return;
                }
            }
            if (cardToPlay is ICreature { IsGift: true } && targetSlot.Model.IsOccupied()) {
                Log($"A Gift needs an empty slot; ignoring drop of {cardToPlay.Name}", LogTag.Actions | LogTag.Cards);
                return;
            }
            PlayFromHand(card, targetSlot.Model);
        }

        // Queues the play of a hand card at what it was dropped on: a slot, or the player of a side
        private void PlayFromHand(CardController card, ITarget droppedOn) {
            var caster = card.Player;
            var cardToPlay = card.GetLinkedCardInstance();
            if (caster == null || cardToPlay == null || !caster.Hand.Contains(cardToPlay)) {
                LogWarning($"Dropped card {card.GetBaseCardData()?.cardName} is not in its player's hand; ignoring drop.", LogTag.Actions | LogTag.Cards);
                return;
            }

            Log($"Adding PlayCardAction for {cardToPlay.Name} on {droppedOn.TargetId.ToUpper()}", LogTag.Actions | LogTag.Cards);
            GameManager.ActionsQueue.CancelPlay(cardToPlay); // A queued card dragged to a new target replaces its play
            var aim = droppedOn is BoardSlot slot ? EffectResolver.AimAt(cardToPlay, caster, slot, GameManager.ActionsQueue) : droppedOn;
            GameManager.ActionsQueue.AddAction(new PlayCardAction(cardToPlay, caster, aim));
        }

        private void HandleCardFromBattlefield(CardController card, BattlefieldSlot targetSlot) {
            var creature = card.GetLinkedCreature();
            if (creature == null || targetSlot == null) return;

            // Shift-drag: an Active ability (like Identity Theft) aimed at the creature there, as its action for the Day.
            // A Bodyguard needs no Shift: dragged onto another friendly creature, it guards it
            if (Abilities.IsActiveAbility(creature.Ability) && (UsingAbility() || Abilities.UsedByPlainDrag(creature, targetSlot.Model))) {
                if (GameManager.ModifierManager.CanAct(creature) && Abilities.CanUseActive(creature, GameManager.ModifierManager)
                    && targetSlot.Model.OccupyingCreature != creature) {
                    GameManager.ActionsQueue.AddAction(new UseAbilityAction(creature, targetSlot.Model));
                }
                return;
            }

            // Friendly Fire (right-drag): it attacks the creature of its own side it was dropped on, Last Will and all
            if (card.IsFriendlyFire) {
                card.IsFriendlyFire = false;
                if (creature.Owner == Player && targetSlot.Model.OccupyingCreature is ICreature victim && victim != creature) {
                    GameManager.CombatHandler.HandleCreatureCombat(creature, targetSlot.Model);
                }
                return;
            }

            if (creature.Owner != Player) {
                // Still deploying, it could be picked up to move, not to attack: say so
                if (!GameManager.ModifierManager.CanAct(creature)) ShowCantAct(card, CantActText(creature));
                else GameManager.CombatHandler.HandleCreatureCombat(creature, targetSlot.Model);
            }
            else if (!GameManager.ModifierManager.CanMove(creature) && targetSlot.Model != creature.Slot) {
                ShowCantAct(card, CantMoveText(creature)); // Picked up to attack, dropped on its own side: the drop is refused, and says why
            }
            else {
                HandleCreatureMove(creature, targetSlot.Model);
            }
        }

        /// <summary>Whether the drop uses the creature's Active ability instead of attacking or moving. Tests can replace it.</summary>
        public static System.Func<bool> UsingAbility = () => GameInput.ShiftHeld;

        private void HandleCreatureMove(ICreature creature, BoardSlot targetSlot) {
            if (!GameManager.ModifierManager.CanMove(creature)) return;

            var sourceSlot = creature.Slot;
            if (sourceSlot == null || sourceSlot == targetSlot) return;

            // A move that can't work in any order can't be planned (GDD 2.4 Impossible Plans)
            var (_, blocked, hint) = MovementRules.Evaluate(creature, targetSlot, GameManager.ActionsQueue);
            if (blocked) {
                Log($"{creature.Name} can't move to {targetSlot}: {hint}", LogTag.UI | LogTag.Creatures);
                return;
            }
            GameManager.ActionsQueue.AddAction(new MoveCreatureAction(creature, sourceSlot, targetSlot, Player,
                MovementRules.KindOf(creature, targetSlot, GameManager.ActionsQueue)));
        }

        private BattlefieldSlot GetTargetSlot() {
            var pointerEventData = new PointerEventData(EventSystem.current) {
                position = GameInput.PointerPosition
            };

            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            foreach (var result in raycastResults) {
                var slot = result.gameObject.GetComponent<BattlefieldSlot>();
                if (slot != null) return slot;
            }

            return null;
        }
        #endregion
    }
}
