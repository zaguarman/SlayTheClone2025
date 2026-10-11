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
        private const float MatPadding = 14f; // Between the slots and the edge of the mat
        private const float ReplacingGhostRaise = 0.3f; // Fraction of card height a ghost is raised when it will replace a creature
        private readonly List<BattlefieldSlot> battlefieldSlots = new List<BattlefieldSlot>();

        // Translucent previews of creatures queued to be summoned into this battlefield
        private readonly Dictionary<BattlefieldSlot, CardController> queuedGhosts = new Dictionary<BattlefieldSlot, CardController>();
        private float nextSummonTime;

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
            arrowManager = new BattlefieldArrowManager(transform, GameMediator, actionsQueue, player, FindSlotView);
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
            Log($"BattlefieldUI initialized for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Initialization);

            // Update UI based on initial state
            UpdateUI(Player);
        }

        protected override DropZoneColors ZoneColors => new DropZoneColors(UITheme.Mat, UITheme.MatValid, UITheme.MatInvalid, UITheme.MatHover);

        // A rounded mat just big enough for its row of slots
        protected override void StyleZone(Image zoneImage) => ApplyMatLook();

        /// <summary>
        /// Makes this battlefield's image a rounded mat just big enough for its row of slots.
        /// Also used in the editor, before there are any slots (see <see cref="BoardSkin"/>).
        /// </summary>
        public void ApplyMatLook() {
            var zoneImage = GetComponent<Image>();
            if (zoneImage != null) UITheme.Round(zoneImage, UITheme.Mat, 1.2f);
            int slotCount = Player?.Battlefield.Count ?? SlayTheClone.Player.BattlefieldSize;
            ((RectTransform)transform).sizeDelta = new Vector2((slotCount - 1) * settings.spacing + BattlefieldSlot.Size.x + 2 * MatPadding, BattlefieldSlot.Size.y + 2 * MatPadding);
        }

        /// <summary>Where each of this many slots sits on the mat: a centered row.</summary>
        public List<Vector2> SlotPositions(int slotCount) {
            float startX = -(slotCount - 1) * settings.spacing / 2f;
            return Enumerable.Range(0, slotCount).Select(i => new Vector2(startX + settings.spacing * i, 0f)).ToList();
        }

        private void UpdateSlotPositions() {
            var positions = SlotPositions(battlefieldSlots.Count);
            for (int i = 0; i < battlefieldSlots.Count; i++) {
                battlefieldSlots[i].SetPosition(positions[i]);
            }
        }
        #endregion

        #region Card Handling
        protected override void HandleCardDropped(CardController card) {
            if (card == null || !CanAcceptCard(card)) return;
            if (GameManager == null) return;

            var targetSlot = GetTargetSlot();
            if (targetSlot == null) return;

            if (IsCardFromHand(card)) {
                HandleCardFromHand(card, targetSlot);
            }
            else {
                HandleCardFromBattlefield(card, targetSlot);
            }
        }

        private bool IsCardFromHand(CardController card) {
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
            var caster = card.Player;
            var cardToPlay = card.GetLinkedCardInstance();
            if (caster == null || cardToPlay == null || !caster.Hand.Contains(cardToPlay)) {
                LogWarning($"Dropped card {card.GetBaseCardData()?.cardName} is not in its player's hand; ignoring drop.", LogTag.Actions | LogTag.Cards);
                return;
            }

            if (cardToPlay is ICreature && queuedGhosts.ContainsKey(targetSlot)) {
                Log($"Slot already has a creature queued; ignoring drop of {cardToPlay.Name}", LogTag.Actions | LogTag.Cards);
                return;
            }

            Log($"Adding PlayCardAction for {cardToPlay.Name} on slot (TargetID: {targetSlot.TargetId.ToUpper()})", LogTag.Actions | LogTag.Cards);
            GameManager.ActionsQueue.AddAction(new PlayCardAction(cardToPlay, caster, targetSlot.Model));
        }

        private void HandleCardFromBattlefield(CardController card, BattlefieldSlot targetSlot) {
            var creature = card.GetLinkedCreature();
            if (creature == null || targetSlot == null) return;

            if (creature.Owner != Player) {
                GameManager.CombatHandler.HandleCreatureCombat(creature, targetSlot.Model);
            }
            else {
                HandleCreatureMove(creature, targetSlot.Model);
            }
        }

        private void HandleCreatureMove(ICreature creature, BoardSlot targetSlot) {
            if (!GameManager.ModifierManager.CanAct(creature)) return;

            var sourceSlot = creature.Slot;
            if (sourceSlot != null && sourceSlot != targetSlot) {
                GameManager.ActionsQueue.AddAction(new MoveCreatureAction(creature, sourceSlot, targetSlot, Player));
            }
        }

        private BattlefieldSlot GetTargetSlot() {
            var pointerEventData = new PointerEventData(EventSystem.current) {
                position = Input.mousePosition
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
            }
        }

        protected override void UnregisterEvents() {
            if (GameMediator != null) {
                GameMediator.CreatureSummoned -= OnCreatureSummoned;
                GameMediator.BattlefieldStateChanged -= UpdateUI;
                GameMediator.ActionsQueueChanged -= RefreshQueuedGhosts;
            }
        }

        private void RefreshQueuedGhosts() {
            if (!IsInitialized || Player == null) return;
            ClearQueuedGhosts();

            var pendingActions = GameManager?.ActionsQueue?.GetPendingActions();
            if (pendingActions == null) return;

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
                // Hoverable for its enlarged preview; drops still reach the slot (they bubble up, and targets come from a full raycast)
                ghost.SetQueued(true);
                queuedGhosts[slot] = ghost;

                // The creature currently in the slot will be replaced when the summon resolves:
                // raise the ghost so both cards and the occupant's REPLACE badge stay visible
                var occupant = slot.OccupyingCard;
                float raise = occupant != null ? ((RectTransform)ghost.transform).rect.height * ReplacingGhostRaise : 0f;
                ghost.transform.localPosition = new Vector3(0f, raise, 0f);
                occupant?.SetMarkedForReplacement(true);
            }
        }

        private void ClearQueuedGhosts() {
            foreach (var entry in queuedGhosts) {
                if (entry.Value != null) Destroy(entry.Value.gameObject);
                if (entry.Key != null) entry.Key.OccupyingCard?.SetMarkedForReplacement(false);
            }
            queuedGhosts.Clear();
        }

        protected override void SetupCardEventHandlers(CardController controller) {
            base.SetupCardEventHandlers(controller);
            controller.OnCardClicked.AddListener(OnCardClicked);
        }

        // Clicking a creature withdraws its queued attack
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
        protected override void OnCardBeginDrag(CardController card) {
            if (card == null) return;

            var startSlot = GetSlot(card);
            arrowManager.ShowDragArrow(startSlot != null ? startSlot.transform.position : card.transform.position);
            draggedCard = card;
            card.transform.SetAsLastSibling();
        }

        // The drag arrow follows the pointer and shows what releasing there would do
        private void Update() {
            if (draggedCard == null) return;

            var eventCamera = CanvasUtility.GetEventCamera(this);
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform, Input.mousePosition, eventCamera, out var pointer)) {
                arrowManager.UpdateDragArrow(pointer, GetDragIntent(draggedCard, GetTargetSlot()));
            }
        }

        private BattlefieldArrowManager.DragIntent GetDragIntent(CardController card, BattlefieldSlot slotUnderPointer) {
            if (slotUnderPointer == null || slotUnderPointer == GetSlot(card)) return BattlefieldArrowManager.DragIntent.None;
            return slotUnderPointer.Owner == Player ? BattlefieldArrowManager.DragIntent.Move : BattlefieldArrowManager.DragIntent.Attack;
        }

        protected override void OnCardEndDrag(CardController card) {
            arrowManager.HideDragArrow();
            draggedCard = null;

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
        protected override void OnDestroy() {
            base.OnDestroy();
            ClearQueuedGhosts();
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
