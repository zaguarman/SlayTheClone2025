using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Shows one player's battlefield. The board itself lives in <see cref="IPlayer.Battlefield"/>; this view
    /// mirrors it (one <see cref="BattlefieldSlot"/> per <see cref="BoardSlot"/>, one card per creature) and turns
    /// drags and drops into actions.
    /// </summary>
    public class BattlefieldUI : CardContainer {
        private const float SummonStagger = 0.15f;
        // The mat's measurements are sliders (UI > Battlefield Tuning)
        private static BattlefieldTuning Tuning => BattlefieldTuning.Current;
        private static float MatPadding => Tuning.matPadding; // Between the slots and the edge of the mat, at the back (the chevron keeps its distance)
        private static float NoseRoom => Tuning.noseRoom;     // Round the Vanguard, at the chevron's nose
        private static float RiverRoom => Tuning.riverRoom;   // The formation sits this far back from the middle of the table: the two noses keep apart
        private const float ReplacingGhostRaise = 0.3f; // Fraction of card height a ghost is raised when it will replace a creature
        private readonly List<BattlefieldSlot> battlefieldSlots = new List<BattlefieldSlot>();

        // Translucent previews of creatures queued to be summoned into this battlefield
        private readonly Dictionary<BattlefieldSlot, CardController> queuedGhosts = new Dictionary<BattlefieldSlot, CardController>();
        private readonly List<CardController> risingGhosts = new List<CardController>(); // Creatures that will rise (Revive, Phoenix Feather)
        private readonly List<CardController> moveGhosts = new List<CardController>(); // Where planned moves leave each creature
        private PlayCardAction draggedGhostPlay; // The play of the ghost being dragged, to tell whether the drop replaced it
        private float nextSummonTime;

        // Queued spells aimed here, each shown as a ghost where it's aimed (see RefreshSpellGhosts)
        private readonly Dictionary<ICard, CardController> spellGhosts = new Dictionary<ICard, CardController>();
        private const float SpellGhostScale = 0.5f;   // Small, on an edge, when something already stands where it's aimed
        private const float SpellGhostSpread = 0.45f; // Older ghosts at the same place step left by this share of their width
        private readonly Dictionary<ICard, int> spellGhostOrder = new Dictionary<ICard, int>(); // When each ghost appeared
        private int nextSpellGhost;

        // The cards whose effects are on each creature, tucked under its card like Magic's enchantments: each peeks out
        // above the one in front of it, just its name showing (see RefreshAttachments)
        private readonly Dictionary<BattlefieldSlot, List<CardController>> attachedViews = new Dictionary<BattlefieldSlot, List<CardController>>();
        private readonly List<GameObject> attachedBands = new List<GameObject>();
        private readonly List<(RectTransform band, CardController front, int depth)> strips = new List<(RectTransform, CardController, int)>();
        private RectTransform stripLayer;
        private readonly Dictionary<ICard, (IPlayer caster, BoardSlot slot)> spellGhostAims = new Dictionary<ICard, (IPlayer, BoardSlot)>();
        // The creature each of them homes on (your own, or one on its way to the slot), when it's a creature and not the slot
        private readonly Dictionary<ICard, ICreature> spellGhostHoming = new Dictionary<ICard, ICreature>();
        public const float StripStep = 0.14f; // How much of a tucked card shows above the one in front: its name
        public const int MaxStrips = 4;       // Shown tucked; hovering the creature and scrolling goes through them all

        private BattlefieldArrowManager arrowManager;
        private CardController draggedCard; // Creature of this board currently being dragged
        private TableViews table; // For slots on the other side of the table

        #region Initialization
        private void InitializeManagers(IPlayer player) {
            // Create ArrowManager using injected dependencies from UIComponent base class
            // Get ActionsQueue from the injected GameManager
            var actionsQueue = GameManager?.ActionsQueue;
            if (actionsQueue == null) {
                LogError("Cannot initialize BattlefieldArrowManager - ActionsQueue is null via GameManager", LogTag.Initialization);
                return;
            }
            arrowManager = new BattlefieldArrowManager(transform, GameMediator, actionsQueue, player, FindSlotView,
                side => table.GetBattlefield(side)?.transform, HiddenFromScreen, () => GameManager?.TurnManager?.IsResolving ?? false);
        }

        private void CreateSlots(IPlayer player) {
            foreach (var model in player.Battlefield) {
                GameObject slotObj = new GameObject($"Slot_{model.Index}", typeof(RectTransform));
                slotObj.transform.SetParent(transform, false);

                var slot = slotObj.AddComponent<BattlefieldSlot>();
                slot.Initialize(model);

                battlefieldSlots.Add(slot);
            }
            UpdateSlotPositions();
        }

        public void Initialize(IPlayer player, IGameMediator mediator, IGameManager manager, CardViews cardViews, TableViews tableViews) {
            table = tableViews ?? throw new System.ArgumentNullException(nameof(tableViews));
            base.Initialize(player, mediator, manager, cardViews);

            // Now initialize Battlefield specific things
            InitializeManagers(player); // Pass player
            CreateSlots(player);
            // The mats as the sliders say now, not as the scene was saved: a retune, once everything is up (the user: the
            // mat's old size covered the other side's middle slot until the sliders were touched)
            StartCoroutine(RetuneWhenUp());
            Log($"BattlefieldUI initialized for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Initialization);

            // Update UI based on initial state
            UpdateUI(Player);
        }

        private System.Collections.IEnumerator RetuneWhenUp() {
            yield return null;
            BattlefieldTuning.ApplyToTable();
        }

        // The mat only lights up under a card that plays on the whole side (see HandlePointerEnter)
        // The mat's rectangle stays invisible (it only catches drops): the chevron drawn on it is the mat (see ApplyMatLook)
        protected override DropZoneColors ZoneColors => new DropZoneColors(Color.clear, Color.clear, Color.clear, Color.clear);

        // A rounded mat just big enough for its row of slots
        protected override void StyleZone(Image zoneImage) => ApplyMatLook();

        /// <summary>
        /// Makes this battlefield's image a rounded mat just big enough for its row of slots.
        /// Also used in the editor, before there are any slots (see <see cref="BoardSkin"/>).
        /// </summary>
        public void ApplyMatLook() {
            var zoneImage = GetComponent<Image>();
            if (zoneImage != null) {
                zoneImage.color = Color.clear;
                // Not its rectangle (the user): it reaches past the river over the other side's Vanguard, and the other
                // side's over yours. The chevron takes drops and hovers for the mat, in its own shape
                zoneImage.raycastTarget = false;
            }
            int slotCount = Player?.Battlefield.Count ?? SlayTheClone.Player.BattlefieldSize;
            ((RectTransform)transform).sizeDelta = new Vector2((slotCount - 1) * ColumnSpacing + BattlefieldSlot.Size.x * SlotScale + 2 * MatPadding + 2 * Mathf.Abs(Tuning.sideways),
                BattlefieldSlot.Size.y * SlotScale + 2 * RowStep + 2 * MatPadding + 2 * RiverRoom);
            var fill = Chevron(MatFillName, 0);
            fill.TakesPointer = true;
            fill.SetShape(MatOutline());
            fill.color = UITheme.Mat;
        }

        public const string MatFillName = "MatChevron";

        /// <summary>The chevron round this side's formation, in the mat's space (see <see cref="ChevronGraphic"/>).</summary>
        public List<Vector2> MatOutline() {
            var positions = SlotPositions(SlayTheClone.Player.BattlefieldSize);
            return ChevronGraphic.Around(positions, BattlefieldSlot.Size * SlotScale / 2f, NoseRoom, MatPadding,
                transform.localPosition.y <= 0f ? 1f : -1f, Tuning.noseWidth, Tuning.tuckGap,
                Tuning.tipOut, Tuning.tipFront, Tuning.tipBack, Tuning.wingSize, Tuning.tuckWidth);
        }

        /// <summary>A chevron layer of the mat by name, made if missing, at this place among the mat's children.</summary>
        public ChevronGraphic Chevron(string layerName, int siblingIndex) {
            var found = transform.Find(layerName);
            var graphic = found != null ? found.GetComponent<ChevronGraphic>() : null;
            if (graphic == null) {
                var go = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                graphic = go.AddComponent<ChevronGraphic>();
            }
            // Over the whole mat (its points are from the mat's middle either way): the pointer is tested against it there
            var layerRect = (RectTransform)graphic.transform;
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.pivot = new Vector2(0.5f, 0.5f);
            layerRect.sizeDelta = Vector2.zero;
            layerRect.anchoredPosition = Vector2.zero;
            graphic.transform.SetSiblingIndex(siblingIndex);
            return graphic;
        }

        /// <summary>The slots (and the cards on them) are drawn this much smaller than a card in the hand (hovering enlarges).</summary>
        public static float SlotScale => Tuning.slotScale;
        // How far apart the formation's rows sit, up and down: the Vanguard nearest the middle of the table, Headquarters furthest
        private static float RowStep => Tuning.rowStep;
        private float ColumnSpacing => settings.spacing * Tuning.columnSpacing * SlotScale;

        /// <summary>
        /// Where each of this many slots sits on the mat: the formation (see <see cref="Formation"/>), five columns across,
        /// each row a step further from the middle of the table: a Λ on the bottom side, a V on the top one.
        /// </summary>
        public List<Vector2> SlotPositions(int slotCount) {
            float spacing = ColumnSpacing;
            float startX = -(slotCount - 1) * spacing / 2f + Tuning.sideways;
            float towardMiddle = transform.localPosition.y <= 0f ? 1f : -1f; // The bottom side's middle is up
            return Enumerable.Range(0, slotCount).Select(i => new Vector2(startX + spacing * Formation.Column(i),
                towardMiddle * ((1 - Formation.Depth(i)) * RowStep - RiverRoom))).ToList();
        }

        /// <summary>
        /// Lays the formation out again for the side of the table this mat is on now (the sides were switched, F8):
        /// the slots, their markers and every chevron drawn on the mat (fill, trims, a spell's glow) face the middle.
        /// </summary>
        public void Relayout() {
            UpdateSlotPositions();
            var outline = MatOutline();
            foreach (var chevron in GetComponentsInChildren<ChevronGraphic>(true)) chevron.Reshape(outline);
            var positions = SlotPositions(SlayTheClone.Player.BattlefieldSize);
            for (int i = 0; i < positions.Count; i++) {
                if (!(transform.Find($"SlotMark_{i}") is RectTransform mark)) continue;
                mark.anchoredPosition = positions[i];
                mark.sizeDelta = BattlefieldSlot.Size * SlotScale;
            }
        }

        /// <summary>A <see cref="BattlefieldTuning"/> slider moved: the mat takes its new size and shape, the slots their places, the
        /// cards played on them (their ghosts) their size, the arrows follow.</summary>
        public void Retune() {
            ApplyMatLook();
            Relayout();
            RefreshQueuedGhosts();
            arrowManager?.RefreshArrows();
        }

        /// <summary>A card on a slot, of the slot (a slider): the creatures standing there and the ghosts of those on their way.</summary>
        public static float CardInSlotScale => BattlefieldSlot.CardScale;

        private static BattlefieldSlot anySlot;

        /// <summary>A card's scale under this parent to show at a card-in-a-slot's size (1 when there's no board).</summary>
        public static float SlottedScaleUnder(Transform parent) {
            if (anySlot == null) anySlot = FindAnyObjectByType<BattlefieldSlot>();
            if (anySlot == null || parent == null) return 1f;
            return anySlot.transform.lossyScale.x * CardInSlotScale / Mathf.Max(1e-6f, parent.lossyScale.x);
        }

        private void UpdateSlotPositions() {
            var positions = SlotPositions(battlefieldSlots.Count);
            for (int i = 0; i < battlefieldSlots.Count; i++) {
                battlefieldSlots[i].SetPosition(positions[i]);
                battlefieldSlots[i].transform.localScale = Vector3.one * SlotScale;
                ((RectTransform)battlefieldSlots[i].transform).sizeDelta = BattlefieldSlot.Size;
                var card = battlefieldSlots[i].OccupyingCard;
                if (card != null && !DOTween.IsTweening(card.transform)) card.transform.localScale = Vector3.one * CardInSlotScale;
            }
        }
        #endregion

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
            if (fill != null) fill.color = lit ? UITheme.MatValid : UITheme.Mat;
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
            !(card?.GetLinkedCardInstance() is Spell spell) || !EffectResolver.IsAimed(spell)
            || EffectResolver.CanAimAt(spell, card.Player, slot, GameManager?.ActionsQueue);

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
                && queuedThere != cardToPlay && !GameManager.ActionsQueue.CancelPlay(queuedThere)) {
                Log($"Slot already has a creature on its way that can't be taken back; ignoring drop of {cardToPlay.Name}", LogTag.Actions | LogTag.Cards);
                return;
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
            GameManager.ActionsQueue.AddAction(new MoveCreatureAction(creature, sourceSlot, targetSlot, Player));
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

        #region UI Updates
        // Makes the slots show what the board model holds: cards follow their creatures between slots,
        // creatures new to the board get a card, and cards whose creature left play their exit
        public override void UpdateUI(IPlayer player) {
            if (!IsInitialized || Player == null) return;
            if (player != Player) return;

            var creaturesOnBoard = new HashSet<ICreature>(battlefieldSlots.Select(s => s.OccupyingCreature).Where(c => c != null));
            var reusableCards = new Dictionary<ICreature, CardController>();
            foreach (var slot in battlefieldSlots) {
                var card = slot.ReleaseCard();
                if (card == null) continue;

                var creature = card.GetLinkedCreature();
                if (creature != null && creaturesOnBoard.Contains(creature)) {
                    reusableCards[creature] = card;
                }
                else {
                    RemoveCard(card);
                    card.PlayLeavePlayAnimation(); // Fades out, then destroys itself
                }
            }

            foreach (var slot in battlefieldSlots) {
                var creature = slot.OccupyingCreature;
                if (creature == null) continue;

                if (!reusableCards.TryGetValue(creature, out var card)) {
                    card = CreateCreatureCard(creature, slot);
                    if (card == null) continue;
                    AddCard(card);
                }
                slot.ShowCard(card);
            }

            // Anything else still tracked isn't shown anywhere (cards leaving play destroy themselves)
            foreach (var card in cards.ToList()) {
                if (battlefieldSlots.Any(s => s.OccupyingCard == card)) continue;
                RemoveCard(card);
                if (card != null && !card.IsLeavingPlay) Destroy(card.gameObject);
            }

            // Slot Effects. Hidden ones are secret from the other side, unless revealed (Fog of War)
            foreach (var slot in battlefieldSlots) {
                var effect = slot.Model?.Effect;
                slot.ShowSlotEffect(effect, FogOfWar.Hides(Viewer, effect), GameManager?.TurnManager?.TurnNumber ?? 0);
            }
            RefreshHiddenPlans();
            RefreshSpellGhosts(); // What stands in the slots decides where they go
            RefreshIdle();

            UpdateLayout();
        }

        private CardController CreateCreatureCard(ICreature creature, BattlefieldSlot slot) {
            var owner = creature.Owner ?? Player;
            var card = CardViews.Create(creature, owner, slot.transform);
            if (card == null) {
                LogError($"BattlefieldUI: Failed to create card for {creature.Name}", LogTag.UI | LogTag.Creatures);
            }
            return card;
        }

        protected override void UpdateLayout() {
            // Intentionally left empty to prevent CardContainer from repositioning cards
            // Slots handle card positioning instead
        }
        #endregion

        #region Event Handling
        protected override void RegisterEvents() {
            if (GameMediator != null) {
                GameMediator.CreatureSummoned += OnCreatureSummoned;
                GameMediator.BattlefieldStateChanged += UpdateUI;
                GameMediator.ActionsQueueChanged += RefreshQueuedGhosts;
                GameMediator.ActionsQueueChanged += RefreshWatchdogs;
                GameMediator.ActionsQueueChanged += RefreshIdle;
                GameMediator.CreatureStatusChanged += OnCreatureStatusChanged;
                GameMediator.CreatureStatsChanged += OnCreatureStatsChanged;
            }
        }

        private void OnCreatureStatusChanged(ICreature creature, Enums.StatusEffectType status, bool applied) => RefreshAttachments();
        private void OnCreatureStatsChanged(ICreature creature) => RefreshAttachments();

        protected override void UnregisterEvents() {
            if (GameMediator != null) {
                GameMediator.CreatureSummoned -= OnCreatureSummoned;
                GameMediator.BattlefieldStateChanged -= UpdateUI;
                GameMediator.ActionsQueueChanged -= RefreshQueuedGhosts;
                GameMediator.ActionsQueueChanged -= RefreshWatchdogs;
                GameMediator.ActionsQueueChanged -= RefreshIdle;
                GameMediator.CreatureStatusChanged -= OnCreatureStatusChanged;
                GameMediator.CreatureStatsChanged -= OnCreatureStatsChanged;
            }
        }

        #region Fog of War
        // The player at the screen (player 1). What their side can't see is hidden from the screen (GDD 2.5)
        private IPlayer Viewer => FogOfWar.ViewerOf(Player);

        private bool HiddenFromScreen(IGameAction action) =>
            FogOfWar.Hides(Viewer, action, GameManager?.ActionsQueue, GameManager?.ModifierManager);

        // A creature the screen doesn't know is covered with "???"; one whose plans are hidden shows "???" across its card
        // instead of its arrows and move ghost
        private void RefreshHiddenPlans() {
            foreach (var slot in battlefieldSlots) {
                var card = slot.OccupyingCard;
                if (card == null) continue;
                var disguise = FogOfWar.DisguiseFor(Viewer, slot.OccupyingCreature); // A decoy shows its fake card instead of "???"
                card.SetDisguise(disguise);
                card.SetAnonymous(disguise == null && FogOfWar.HidesIdentity(Viewer, slot.OccupyingCreature));
                card.SetPlansHidden(FogOfWar.HidesPlansOf(slot.OccupyingCreature, Viewer, GameManager?.ActionsQueue, GameManager?.ModifierManager));
            }
        }

        /// <summary>The creatures of this row, and the queued summons, whose cards are covered with "???".</summary>
        public IEnumerable<ICard> AnonymousCards =>
            battlefieldSlots.Where(s => s.OccupyingCard != null && s.OccupyingCard.IsAnonymous).Select(s => (ICard)s.OccupyingCreature)
                .Concat(queuedGhosts.Values.Where(g => g != null && g.IsAnonymous).Select(g => g.GetLinkedCardInstance()));

        /// <summary>The creatures of this row whose cards show "???": they plan something the screen can't show.</summary>
        public IEnumerable<ICreature> CreaturesWithHiddenPlans =>
            battlefieldSlots.Where(s => s.OccupyingCard != null && s.OccupyingCard.ArePlansHidden).Select(s => s.OccupyingCreature);
        #endregion

        // This side's active Watchdogs, listed at the end of its row. The player at the screen sees the other side's only as
        // what they watch (GDD 3.11 Visibility), until revealed
        private TMPro.TextMeshProUGUI watchdogList;

        /// <summary>The Watchdog list shown at the end of this row, empty when there are none.</summary>
        public string WatchdogText => watchdogList != null && watchdogList.gameObject.activeSelf ? watchdogList.text : "";

        private void RefreshWatchdogs() {
            if (!IsInitialized || Player == null || battlefieldSlots.Count == 0) return;
            var mine = GameManager?.ActionsQueue?.Watchdogs.Where(w => w.Controller == Player).ToList();
            if (mine == null || mine.Count == 0) {
                if (watchdogList != null) watchdogList.gameObject.SetActive(false);
                return;
            }

            if (watchdogList == null) {
                var lastSlot = battlefieldSlots[battlefieldSlots.Count - 1].transform;
                watchdogList = UITheme.NewText("Watchdogs", lastSlot, "", 15f, UITheme.Parchment, TMPro.TextAlignmentOptions.TopLeft);
                watchdogList.textWrappingMode = TMPro.TextWrappingModes.Normal;
                var rect = watchdogList.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(190f, BattlefieldSlot.Size.y);
                rect.anchoredPosition = new Vector2(16f, 0f);
                UITheme.AddDropShadow(watchdogList);
            }
            watchdogList.text = "<b>Watchdogs</b>\n" + string.Join("\n", mine.Select(w => "- " + w.Describe(!FogOfWar.HidesCondition(Viewer, w))));
            watchdogList.gameObject.SetActive(true);
        }

        // A reminder before the Day ends: your creatures that could act (ready, free to, able to attack) with nothing planned.
        // Only on the side the screen plays, while it plans
        private void RefreshIdle() {
            if (!IsInitialized || Player == null) return;
            var queue = GameManager?.ActionsQueue;
            var modifiers = GameManager?.ModifierManager;
            bool planning = queue != null && modifiers != null && FogOfWar.CanControl(Player) && !MatchSetup.PlayedByAI(Player)
                            && !(GameManager.TurnManager?.IsResolving ?? false) && !(GameMediator?.IsGameOver ?? false);
            var pending = planning ? queue.GetPendingActions() : null;
            foreach (var slot in battlefieldSlots) {
                var card = slot.OccupyingCard;
                if (card == null) continue;
                var creature = slot.OccupyingCreature;
                card.SetIdle(planning && creature != null && !creature.IsDead && creature.IsOperative && modifiers.CanAct(creature)
                             && Abilities.CanAttack(creature) && !pending.Any(a => Intel.PlannerOf(a) == creature));
                card.SetDeploying(creature != null && !creature.IsDead && !creature.IsOperative && !card.IsAnonymous);
            }
        }

        private void RefreshQueuedGhosts() {
            if (!IsInitialized || Player == null) return;
            ClearQueuedGhosts();

            var pendingActions = GameManager?.ActionsQueue?.GetPendingActions();
            if (pendingActions == null) return;

            int order = 0; // The queue lists them in the order they resolve; the same Speed shares a number (the game settles that tie)
            int? lastSpeed = null;
            // Moves resolve before summons: a creature planned to step out of a slot leaves it free for one summoned there
            var afterMoves = MovementRules.Preview(Player.Battlefield, MovementRules.PlannedMovesOf(Player, GameManager.ActionsQueue)).FinalSlots;
            foreach (var action in pendingActions) {
                // A played creature stays previewed until its follow-up summon actually executes
                ICard card;
                ITarget target;
                if (action is PlayCardAction playAction) {
                    card = playAction.GetCard();
                    target = playAction.GetTarget();
                }
                else if (action is SummonCreatureAction summonAction) {
                    card = summonAction.GetCreature();
                    target = summonAction.GetTargetSlot();
                }
                else {
                    continue;
                }

                if (!(card is ICreature) || !(target is BoardSlot model) || model.Owner != Player) continue;
                var slot = battlefieldSlots[model.Index];
                if (queuedGhosts.ContainsKey(slot)) continue;

                var ghost = CardViews.Create(card, Player, slot.transform);
                if (ghost == null) continue;

                ghost.name = $"QueuedGhost_{card.Name}";
                ghost.transform.localRotation = Quaternion.identity;
                ghost.transform.localScale = Vector3.one * CardInSlotScale; // As big as the card it'll be
                // Hoverable for its enlarged preview; drops still reach the slot (they bubble up, and targets come from a full raycast).
                // Like a queued card in the hand, clicking it or dragging it away takes the play back, and dragging it to a slot re-aims it.
                ghost.SetQueued(true, canBeDragged: true, badge: Abilities.ArrivesLate(action) ? "After the attacks" : "Queued"); // Sorry I'm Late, He Protec
                ghost.OnCardClicked.AddListener(TakeBackPlay);
                ghost.OnCardRightClicked.AddListener(TakeBackPlay);
                ghost.OnBeginDragEvent.AddListener(OnGhostBeginDrag);
                ghost.OnEndDragEvent.AddListener(OnGhostEndDrag);
                ghost.SetAnonymous(FogOfWar.HidesSummon(Viewer, action, GameManager.ActionsQueue)); // Something is coming, but what?
                int speed = ((ICreature)card).Speed;
                if (lastSpeed != speed) order++;
                lastSpeed = speed;
                ghost.SetOrderNumber(ghost.IsAnonymous ? (int?)null : order); // Arrivals happen in turn, by Speed
                queuedGhosts[slot] = ghost;

                // The creature currently in the slot will be replaced when the summon resolves (unless it moves out first):
                // raise the ghost so both cards and the occupant's REPLACE badge stay visible
                var occupant = slot.OccupyingCard;
                var standing = slot.OccupyingCreature;
                if (standing != null && afterMoves.TryGetValue(standing, out var willStand) && willStand != slot.Model) occupant = null;
                float raise = occupant != null ? ((RectTransform)ghost.transform).rect.height * CardInSlotScale * ReplacingGhostRaise : 0f;
                ghost.transform.localPosition = new Vector3(0f, raise, 0f);
                occupant?.SetMarkedForReplacement(true);
            }

            ShowRisingGhosts();
            ShowMoveGhosts();
            RefreshHiddenPlans();
            RefreshSpellGhosts();
        }

        /// <summary>The faded cards of creatures waiting to rise again (Revive, Phoenix Feather), by slot.</summary>
        public IReadOnlyList<CardController> RisingGhosts => risingGhosts.Where(g => g != null).ToList();

        // A creature that will rise again waits in its slot, faded like a summon on its way, counting the Days. If something
        // stands there it rises in the first free slot instead, so it shows there
        private void ShowRisingGhosts() {
            int today = GameManager?.TurnManager?.TurnNumber ?? 0;
            foreach (var waiting in Player.GoneDark.Where(g => g.Reborn && g.Creature != null)) {
                int index = Mathf.Clamp(waiting.SlotIndex, 0, battlefieldSlots.Count - 1);
                var slot = Free(battlefieldSlots[index]) ? battlefieldSlots[index] : battlefieldSlots.FirstOrDefault(Free);
                if (slot == null) continue;
                var ghost = CardViews.Create(waiting.Creature, Player, slot.transform);
                if (ghost == null) continue;
                ghost.name = $"RisingGhost_{waiting.Creature.Name}";
                ghost.transform.localRotation = Quaternion.identity;
                ghost.transform.localScale = Vector3.one * CardInSlotScale;
                int days = waiting.ReturnsOnTurn - today;
                ghost.SetQueued(true, badge: days <= 1 ? "Rises at Dawn" : $"Rises in {days} Days");
                risingGhosts.Add(ghost);
            }

            bool Free(BattlefieldSlot slot) => slot.OccupyingCard == null && !queuedGhosts.ContainsKey(slot)
                                               && !risingGhosts.Any(g => g != null && g.transform.parent == slot.transform);
        }

        /// <summary>The ghosts of the queued spells aimed at this battlefield, by spell.</summary>
        public IReadOnlyDictionary<ICard, CardController> SpellGhosts => spellGhosts;

        // A queued spell leaves its caster's hand and shows as a ghost where it's aimed, so the board says what's coming:
        // in its slot when nothing stands there, otherwise small on the slot's edge that faces the other side; a spell that
        // plays on a whole side, small on the middle of that edge of the battlefield. Like a creature's ghost it's hovered
        // for its enlarged view, clicked to take the play back, and dragged to aim it elsewhere. Cast, it flies off in the
        // cast animation. An enemy's is a card back (Fog of War: where it's aimed is seen, not what it is); one the screen
        // can't see at all stays a card back in its caster's hand
        private void RefreshSpellGhosts() {
            if (!IsInitialized || Player == null || GameManager?.ActionsQueue == null) return;
            var aimed = new List<(Spell spell, IPlayer caster, BoardSlot slot, bool byRiver, bool hidden)>();
            spellGhostAims.Clear();
            spellGhostHoming.Clear();
            foreach (var play in GameManager.ActionsQueue.GetPendingActions().OfType<PlayCardAction>()) {
                if (!(play.GetCard() is Spell spell) || HiddenFromScreen(play)) continue;
                var side = BattlefieldArrowManager.AimOf(spell, play.GetOwner(), play.GetTarget(), out var slot);
                // Where it waits: aimed, on what it's aimed at; on one whole side, beside that battlefield; on both sides (Holy
                // War, a Trend), by the river, on its caster's half
                var caster = play.GetOwner();
                bool byRiver = side != null && slot == null && EffectResolver.Reaches(spell, caster, caster) && EffectResolver.Reaches(spell, caster, caster.Opponent);
                if (byRiver ? caster == Player : side == Player) {
                    // Fog of War: the enemy's shows its back, until a Reveal (or a Watchdog) shows it
                    bool hidden = FogOfWar.HidesHandOf(caster) && !GameManager.ActionsQueue.IsRevealedTo(Viewer, play);
                    aimed.Add((spell, caster, slot, byRiver, hidden));
                    spellGhostAims[spell] = (play.GetOwner(), slot);
                    if (play.GetTarget() is HomingTarget homing) spellGhostHoming[spell] = homing.Creature;
                }
            }

            // Gone from the queue: cast (it left its caster's hand), or taken back
            var stillAimed = new HashSet<ICard>(aimed.Select(a => (ICard)a.spell));
            foreach (var entry in spellGhosts.Where(e => !stillAimed.Contains(e.Key)).ToList()) {
                spellGhosts.Remove(entry.Key);
                var ghost = entry.Value;
                if (ghost == null) continue;
                Destroy(ghost.gameObject); // Cast: the presenter flies it from here to the stage (see ResolutionPresenter)
            }

            foreach (var (spell, caster, _, _, hidden) in aimed) {
                if (!spellGhosts.TryGetValue(spell, out var ghost) || ghost == null) {
                    ghost = CardViews.Create(spell, caster, transform); // The caster's card, wherever it's aimed
                    if (ghost == null) continue;
                    ghost.name = $"SpellGhost_{spell.Name}";
                    ghost.SetQueued(true, canBeDragged: true);
                    ghost.OnCardClicked.AddListener(TakeBackPlay);
                    ghost.OnCardRightClicked.AddListener(TakeBackPlay);
                    ghost.OnBeginDragEvent.AddListener(OnGhostBeginDrag);
                    ghost.OnEndDragEvent.AddListener(OnGhostEndDrag);
                    spellGhosts[spell] = ghost;
                    spellGhostOrder[spell] = nextSpellGhost++;
                }
                ghost.SetFaceDown(hidden);
            }
            foreach (var gone in spellGhostOrder.Keys.Where(k => !spellGhosts.ContainsKey(k)).ToList()) spellGhostOrder.Remove(gone);

            // At one place, the newest in the middle and the older ones stepping left, each partly under the next
            foreach (var group in aimed.Where(a => spellGhosts.ContainsKey(a.spell)).GroupBy(a => (a.slot, a.byRiver))) {
                var together = group.OrderBy(a => spellGhostOrder[a.spell]).ToList();
                for (int i = 0; i < together.Count; i++) {
                    var ghost = spellGhosts[together[i].spell];
                    if (CardController.Dragged == ghost) continue;
                    if (together[i].slot == null) PlaceInPile(ghost, together[i].byRiver, together.Count - 1 - i);
                    else PlaceSpellGhost(ghost, together[i].slot, together.Count - 1 - i, together.Count);
                }
            }
            RefreshAttachments();
        }

        /// <summary>The cards tucked under the creature in this slot (its stack), the nearest first; empty when none.</summary>
        public List<CardController> AttachedViews(BoardSlot slot) {
            var view = slot != null && slot.Index < battlefieldSlots.Count ? battlefieldSlots[slot.Index] : null;
            return view != null && attachedViews.TryGetValue(view, out var views) ? views.Where(v => v != null).ToList() : new List<CardController>();
        }

        // Above each creature, the cards whose effects are on it now (statuses and stat changes from other cards), then the
        // spells queued on it: their own ghosts, which can still be dragged away or clicked to take the play back. Each shows through a strip just above the card in front of it, its name
        // band only. Only so many show; the creature's card knows them all, for the preview
        private void RefreshAttachments() {
            if (!IsInitialized || Player == null) return;
            foreach (var ghost in spellGhosts.Values) { // A ghost tucked away goes back to the mat before its strip goes
                if (ghost == null) continue;
                ghost.TuckedUnder = null;
                if (stripLayer != null && ghost.transform.IsChildOf(stripLayer)) ghost.transform.SetParent(transform, false);
            }
            foreach (var band in attachedBands) if (band != null) Destroy(band);
            attachedBands.Clear();
            strips.Clear();
            attachedViews.Clear();
            var modifiers = GameManager?.ModifierManager;
            if (modifiers == null) return;

            foreach (var slot in battlefieldSlots) {
                var aimedHere = spellGhostAims.Where(e => e.Value.slot == slot.Model && spellGhosts.ContainsKey(e.Key))
                    .OrderBy(e => spellGhostOrder.TryGetValue(e.Key, out var order) ? order : 0).ToList();
                // The creature on its way here (its ghost): the spells that will land on it are tucked under it, in its stack (the user)
                if (queuedGhosts.TryGetValue(slot, out var incoming) && incoming != null) {
                    var onIt = aimedHere.Where(e => OnIncoming(e.Key, slot)).ToList();
                    incoming.AttachedCards = onIt.Select(q => q.Key).ToList();
                    incoming.AttachedOwners = onIt.Select(q => q.Value.caster).ToList();
                    incoming.AttachedHidden = onIt.Select(q => spellGhosts[q.Key] != null && spellGhosts[q.Key].IsFaceDown).ToList();
                    var ghostViews = new List<CardController>();
                    foreach (var spell in onIt.Select(q => q.Key)) {
                        if (ghostViews.Count >= MaxStrips + 2) break;
                        var view = spellGhosts[spell];
                        if (view == null || CardController.Dragged == view) continue;
                        Tuck(view, incoming, ghostViews.Count + 1);
                        ghostViews.Add(view);
                    }
                    aimedHere = aimedHere.Where(e => !OnIncoming(e.Key, slot)).ToList();
                    if (slot.OccupyingCard == null) attachedViews[slot] = ghostViews;
                }

                var front = slot.OccupyingCard;
                var creature = slot.OccupyingCreature;
                if (front == null || creature == null) continue;
                var attached = modifiers.AttachmentsOf(creature).Where(a => a.Card?.SourceData != null && CanSee(a, creature)).ToList();
                var queued = aimedHere;
                front.AttachedCards = attached.Select(a => a.Card).Concat(queued.Select(q => q.Key)).ToList();
                front.AttachedOwners = attached.Select(a => a.Owner).Concat(queued.Select(q => q.Value.caster)).ToList();
                // An enemy spell on its way shows its back here too: scrolling the stack mustn't show what it is
                front.AttachedHidden = attached.Select(_ => false).Concat(queued.Select(q => spellGhosts[q.Key] != null && spellGhosts[q.Key].IsFaceDown)).ToList();

                var views = new List<CardController>();
                foreach (var attachment in attached.Take(MaxStrips)) {
                    var view = CardViews.Create(attachment.Card, attachment.Owner ?? Player, transform);
                    if (view == null) continue;
                    view.name = $"Attached_{attachment.Card.Name}";
                    view.SetAttachment();
                    Tuck(view, front, views.Count + 1);
                    views.Add(view);
                }
                foreach (var spell in queued.Select(q => q.Key)) {
                    if (views.Count >= MaxStrips + 2) break;
                    var view = spellGhosts[spell];
                    if (view == null || CardController.Dragged == view) continue;
                    Tuck(view, front, views.Count + 1);
                    views.Add(view);
                }
                attachedViews[slot] = views;
            }
        }

        // Whether this queued spell lands on the creature on its way to this slot: it homes on it, or it's aimed at the slot
        // with nothing standing there (the arrival is what it finds)
        private bool OnIncoming(ICard spell, BattlefieldSlot slot) {
            if (spell == null || slot == null || !queuedGhosts.TryGetValue(slot, out var incoming) || incoming == null) return false;
            var arriving = incoming.GetLinkedCardInstance() as ICreature;
            if (arriving == null) return false;
            return spellGhostHoming.TryGetValue(spell, out var homesOn) ? homesOn == arriving : slot.OccupyingCreature == null;
        }

        // A strip just above the card in front (depth 1 right on its top edge), as wide as it and a name band tall, showing
        // the top of the card tucked in it. The strips have a layer of their own beside the mat: for the side across the
        // table it's over that side's hand, which would otherwise hide them; for the screen's side, under its hand, so a
        // raised hand card stays over them
        private void Tuck(CardController view, CardController front, int depth) {
            view.TuckedUnder = front;
            var band = new GameObject($"Strip_{view.name}", typeof(RectTransform), typeof(RectMask2D));
            attachedBands.Add(band);
            var bandRect = (RectTransform)band.transform;
            bandRect.SetParent(StripLayer(), false);
            strips.Add((bandRect, front, depth));
            PlaceStrip(bandRect, front, depth);
            float strip = ((RectTransform)front.transform).rect.height * StripStep;

            var cardRect = (RectTransform)view.transform;
            cardRect.SetParent(bandRect, false);
            cardRect.localScale = Vector3.one;
            cardRect.localRotation = Quaternion.identity;
            cardRect.localPosition = new Vector3(-cardRect.rect.center.x, strip / 2f - cardRect.rect.yMax, 0f);
        }

        private void PlaceStrip(RectTransform band, CardController front, int depth) {
            var frontRect = (RectTransform)front.transform;
            float strip = frontRect.rect.height * StripStep;
            band.localScale = Vector3.one * (frontRect.lossyScale.x / Mathf.Max(1e-6f, band.parent.lossyScale.x));
            band.rotation = frontRect.rotation;
            band.sizeDelta = new Vector2(frontRect.rect.width, strip);
            Vector3 top = frontRect.TransformPoint(new Vector3(frontRect.rect.center.x, frontRect.rect.yMax, 0f));
            band.position = top + frontRect.up * (frontRect.lossyScale.y * strip * (depth - 0.5f));
        }

        // The strips follow their creature's card wherever it is (a summon's swell, the sides switching places). The idle
        // reminders go while the Day plays out and come back when the next one is planned
        private void LateUpdate() {
            UpdatePlayTarget();
            if (matLit && CardController.Dragged == null) LightUp(false); // Nothing carried any more: no glow left behind
            foreach (var (band, front, depth) in strips) {
                if (band != null && front != null) PlaceStrip(band, front, depth);
            }
            bool resolving = GameManager?.TurnManager?.IsResolving ?? false;
            if (resolving != wasResolving) {
                wasResolving = resolving;
                RefreshIdle();
                arrowManager?.RefreshArrows(); // The planned arrows go when the Day starts playing out, and the next Day's come back
            }
            // While an aimed spell is carried, the slots it can be played on glow: what it can be used on
            var dragged = CardController.Dragged;
            if (dragged != glowingFor) {
                glowingFor = dragged;
                bool aimed = dragged != null && dragged.GetLinkedCardInstance() is Spell spell && EffectResolver.IsAimed(spell)
                             && (IsCardFromHand(dragged) || dragged.IsQueued);
                foreach (var slot in battlefieldSlots) slot.SetEligible(aimed && CanAimAt(dragged, slot.Model));
            }
        }
        private bool wasResolving;
        private CardController glowingFor;

        private RectTransform StripLayer() {
            if (stripLayer == null) {
                stripLayer = (RectTransform)new GameObject($"AttachedCards_{Player?.Name}", typeof(RectTransform)).transform;
                stripLayer.SetParent(transform.parent, false);
            }
            var hand = table.GetHand(Player)?.transform;
            bool handAbove = hand != null && hand.parent == stripLayer.parent && hand.position.y > transform.position.y;
            stripLayer.SetAsLastSibling();
            stripLayer.SetSiblingIndex((handAbove ? hand : transform).GetSiblingIndex() + 1);
            return stripLayer;
        }

        // A status the screen can't see (Fog of War) doesn't show which card put it there
        private bool CanSee(Attachment attachment, ICreature creature) =>
            !(attachment.Modifier is StatusEffectModifier status) || Intel.CanSeeStatus(Viewer, creature, status.EffectType);

        // On a creature it's tucked under its card (RefreshAttachments). A lone spell aimed at an empty slot sits in it;
        // several there, small in the slot's lower-left corner, older ones stepping further left (stepsLeft, 0: the newest).
        // A spell on a whole side sits at the battlefield's lower-left end
        private void PlaceSpellGhost(CardController ghost, BoardSlot slot, int stepsLeft, int count) {
            // A slot this board hasn't a view for (its views not built yet, or another side's): it waits at the board's end
            var slotView = slot != null && slot.Owner == Player && slot.Index >= 0 && slot.Index < battlefieldSlots.Count ? battlefieldSlots[slot.Index] : null;
            if (slotView?.OccupyingCard != null) return; // Tucked under the creature (RefreshAttachments)
            if (slotView != null && OnIncoming(ghost.GetLinkedCardInstance(), slotView)) return; // Under the ghost of the creature on its way
            bool roomInSlot = slotView != null && count == 1 && slotView.OccupyingCard == null && !queuedGhosts.ContainsKey(slotView);
            var rect = (RectTransform)ghost.transform;
            rect.localRotation = Quaternion.identity;
            if (roomInSlot) {
                rect.SetParent(slotView.transform, false);
                rect.localPosition = Vector3.zero;
                rect.localScale = Vector3.one * CardInSlotScale;
                rect.SetAsLastSibling();
                return;
            }
            rect.SetParent(transform, false);
            rect.SetAsLastSibling();
            rect.localScale = Vector3.one * SpellGhostScale;
            var mat = ((RectTransform)transform).rect;
            float width = rect.rect.width * SpellGhostScale, height = rect.rect.height * SpellGhostScale;
            float left = slotView != null
                ? slotView.transform.localPosition.x - ((RectTransform)slotView.transform).rect.width * slotView.transform.localScale.x / 2f
                : mat.xMin;
            float x = left + width / 2f - stepsLeft * width * SpellGhostSpread;
            rect.localPosition = new Vector3(x, mat.yMin + height / 2f, 0f);
        }

        // Spells that aren't aimed wait in a pile past the right end of the mat, off the battlefields, the newest on top and
        // the older ones peeking out behind. Where it sits says what it hits: one on this whole side, small, tucked into the
        // mat's outer corner; one on both sides, bigger, on the mat's river edge reaching out over the river (on its caster's
        // half: yours below the river, the other side's above)
        private const float CastPileGap = 20f, CastPileStep = 12f, PileScale = 0.7f, SidePileScale = 0.5f;
        private const float OverTheRiver = 0.3f; // How much of a both-sides card reaches past the mat's edge, over the river
        private void PlaceInPile(CardController ghost, bool byRiver, int stepsBack) {
            var rect = (RectTransform)ghost.transform;
            rect.SetParent(transform, false);
            rect.SetAsLastSibling();
            rect.localRotation = Quaternion.identity;
            float scale = byRiver ? PileScale : SidePileScale;
            rect.localScale = Vector3.one * scale;
            var mat = ((RectTransform)transform).rect;
            float width = rect.rect.width * scale, height = rect.rect.height * scale;
            var other = table.GetBattlefield(Player.Opponent);
            bool riverAbove = other == null || other.transform.position.y > transform.position.y;
            float away = riverAbove ? -1f : 1f; // From the river toward this side
            float y = byRiver
                ? (riverAbove ? mat.yMax : mat.yMin) + away * (height * (0.5f - OverTheRiver) + stepsBack * CastPileStep)
                : (riverAbove ? mat.yMin : mat.yMax) - away * (height / 2f + 4f + stepsBack * CastPileStep);
            float x = mat.xMax + CastPileGap + width / 2f + stepsBack * CastPileStep;
            rect.localPosition = new Vector3(x, y, 0f);
        }

        private void ClearSpellGhosts() {
            foreach (var ghost in spellGhosts.Values) {
                if (ghost != null) Destroy(ghost.gameObject);
            }
            spellGhosts.Clear();
            spellGhostOrder.Clear();
        }

        // A see-through copy of each creature planned to move (or be pushed or swapped) in the slot it will end in, with the
        // formation arrows. Not interactive: the real card stays where it is and is still the one to drag
        private void ShowMoveGhosts() {
            var preview = MovementRules.Preview(Player.Battlefield, MovementRules.PlannedMovesOf(Player, GameManager.ActionsQueue, move => !HiddenFromScreen(move)));
            foreach (var entry in preview.FinalSlots) {
                var creature = entry.Key;
                var finalSlot = entry.Value;
                if (creature == null || finalSlot == null || creature.Slot == finalSlot || finalSlot.Owner != Player) continue;

                var slot = battlefieldSlots[finalSlot.Index];
                var ghost = CardViews.Create(creature, Player, slot.transform);
                if (ghost == null) continue;
                ghost.name = $"MoveGhost_{creature.Name}";
                ghost.transform.localRotation = Quaternion.identity;
                ghost.transform.localScale = Vector3.one * CardInSlotScale;
                ghost.SetQueued(true, blockRaycasts: false, badge: "Moving");
                ghost.SetAnonymous(FogOfWar.HidesIdentity(Viewer, creature));
                // Someone stands there now (it leaves first): lift the ghost so both stay visible
                float raise = slot.OccupyingCard != null || queuedGhosts.ContainsKey(slot) ? ((RectTransform)ghost.transform).rect.height * CardInSlotScale * ReplacingGhostRaise : 0f;
                ghost.transform.localPosition = new Vector3(0f, raise, 0f);
                moveGhosts.Add(ghost);
            }
        }

        /// <summary>The creatures shown as ghosts in the slot they're planned to end their move in.</summary>
        public IEnumerable<(ICreature creature, BoardSlot slot)> MoveGhosts =>
            moveGhosts.Where(g => g != null).Select(g => (g.GetLinkedCreature(), g.GetComponentInParent<BattlefieldSlot>()?.Model));

        private void TakeBackPlay(CardController ghost) {
            if (GameManager?.TurnManager?.IsResolving ?? true) return;
            GameManager.ActionsQueue.CancelPlay(ghost.GetLinkedCardInstance());
        }

        private PlayCardAction QueuedPlayOf(ICard card) =>
            GameManager?.ActionsQueue?.GetPendingActions().OfType<PlayCardAction>().FirstOrDefault(p => p.GetCard() == card);

        private void OnGhostBeginDrag(CardController ghost) {
            draggedGhostPlay = QueuedPlayOf(ghost.GetLinkedCardInstance());
            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas != null) ghost.transform.SetParent(rootCanvas.transform, true); // Drawn over every slot while dragged
        }

        private void OnGhostEndDrag(CardController ghost) {
            PlayOnTarget(ghost); // Let go near a slot, not on one: it lands in the one picked
            // Dropped where it can't be played, it keeps its old play: take it back. A valid drop already replaced it.
            if (draggedGhostPlay != null && QueuedPlayOf(ghost.GetLinkedCardInstance()) == draggedGhostPlay) {
                GameManager.ActionsQueue.CancelPlay(ghost.GetLinkedCardInstance());
            }
            draggedGhostPlay = null;
            RefreshQueuedGhosts(); // Puts back any ghost left where it was released
        }

        private void ClearQueuedGhosts() {
            foreach (var entry in queuedGhosts) {
                if (entry.Value != null) Destroy(entry.Value.gameObject);
                if (entry.Key != null) entry.Key.OccupyingCard?.SetMarkedForReplacement(false);
            }
            queuedGhosts.Clear();
            foreach (var ghost in risingGhosts) {
                if (ghost != null) Destroy(ghost.gameObject);
            }
            risingGhosts.Clear();
            foreach (var ghost in moveGhosts) {
                if (ghost != null) Destroy(ghost.gameObject);
            }
            moveGhosts.Clear();
        }

        protected override void SetupCardEventHandlers(CardController controller) {
            base.SetupCardEventHandlers(controller);
            controller.OnCardClicked.AddListener(OnCardClicked);
            controller.OnCardRightClicked.AddListener(OnCardClicked);
            controller.RefusesDrag = RefuseIfItCantAct;
        }

        // Clicking or right-clicking a creature withdraws its queued attack
        private void OnCardClicked(CardController card) {
            if (GameManager.TurnManager.IsResolving) return;
            GameManager.CombatHandler.CancelAttack(card.GetLinkedCreature());
        }

        private void OnCreatureSummoned(ICreature creature, IPlayer player) {
            if (!IsInitialized || player != Player) return;
            UpdateUI(Player);

            var summonedCard = GetCardController(creature);
            if (summonedCard != null) {
                float delay = Mathf.Max(0f, nextSummonTime - Time.time);
                nextSummonTime = Time.time + delay + SummonStagger;
                summonedCard.PlaySummonAnimation(delay);
            }
        }

        #endregion

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
            if (creature?.Slot == null || creature.Owner != Player || !FogOfWar.CanControl(Player) || GameManager.ModifierManager.CanMove(creature)) return false;
            ShowCantAct(card, GameManager.ModifierManager.HasStatusEffect(creature, Enums.StatusEffectType.Rooted) && GameManager.ModifierManager.CanAct(creature)
                ? "Rooted: it can't move" : CantActText(creature));
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
            top.sortingOrder = 1000;
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

            // Released anywhere but another slot (including back on its own): the creature stands down
            if (GetDragIntent(card, GetTargetSlot()) == BattlefieldArrowManager.DragIntent.None && !GameManager.TurnManager.IsResolving) {
                GameManager.CombatHandler.CancelAttack(card.GetLinkedCreature());
            }
            UpdateUI(Player);
        }
        #endregion

        // The card shown for the creature on this board, if any
        public CardController GetCardController(ICreature creature) {
            if (creature == null) return null;
            return battlefieldSlots.FirstOrDefault(s => s.OccupyingCard != null && s.OccupyingCard.GetLinkedCreature() == creature)?.OccupyingCard;
        }

        public BattlefieldSlot GetSlot(CardController card) {
            return battlefieldSlots.FirstOrDefault(s => s.OccupyingCard == card);
        }

        // The view of a board slot on either battlefield
        public BattlefieldSlot FindSlotView(BoardSlot model) {
            if (model == null) return null;
            if (model.Owner == Player) return battlefieldSlots[model.Index];
            return table.GetBattlefield(model.Owner)?.FindSlotView(model);
        }

        protected override void OnCardDropped(CardController card) {
            Log($"Card dropped from Battlefield: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);
            UpdateLayout();
        }

        #region Cleanup
        protected override void OnEnable() {
            base.OnEnable(); // Its events again
            boards.Add(this);
        }

        protected override void OnDisable() {
            base.OnDisable();
            boards.Remove(this);
        }

        protected override void OnDestroy() {
            base.OnDestroy();
            ClearQueuedGhosts();
            ClearSpellGhosts();
            foreach (var band in attachedBands) if (band != null) Destroy(band);
            attachedBands.Clear();
            attachedViews.Clear();
            if (stripLayer != null) Destroy(stripLayer.gameObject);
            foreach (var slot in battlefieldSlots) {
                if (slot != null) {
                    var cardController = slot.OccupyingCard;

                    if (cardController != null) {
                        Destroy(cardController.gameObject);
                    }

                    Destroy(slot.gameObject);
                }
            }
            battlefieldSlots.Clear();

            if (arrowManager != null) {
                arrowManager.Cleanup();
            }
        }
        #endregion

        protected override void OnCardHoverEnter(CardController card) { }
        protected override void OnCardHoverExit(CardController card) { }
    }
}
