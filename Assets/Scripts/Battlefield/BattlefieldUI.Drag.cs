using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>The battlefield: dragging creatures and cards over it (a part of BattlefieldUI).</summary>
    public partial class BattlefieldUI {

        #region Drag Handling
        private const float DragArrowClearance = 0.55f; // Of the card's height, from its slot's middle: no arrow nearer

        protected override void OnCardBeginDrag(CardController card) {
            if (card == null) return;

            var startSlot = GetSlot(card);
            // No arrow until the pointer is off the creature's own card, about: a few pixels' drag draws nothing
            var cardRect = (RectTransform)card.transform;
            float clear = DragArrowClearance * cardRect.rect.height * cardRect.lossyScale.y;
            arrowManager.ShowDragArrow(startSlot != null ? startSlot.transform.position : card.transform.position, clear);
            draggedCard = card;
            card.transform.SetAsLastSibling();
            ShowMoveHints(card.GetLinkedCreature());
        }

        /// <summary>The last reason a creature picked up couldn't act (shown over it), for tests.</summary>
        public string CantActReason { get; private set; }

        // A creature of ours that can do nothing this Day, picked up, isn't picked up (the user): it stays in its slot, shakes
        // its head (side to side, a "no no") and says why over everything else, as the hand does for a card it can't play.
        // One still deploying can be picked up: it can move, not attack
        private bool RefuseIfItCantAct(CardController card) {
            var creature = card.GetLinkedCreature();
            if (creature?.Slot == null || creature.Owner != Player || !FogOfWar.CanControl(Player)) return false;
            if (GameManager.ActionsQueue?.IsLooping(creature) == true) { // Groundhog Day: its plan is made for it
                ShowCantAct(card, "Groundhog Day: it does today what it did yesterday");
                return true;
            }
            if (GameManager.ModifierManager.CanMove(creature)) return false;
            ShowCantAct(card, MovementRules.IsRooted(creature, GameManager.ModifierManager) && GameManager.ModifierManager.CanAct(creature)
                ? (Abilities.IsPinned(creature) ? "Clingy Ex: it can't move while it's faced" : "Rooted: it can't move") : CantActText(creature));
            return true;
        }

        private static string CantActText(ICreature creature) {
            int days = creature.TurnsUntilOperative;
            return !creature.IsOperative ? $"Still deploying: it can move, and attack in {days} Day{(days == 1 ? "" : "s")}" : "It can't act this Day";
        }

        private void ShowCantAct(CardController card, string reason) {
            CantActReason = reason;

            const string ShakeId = "CantActShake";
            DOTween.Complete(card.transform.GetInstanceID() + ShakeId); // Asked again mid-shake: it starts over from its place
            card.transform.DOPunchPosition(new Vector3(9f, 0f, 0f), 0.45f, 8, 0.6f).SetId(card.transform.GetInstanceID() + ShakeId);

            // A dark pill on the root canvas, last, with its own sorting: readable over the other side, the river, the hand
            // and any card shown enlarged
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var old = canvas.transform.Find("CantAct");
            if (old != null) Destroy(old.gameObject);
            var pill = UITheme.NewImage("CantAct", canvas.transform, UITheme.Hex(0x0A0F1C, 0.9f));
            UITheme.Round(pill, pill.color, 3f);
            pill.raycastTarget = false;
            var top = pill.gameObject.AddComponent<Canvas>();
            top.overrideSorting = true;
            top.sortingOrder = UITheme.WindowsLayer; // Over the hands too
            var fade = pill.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            var note = UITheme.NewText("Text", pill.transform, CantActReason, 24f, UITheme.Hex(0xFF8A7A));
            note.fontStyle = TMPro.FontStyles.Bold;
            note.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            note.alignment = TMPro.TextAlignmentOptions.Center;
            note.raycastTarget = false;
            UITheme.Stretch(note.rectTransform, 0f);
            var rect = pill.rectTransform;
            rect.localScale = Vector3.one * (transform.lossyScale.x / canvas.transform.lossyScale.x); // The board's size of text
            rect.sizeDelta = new Vector2(note.GetPreferredValues().x + 36f, 44f);
            rect.SetAsLastSibling();
            float lift = BattlefieldSlot.Size.y * SlotScale * 0.6f * transform.lossyScale.y;
            rect.position = card.transform.position + new Vector3(0f, lift, 0f);
            rect.DOMoveY(rect.position.y + 30f * transform.lossyScale.y, 1.8f).SetEase(Ease.OutCubic).SetTarget(pill);
            DOTween.To(() => fade.alpha, a => fade.alpha = a, 0f, 0.6f).SetDelay(1.4f).SetTarget(pill).OnComplete(() => { if (pill != null) Destroy(pill.gameObject); });
        }

        // Every slot of the row says what dropping the creature there would do, given this side's other planned moves
        private void ShowMoveHints(ICreature creature) {
            if (creature?.Slot == null || creature.Owner != Player || !GameManager.ModifierManager.CanMove(creature)) return;
            foreach (var slot in battlefieldSlots) {
                if (slot.Model == creature.Slot) continue;
                var (kind, blocked, hint) = MovementRules.Evaluate(creature, slot.Model, GameManager.ActionsQueue);
                slot.ShowMoveHint(kind, blocked, hint);
            }
        }

        private void ClearMoveHints() {
            foreach (var slot in battlefieldSlots) slot.ClearMoveHint();
        }

        // The drag arrow follows the pointer and shows what releasing there would do
        private void Update() {
            if (draggedCard == null) return;

            var eventCamera = CanvasUtility.GetEventCamera(this);
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform, GameInput.PointerPosition, eventCamera, out var pointer)) {
                arrowManager.UpdateDragArrow(pointer, GetDragIntent(draggedCard, GetTargetSlot()));
            }
        }

        private BattlefieldArrowManager.DragIntent GetDragIntent(CardController card, BattlefieldSlot slotUnderPointer) {
            if (slotUnderPointer == null || slotUnderPointer == GetSlot(card)) return BattlefieldArrowManager.DragIntent.None;
            if (card.IsFriendlyFire) { // Only onto another creature of its own side
                return slotUnderPointer.Owner == Player && slotUnderPointer.Model.IsOccupied() ? BattlefieldArrowManager.DragIntent.Attack : BattlefieldArrowManager.DragIntent.None;
            }
            return slotUnderPointer.Owner == Player ? BattlefieldArrowManager.DragIntent.Move : BattlefieldArrowManager.DragIntent.Attack;
        }

        protected override void OnCardEndDrag(CardController card) {
            arrowManager.HideDragArrow();
            draggedCard = null;
            ClearMoveHints();

            // Released anywhere but another slot (including back on its own): the creature stands down. Called off with a
            // right click, its plan stays as it was
            if (!card.DragCancelled && GetDragIntent(card, GetTargetSlot()) == BattlefieldArrowManager.DragIntent.None && !GameManager.TurnManager.IsResolving) {
                GameManager.CombatHandler.CancelAttack(card.GetLinkedCreature());
            }
            UpdateUI(Player);
        }
        #endregion
    }
}
