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
        private readonly List<CardController> moveGhosts = new List<CardController>(); // Where planned moves leave each creature
        private PlayCardAction draggedGhostPlay; // The play of the ghost being dragged, to tell whether the drop replaced it
        private float nextSummonTime;

        // Queued spells aimed here, each shown as a ghost where it's aimed (see RefreshSpellGhosts)
        private readonly Dictionary<ICard, CardController> spellGhosts = new Dictionary<ICard, CardController>();
        private const float SpellGhostScale = 0.5f;   // Small, on an edge, when something already stands where it's aimed
        private const float SpellGhostSpread = 0.45f; // Ghosts at the same place fan out by this share of their width
        private const float CastStagger = 0.2f;
        private const float CastScale = 2.1f;         // How big a cast spell swells, of board size
        private float nextCastTime;

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
                side => table.GetBattlefield(side)?.transform, HiddenFromScreen);
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

        // The mat only lights up under a card that plays on the whole side (see HandlePointerEnter)
        protected override DropZoneColors ZoneColors => new DropZoneColors(UITheme.Mat, UITheme.Mat, UITheme.Mat, UITheme.Mat);

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
        /// <summary>Where a dragged card lands on this battlefield: one of its slots, the whole side, or nowhere.</summary>
        public enum DropAim { None, Slot, Side }

        /// <summary>
        /// What dropping this card here would aim at. Creatures on the board move or attack into a slot; creatures from the
        /// hand are summoned into a slot of their own side; aimed spells hit a slot on either side; other spells play on a
        /// side they do something to (see <see cref="EffectResolver.Reaches"/>). Only what it aims at lights up.
        /// </summary>
        public DropAim AimOf(CardController card) {
            if (card == null || Player == null) return DropAim.None;
            // A queued creature's ghost is still a card in the hand
            if (!IsCardFromHand(card) && !card.IsQueued) return card.GetLinkedCreature() != null ? DropAim.Slot : DropAim.None;

            switch (card.GetLinkedCardInstance()) {
                case ICreature:
                    return card.Player == Player ? DropAim.Slot : DropAim.None;
                case Spell spell when EffectResolver.IsAimed(spell):
                    return DropAim.Slot;
                case Spell spell:
                    return EffectResolver.Reaches(spell, card.Player, Player) ? DropAim.Side : DropAim.None;
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
            if (PlaysAnywhere(dragged)) table.GetBattlefield(Player.Opponent)?.LightUp(false);
        }

        /// <summary>Lights the mat up as where a carried card would play, or puts it back.</summary>
        public void LightUp(bool lit) => GetComponent<Image>().color = lit ? UITheme.MatValid : UITheme.Mat;

        /// <summary>
        /// Whether this card plays on either side alike: a spell (a Trend, or one that does something to both sides) that
        /// isn't aimed at a slot. It's played by dropping it anywhere on the table (see <see cref="PlayArea"/>).
        /// </summary>
        public bool PlaysAnywhere(CardController card) =>
            card?.Player != null && (IsCardFromHand(card) || card.IsQueued) && card.GetLinkedCardInstance() is Spell spell && !EffectResolver.IsAimed(spell)
            && EffectResolver.Reaches(spell, card.Player, card.Player) && EffectResolver.Reaches(spell, card.Player, card.Player.Opponent);

        /// <summary>Plays a card that plays on either side, dropped somewhere on the table: on its caster's side.</summary>
        public void PlayAnywhere(CardController card) {
            if (!PlaysAnywhere(card) || GameManager == null) return;
            PlayFromHand(card, card.Player);
        }

        protected override void HandleCardDropped(CardController card) {
            if (card == null || !CanAcceptCard(card)) return;
            if (GameManager == null) return;

            var aim = AimOf(card);
            if (aim == DropAim.Side) {
                PlayFromHand(card, Player);
                return;
            }
            var targetSlot = aim == DropAim.Slot ? GetTargetSlot() : null;
            if (targetSlot == null) return;

            if (IsCardFromHand(card) || card.IsQueued) {
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
            var cardToPlay = card.GetLinkedCardInstance();
            if (cardToPlay is ICreature && queuedGhosts.TryGetValue(targetSlot, out var ghost) && ghost.GetLinkedCardInstance() != cardToPlay) {
                Log($"Slot already has a creature queued; ignoring drop of {cardToPlay.Name}", LogTag.Actions | LogTag.Cards);
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

            if (creature.Owner != Player) {
                GameManager.CombatHandler.HandleCreatureCombat(creature, targetSlot.Model);
            }
            else {
                HandleCreatureMove(creature, targetSlot.Model);
            }
        }

        /// <summary>Whether the drop uses the creature's Active ability instead of attacking or moving. Tests can replace it.</summary>
        public static System.Func<bool> UsingAbility = () => GameInput.ShiftHeld;

        private void HandleCreatureMove(ICreature creature, BoardSlot targetSlot) {
            if (!GameManager.ModifierManager.CanAct(creature)) return;

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
                slot.ShowSlotEffect(effect, FogOfWar.Hides(Viewer, effect));
            }
            RefreshHiddenPlans();
            RefreshSpellGhosts(); // What stands in the slots decides where they go

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
            }
        }

        protected override void UnregisterEvents() {
            if (GameMediator != null) {
                GameMediator.CreatureSummoned -= OnCreatureSummoned;
                GameMediator.BattlefieldStateChanged -= UpdateUI;
                GameMediator.ActionsQueueChanged -= RefreshQueuedGhosts;
                GameMediator.ActionsQueueChanged -= RefreshWatchdogs;
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
                card.SetAnonymous(FogOfWar.HidesIdentity(Viewer, slot.OccupyingCreature));
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
                // Hoverable for its enlarged preview; drops still reach the slot (they bubble up, and targets come from a full raycast).
                // Like a queued card in the hand, clicking it or dragging it away takes the play back, and dragging it to a slot re-aims it.
                ghost.SetQueued(true, canBeDragged: true);
                ghost.OnCardClicked.AddListener(TakeBackPlay);
                ghost.OnCardRightClicked.AddListener(TakeBackPlay);
                ghost.OnBeginDragEvent.AddListener(OnGhostBeginDrag);
                ghost.OnEndDragEvent.AddListener(OnGhostEndDrag);
                ghost.SetAnonymous(FogOfWar.HidesSummon(Viewer, action, GameManager.ActionsQueue)); // Something is coming, but what?
                queuedGhosts[slot] = ghost;

                // The creature currently in the slot will be replaced when the summon resolves:
                // raise the ghost so both cards and the occupant's REPLACE badge stay visible
                var occupant = slot.OccupyingCard;
                float raise = occupant != null ? ((RectTransform)ghost.transform).rect.height * ReplacingGhostRaise : 0f;
                ghost.transform.localPosition = new Vector3(0f, raise, 0f);
                occupant?.SetMarkedForReplacement(true);
            }

            ShowMoveGhosts();
            RefreshHiddenPlans();
            RefreshSpellGhosts();
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
            var aimed = new List<(Spell spell, IPlayer caster, BoardSlot slot)>();
            foreach (var play in GameManager.ActionsQueue.GetPendingActions().OfType<PlayCardAction>()) {
                if (!(play.GetCard() is Spell spell) || HiddenFromScreen(play)) continue;
                var side = BattlefieldArrowManager.AimOf(spell, play.GetOwner(), play.GetTarget(), out var slot);
                if (side == Player) aimed.Add((spell, play.GetOwner(), slot));
            }

            // Gone from the queue: cast (it left its caster's hand), or taken back
            var stillAimed = new HashSet<ICard>(aimed.Select(a => (ICard)a.spell));
            foreach (var entry in spellGhosts.Where(e => !stillAimed.Contains(e.Key)).ToList()) {
                spellGhosts.Remove(entry.Key);
                var ghost = entry.Value;
                if (ghost == null) continue;
                if (ghost.Player != null && !ghost.Player.Hand.Contains(entry.Key)) PlayCast(ghost);
                else Destroy(ghost.gameObject);
            }

            foreach (var group in aimed.GroupBy(a => a.slot)) {
                var together = group.ToList();
                for (int i = 0; i < together.Count; i++) {
                    var (spell, caster, slot) = together[i];
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
                    }
                    ghost.SetFaceDown(FogOfWar.HidesHandOf(caster));
                    if (CardController.Dragged == ghost) continue;
                    PlaceSpellGhost(ghost, slot, i, together.Count);
                }
            }
        }

        // In the empty slot it's aimed at (the first one there), or small on the edge facing the other side
        private void PlaceSpellGhost(CardController ghost, BoardSlot slot, int index, int count) {
            var slotView = slot != null ? battlefieldSlots[slot.Index] : null;
            bool roomInSlot = slotView != null && index == 0 && slotView.OccupyingCard == null && !queuedGhosts.ContainsKey(slotView);
            var rect = (RectTransform)ghost.transform;
            rect.localRotation = Quaternion.identity;
            if (roomInSlot) {
                rect.SetParent(slotView.transform, false);
                rect.localPosition = Vector3.zero;
                rect.localScale = Vector3.one;
                rect.SetAsLastSibling();
                return;
            }
            rect.SetParent(transform, false);
            rect.SetAsLastSibling();
            rect.localScale = Vector3.one * SpellGhostScale;
            var mat = ((RectTransform)transform).rect;
            var other = table.GetBattlefield(Player.Opponent);
            bool facesUp = other == null || other.transform.position.y > transform.position.y;
            float x = slotView != null ? slotView.transform.localPosition.x : mat.center.x;
            x += (index - (count - 1) / 2f) * rect.rect.width * SpellGhostScale * SpellGhostSpread;
            rect.localPosition = new Vector3(x, facesUp ? mat.yMax : mat.yMin, 0f);
        }

        // The cast: the ghost leaps to the middle of the screen, swells and bursts, like a spell cast from the hand
        private void PlayCast(CardController ghost) {
            var root = GetComponentInParent<Canvas>()?.rootCanvas;
            if (root == null) {
                Destroy(ghost.gameObject);
                return;
            }
            float delay = Mathf.Max(0f, nextCastTime - Time.time);
            nextCastTime = Time.time + delay + CastStagger;
            ghost.PlayCastAnimation(root.transform, root.transform.position, delay, CardFx.UnitOf(transform, root.transform) * CastScale);
        }

        private void ClearSpellGhosts() {
            foreach (var ghost in spellGhosts.Values) {
                if (ghost != null) Destroy(ghost.gameObject);
            }
            spellGhosts.Clear();
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
                ghost.SetQueued(true, blockRaycasts: false);
                ghost.SetAnonymous(FogOfWar.HidesIdentity(Viewer, creature));
                // Someone stands there now (it leaves first): lift the ghost so both stay visible
                float raise = slot.OccupyingCard != null || queuedGhosts.ContainsKey(slot) ? ((RectTransform)ghost.transform).rect.height * ReplacingGhostRaise : 0f;
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
            foreach (var ghost in moveGhosts) {
                if (ghost != null) Destroy(ghost.gameObject);
            }
            moveGhosts.Clear();
        }

        protected override void SetupCardEventHandlers(CardController controller) {
            base.SetupCardEventHandlers(controller);
            controller.OnCardClicked.AddListener(OnCardClicked);
            controller.OnCardRightClicked.AddListener(OnCardClicked);
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
        protected override void OnCardBeginDrag(CardController card) {
            if (card == null) return;

            var startSlot = GetSlot(card);
            arrowManager.ShowDragArrow(startSlot != null ? startSlot.transform.position : card.transform.position);
            draggedCard = card;
            card.transform.SetAsLastSibling();
            ShowMoveHints(card.GetLinkedCreature());
        }

        // Every slot of the row says what dropping the creature there would do, given this side's other planned moves
        private void ShowMoveHints(ICreature creature) {
            if (creature?.Slot == null || creature.Owner != Player || !GameManager.ModifierManager.CanAct(creature)) return;
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
        protected override void OnDestroy() {
            base.OnDestroy();
            ClearQueuedGhosts();
            ClearSpellGhosts();
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
