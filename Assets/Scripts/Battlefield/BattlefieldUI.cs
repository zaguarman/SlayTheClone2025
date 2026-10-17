using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>
    /// Shows one player's battlefield. The board itself lives in <see cref="IPlayer.Battlefield"/>; this view
    /// mirrors it (one <see cref="BattlefieldSlot"/> per <see cref="BoardSlot"/>, one card per creature) and turns
    /// drags and drops into actions.
    /// </summary>
    public partial class BattlefieldUI : CardContainer {
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
        private readonly List<CardController> moveGhosts = new List<CardController>(); // The phantoms planned moves leave behind
        private readonly List<CardController> displaced = new List<CardController>(); // The cards standing where their planned move ends
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
            int slotCount = Player?.Battlefield.Count ?? Zeitgeist.Player.BattlefieldSize;
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
            var positions = SlotPositions(Zeitgeist.Player.BattlefieldSize);
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
            var positions = SlotPositions(Zeitgeist.Player.BattlefieldSize);
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
