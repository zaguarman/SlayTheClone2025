using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// On-screen view of one <see cref="BoardSlot"/>: shows the card of the creature the rules placed there
    /// and takes pointer input. Rules never touch it; <see cref="BattlefieldUI"/> keeps it in sync with the model.
    /// </summary>
    public class BattlefieldSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        private RectTransform rectTransform;
        private Image backgroundImage;

        public BoardSlot Model { get; private set; }
        public string TargetId => Model?.TargetId;
        public IPlayer Owner => Model?.Owner; // Player whose battlefield this slot belongs to
        public ICreature OccupyingCreature => Model?.OccupyingCreature;
        public CardController OccupyingCard { get; private set; } // The card currently shown here

        // A card-shaped space on the mat. At rest it is invisible over the slot marker the mat already draws there
        // (see BoardSkin); it lights up under the pointer when a creature stands here, and turns brass under a dragged
        // card that can be dropped here
        /// <summary>A card's own size (the card prefab's), before any scale.</summary>
        public static readonly Vector2 CardSize = new Vector2(160f, 200f);

        /// <summary>
        /// A card's scale in a slot: as big as fits it, up to the slot's markers (the user: legible without hovering), times
        /// the Card Scale slider (UI > Battlefield Tuning).
        /// </summary>
        public static float CardScale => BattlefieldTuning.Current.cardScale * Mathf.Min(Size.x / CardSize.x, Size.y / CardSize.y);

        public static Vector2 Size => new Vector2(172f * BattlefieldTuning.Current.slotWidth, 212f * BattlefieldTuning.Current.slotHeight); // A card's, by the sliders (UI > Battlefield Tuning)
        public static readonly Color MarkerColor = UITheme.Hex(0xE2B04A, 0.6f); // The corner brackets of a slot marker
        private static readonly Color HoverColor = UITheme.Hex(0xFFFFFF, 0.06f);
        private static readonly Color HoverTrimColor = UITheme.Hex(0xE2B04A, 0.95f);
        private static readonly Color DragTargetColor = UITheme.Hex(0xE2B04A, 0.3f);
        private static readonly Color DragTargetTrimColor = UITheme.Hex(0xFFE08A, 1f);

        private Image trim;
        private bool isMarkedForCombat;

        // While one of this side's creatures is dragged: what dropping it here would do (GDD 2.4 Choosing the Move Type)
        private static readonly Color WalkHintColor = UITheme.Hex(0x4CC46A, 0.22f);
        private static readonly Color PushHintColor = UITheme.Hex(0xF0A040, 0.24f);
        private static readonly Color LeapfrogHintColor = UITheme.Hex(0x4FC3F7, 0.24f);
        private static readonly Color SwapHintColor = UITheme.Hex(0xB388FF, 0.24f);
        private static readonly Color BlockedHintColor = UITheme.Hex(0x3A181C, 0.55f);
        private Color? hintFill;
        private Color? hintTrim;
        // While an aimed spell is carried: a slot it can be played on glows softly
        private static readonly Color EligibleColor = UITheme.Hex(0xE2B04A, 0.14f);
        private static readonly Color EligibleTrimColor = UITheme.Hex(0xFFE08A, 0.7f);
        private bool isEligible;
        // Where the action hovered in the queue lands (see BattlefieldArrowManager.Focus): lit up like a drop target
        private bool isFocused;

        /// <summary>Lights the slot up as where the action hovered in the queue lands.</summary>
        public void SetFocused(bool focused) {
            if (isFocused == focused) return;
            isFocused = focused;
            ShowHighlight(null);
        }

        /// <summary>Whether this slot glows as one the carried card can be played on.</summary>
        public bool IsEligible => isEligible;

        public void SetEligible(bool eligible) {
            if (isEligible == eligible) return;
            isEligible = eligible;
            ShowHighlight(null);
        }
        private TextMeshProUGUI hintLabel;

        // The Slot Effect on this slot, as a badge just under the slot, clear of the card's stats
        private TextMeshProUGUI effectBadge;
        private static readonly Color EffectBadgeColor = UITheme.Hex(0x1B1B22, 0.82f);

        /// <summary>The text of the Slot Effect badge, empty when none is shown.</summary>
        public string SlotEffectText => effectBadge != null && effectBadge.transform.parent.gameObject.activeSelf ? effectBadge.text : "";

        /// <summary>The move hint shown here, if any: its kind and whether it's blocked.</summary>
        public (MoveKind kind, bool blocked)? MoveHint { get; private set; }

        private void Awake() {
            rectTransform = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            backgroundImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        }

        public void Initialize(BoardSlot model) {
            Model = model;
            rectTransform.sizeDelta = Size;
            UITheme.Round(backgroundImage, Color.clear, 1.6f);
            trim = UITheme.AddTrim(transform, Color.clear, 1f, -3f);
            trim.sprite = UITheme.CornerBrackets; // Zone markers, like a playmat's
            ResetVisuals();
        }

        public void SetPosition(Vector2 position) {
            if (rectTransform != null) {
                rectTransform.anchoredPosition = position;
            }
        }

        /// <summary>Shows the card here, centered and upright.</summary>
        public void ShowCard(CardController card) {
            OccupyingCard = card;
            if (card == null) return;

            card.transform.SetParent(transform, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one * CardScale; // A slider: the card's size in its slot
        }

        /// <summary>Stops showing the current card and returns it; the caller decides what happens to it.</summary>
        public CardController ReleaseCard() {
            var card = OccupyingCard;
            OccupyingCard = null;
            if (card != null) card.SetReplacementPreview(false);
            return card;
        }

        public void ResetVisuals() {
            isMarkedForCombat = false;
            ShowHighlight(null);
        }

        // Null for the resting look: the move hint while there is one
        private void ShowHighlight(Color? fill, Color? trimColor = null) {
            if (backgroundImage != null) backgroundImage.color = fill ?? hintFill ?? (isFocused ? DragTargetColor : isEligible ? EligibleColor : isMarkedForCombat ? UITheme.SlotCombat : Color.clear);
            if (trim != null) trim.color = trimColor ?? hintTrim ?? (isFocused ? DragTargetTrimColor : isEligible ? EligibleTrimColor : Color.clear);
        }

        /// <summary>Shows the slot's Slot Effect, or nothing. Hidden ones are only shown to their controller, marked as hidden.</summary>
        public void ShowSlotEffect(SlotEffect effect, bool isHiddenFromViewer, int today = 0) {
            bool show = effect != null && !isHiddenFromViewer;
            if (!show) {
                if (effectBadge != null) effectBadge.transform.parent.gameObject.SetActive(false);
                return;
            }
            if (effectBadge == null) {
                var strip = new GameObject("SlotEffect", typeof(RectTransform), typeof(Image));
                strip.transform.SetParent(transform, false);
                var rect = (RectTransform)strip.transform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, 24f);
                rect.anchoredPosition = new Vector2(0f, -2f);
                UITheme.Round(strip.GetComponent<Image>(), EffectBadgeColor, 1.6f);
                strip.GetComponent<Image>().raycastTarget = false;
                effectBadge = UITheme.NewText("Label", strip.transform, "", 16f, UITheme.Parchment);
                UITheme.Stretch(effectBadge.rectTransform, 2f);
            }
            string icon = EffectIcons.Tag(CardTextFormatter.SlotEffectIcon(effect.Type));
            int left = effect.DaysLeft(today); // An hourglass with the Days it still lasts, like a card's Deployment Time
            string days = left > 0 ? $"  {EffectIcons.Tag(EffectIcons.Days)}{left}" : "";
            effectBadge.text = $"{icon}{effect.Name}{(effect.IsHidden ? " <i>(hidden)</i>" : "")}{days}";
            effectBadge.transform.parent.gameObject.SetActive(true);
            effectBadge.transform.parent.SetAsLastSibling(); // Over the card standing here
        }

        /// <summary>Shows what dropping the dragged creature here would do: walk, push, Leapfrog, Swap, or blocked with a hint.</summary>
        public void ShowMoveHint(MoveKind kind, bool blocked, string hint) {
            MoveHint = (kind, blocked);
            hintFill = blocked ? BlockedHintColor : kind switch {
                MoveKind.Leapfrog => LeapfrogHintColor,
                MoveKind.Swap => SwapHintColor,
                _ => WalkHintColor
            };
            hintTrim = UITheme.WithAlpha(hintFill.Value, blocked ? 0.5f : 0.9f);

            string label = blocked ? $"Blocked\n<size=75%>{hint}</size>" : kind == MoveKind.Walk ? "" : kind.ToString();
            if (hintLabel == null) {
                hintLabel = UITheme.NewText("MoveHint", transform, "", 22f, UITheme.Parchment);
                hintLabel.textWrappingMode = TextWrappingModes.Normal;
                UITheme.Stretch(hintLabel.rectTransform, 8f);
                UITheme.AddDropShadow(hintLabel);
            }
            hintLabel.text = label;
            hintLabel.gameObject.SetActive(label.Length > 0);
            hintLabel.transform.SetAsLastSibling(); // Over the card standing here
            ShowHighlight(null);
        }

        public void ClearMoveHint() {
            MoveHint = null;
            hintFill = null;
            hintTrim = null;
            if (hintLabel != null) hintLabel.gameObject.SetActive(false);
            ShowHighlight(null);
        }

        public void OnPointerEnter(PointerEventData eventData) {
            var dragged = eventData?.pointerDrag != null ? eventData.pointerDrag.GetComponent<CardController>() : null;
            if (dragged != null) {
                // A card being played: the board picks its slot, near the pointer as well as under it (see ShowPlayTarget).
                // A creature moving or attacking lights up a slot of this side it's aimed at
                var board = GetComponentInParent<BattlefieldUI>();
                if (BattlefieldUI.IsPlayed(dragged)) BattlefieldUI.PointerOnSlot(this, dragged, true);
                else if (board?.AimOf(dragged) == BattlefieldUI.DropAim.Slot && board.CanAimAt(dragged, Model)) {
                    ShowHighlight(DragTargetColor, DragTargetTrimColor);
                }
            }
            else if (OccupyingCard != null) ShowHighlight(HoverColor, HoverTrimColor); // An empty slot stays plain

            if (IsOverQueuedGhost(eventData)) return; // The ghost of a creature queued here shows its own preview

            if (OccupyingCard != null) Tooltip.Shared.ShowTooltip(OccupyingCard); // An empty slot has nothing to show
        }

        public void OnPointerExit(PointerEventData eventData) {
            var dragged = eventData?.pointerDrag != null ? eventData.pointerDrag.GetComponent<CardController>() : null;
            if (BattlefieldUI.IsPlayed(dragged)) BattlefieldUI.PointerOnSlot(this, dragged, false);
            // The slot picked for a card being played stays lit while the pointer is near it (see ShowPlayTarget)
            if (!IsPlayTarget) {
                if (OccupyingCard != null) OccupyingCard.SetReplacementPreview(false);
                ShowHighlight(null);
            }

            // Leaving a slot only lets go of what the tooltip shows: it closes once the pointer is away from it, not because
            // a keyword explanation opened over this slot and took the pointer
            if (OccupyingCard != null) Tooltip.Existing?.ReleaseCard(OccupyingCard);
            else Tooltip.Existing?.Release();
        }

        /// <summary>
        /// Picked by the board for the card being played (see <see cref="BattlefieldUI.PlayTarget"/>): lit, the card settling
        /// into it (the user), and a creature standing here shown as replaced by a creature coming in. Off again, as it was.
        /// </summary>
        public void ShowPlayTarget(CardController card, bool on) {
            IsPlayTarget = on;
            if (on) ShowHighlight(DragTargetColor, DragTargetTrimColor);
            else ShowHighlight(null);
            if (OccupyingCard != null && OccupyingCard != card) OccupyingCard.SetReplacementPreview(on && card.GetBaseCardData() is CreatureData);
            if (!on) SnapCard(card, false);
        }

        /// <summary>The card being played settles into this slot (the pointer is on it), or follows the pointer again.</summary>
        public void SnapCard(CardController card, bool snap) {
            if (card == null) return;
            if (snap) card.SnapsTo = transform;
            else if (card.SnapsTo == transform) card.SnapsTo = null;
        }

        public bool IsPlayTarget { get; private set; }

        // The pointer entered this slot by entering the ghost card of a creature queued to be summoned here
        private bool IsOverQueuedGhost(PointerEventData eventData) {
            var hovered = eventData?.pointerEnter != null ? eventData.pointerEnter.GetComponentInParent<CardController>() : null;
            return hovered != null && hovered != OccupyingCard && hovered.IsQueued;
        }

        public void MarkForCombat() {
            isMarkedForCombat = true;
            ShowHighlight(null);
        }
    }
}
