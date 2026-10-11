using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class BattlefieldUI : CardContainer {
        private const int MaxSlots = 5;
        private const float SummonStagger = 0.15f;
        private const float ReplacingGhostRaise = 0.3f; // Fraction of card height a ghost is raised when it will replace a creature
        private readonly List<BattlefieldSlot> battlefieldSlots = new List<BattlefieldSlot>();

        // Translucent previews of creatures queued to be summoned into this battlefield
        private readonly Dictionary<BattlefieldSlot, CardController> queuedGhosts = new Dictionary<BattlefieldSlot, CardController>();
        private float nextSummonTime;

        private BattlefieldArrowManager arrowManager;
        private CardController draggedCard; // Creature of this board currently being dragged

        #region Initialization
        private void InitializeManagers(IPlayer player) {
            // Create ArrowManager using injected dependencies from UIComponent base class
            // Get ActionsQueue from the injected GameManager
            var actionsQueue = GameManager?.ActionsQueue;
            if (actionsQueue == null) {
                LogError("Cannot initialize BattlefieldArrowManager - ActionsQueue is null via GameManager", LogTag.Initialization);
                return;
            }
            arrowManager = new BattlefieldArrowManager(transform, GameMediator, actionsQueue, player);
        }

        private void CreateSlots() {
            for (int i = 0; i < MaxSlots; i++) {
                GameObject slotObj = new GameObject($"Slot_{i}", typeof(RectTransform));
                slotObj.transform.SetParent(transform, false);

                var slot = slotObj.AddComponent<BattlefieldSlot>();
                slot.Initialize(defaultColor, validDropColor, invalidDropColor, hoverColor);

                // Pass the GameReferences to the slot
                if (GameReferences != null) {
                    slot.SetGameReferences(GameReferences);
                }

                battlefieldSlots.Add(slot);
            }
            UpdateSlotPositions();
        }

        public override void Initialize(IPlayer player, IGameMediator mediator, IGameReferences references, IGameManager manager) {
            // Call base CardContainer Initialize FIRST
            base.Initialize(player, mediator, references, manager);

            // Now initialize Battlefield specific things
            InitializeManagers(player); // Pass player
            CreateSlots();
            Log($"BattlefieldUI initialized for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Initialization);

            player.InitializeBattlefield(battlefieldSlots);
            Log($"Player Battlefield initialized for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Initialization);

            // Update UI based on initial state
            UpdateUI(Player);
        }

        private void UpdateSlotPositions() {
            float totalWidth = (MaxSlots - 1) * settings.spacing;
            float startX = -totalWidth / 2;

            for (int i = 0; i < battlefieldSlots.Count; i++) {
                float xPos = startX + (settings.spacing * i);
                battlefieldSlots[i].SetPosition(new Vector2(xPos, 0));
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
            GameManager.ActionsQueue.AddAction(new PlayCardAction(cardToPlay, caster, targetSlot));
        }

        private void HandleCardFromBattlefield(CardController card, ITarget target) {
            if (target != null) {
                if (card.IsPlayer1Card() != Player.IsPlayer1) {
                    GameManager.CombatHandler.HandleCreatureCombat(card, target);
                }
                else {
                    HandleCreatureMove(card, target);
                }
            }
        }

        private void HandleCreatureMove(CardController card, ITarget targetSlot) {
            var creature = card.GetLinkedCreature();
            if (creature == null) return;

            var sourceSlot = battlefieldSlots.FirstOrDefault(s => s.OccupyingCard == card);
            if (sourceSlot != null && sourceSlot != (BattlefieldSlot)targetSlot) {
                var moveAction = new MoveCreatureAction(creature, (ITarget)sourceSlot, targetSlot, Player);
                GameManager.ActionsQueue.AddAction(moveAction);
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
        public override void UpdateUI(IPlayer player) {
            if (!IsInitialized || Player == null) return;
            if (player != Player) return;

            // Clear orphaned cards (cards leaving play destroy themselves once their exit animation ends)
            foreach (var card in cards.ToList()) {
                bool existsInSlot = player.Battlefield.Any(s => s.OccupyingCard == card);
                if (!existsInSlot) {
                    RemoveCard(card);
                    if (card != null && !card.IsLeavingPlay) Destroy(card.gameObject);
                }
            }

            // Add and position cards from slots
            foreach (var slot in player.Battlefield) {
                if (slot.OccupyingCard != null) {
                    // Add card to the list if not already present
                    if (!cards.Contains(slot.OccupyingCard)) {
                        AddCard(slot.OccupyingCard);
                    }
                    slot.SnapToSlot(slot.OccupyingCard);
                }
            }

            UpdateLayout();
        }

        protected override void UpdateLayout() {
            // Intentionally left empty to prevent CardContainer from repositioning cards
            // Slots handle card positioning instead
        }
        #endregion

        #region Event Handling
        protected override void RegisterEvents() {
            if (GameMediator != null) {
                GameMediator.AddCreatureSummonedListener(OnCreatureSummoned);
                GameMediator.AddBattlefieldStateChangedListener(UpdateUI);
                GameMediator.AddActionsQueueChangedListener(RefreshQueuedGhosts);
            }
        }

        protected override void UnregisterEvents() {
            if (GameMediator != null) {
                GameMediator.RemoveCreatureSummonedListener(OnCreatureSummoned);
                GameMediator.RemoveBattlefieldStateChangedListener(UpdateUI);
                GameMediator.RemoveActionsQueueChangedListener(RefreshQueuedGhosts);
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

                if (!(card is ICreature) || !(target is BattlefieldSlot slot)) continue;
                if (!battlefieldSlots.Contains(slot) || queuedGhosts.ContainsKey(slot)) continue;

                var data = CardFactory.FindCardData(card, Player);
                if (data == null) continue;

                var ghost = CardFactory.CreateCardController(card, data, Player, slot.transform, GameMediator, GameReferences, GameManager);
                if (ghost == null) continue;

                ghost.name = $"QueuedGhost_{card.Name}";
                ghost.transform.localRotation = Quaternion.identity;
                ghost.SetQueued(true, blockRaycasts: false); // Let drops and hovers reach the slot
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

        public CardController GetCardController(ICreature creature) => FindOwnSlot(creature)?.OccupyingCard;

        // Looks on this board, then on the opponent's
        public BattlefieldSlot GetSlot(ICreature creature) => FindOwnSlot(creature) ?? GetOpponentBattlefield().FindOwnSlot(creature);

        private BattlefieldSlot FindOwnSlot(ICreature creature) => battlefieldSlots.FirstOrDefault(s => s.OccupyingCreature == creature);

        public BattlefieldSlot GetSlot(CardController card) {
            return battlefieldSlots.FirstOrDefault(s => s.OccupyingCard == card);
        }

        private BattlefieldUI GetOpponentBattlefield() => GameReferences.GetPlayerReferences(Player.Opponent).BattlefieldUI;

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
