using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>The battlefield: what it shows as the game changes, and the Fog of War (a part of BattlefieldUI).</summary>
    public partial class BattlefieldUI {

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
            var afterMoves = MovementRules.Preview(Player.Battlefield, MovementRules.PlannedMovesOf(Player, GameManager.ActionsQueue), GameManager.ModifierManager).FinalSlots;
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
                ghost.OnCardRightClicked.AddListener(g => { if (!TakeBackSpellOn(g)) TakeBackPlay(g); }); // Your spells on it come back first
                ghost.OnRightClickedAcross.AddListener(g => TakeBackSpellOn(g));
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
            piles.Clear();
            foreach (var group in aimed.Where(a => spellGhosts.ContainsKey(a.spell)).GroupBy(a => (a.slot, a.byRiver))) {
                var together = group.OrderBy(a => spellGhostOrder[a.spell]).ToList();
                if (group.Key.slot == null) piles.Add(together.Select(a => ((ICard)a.spell, a.caster)).ToList());
                for (int i = 0; i < together.Count; i++) {
                    var ghost = spellGhosts[together[i].spell];
                    if (CardController.Dragged == ghost) continue;
                    if (together[i].slot == null) PlaceInPile(ghost, together[i].byRiver, together.Count - 1 - i);
                    else PlaceSpellGhost(ghost, together[i].slot, together.Count - 1 - i, together.Count);
                }
            }
            RefreshAttachments();
        }

        // The piles of spells beside the mat (on a whole side, or by the river), each oldest first
        private readonly List<List<(ICard spell, IPlayer caster)>> piles = new List<List<(ICard, IPlayer)>>();

        // A pile's roulette (the user): its newest card the screen can see leads, the others are its stack, newest first, so
        // hovering any of them opens it there. A pile of backs only (the enemy's, unseen) has none
        private void GroupPiles() {
            foreach (var pile in piles) {
                var views = pile.Where(p => spellGhosts.TryGetValue(p.spell, out var v) && v != null).Select(p => (p.spell, p.caster, view: spellGhosts[p.spell])).ToList();
                foreach (var (_, _, view) in views) {
                    view.HeadsPile = false;
                    view.AttachedCards = new List<ICard>();
                    view.AttachedOwners = new List<IPlayer>();
                    view.AttachedHidden = new List<bool>();
                    view.AttachedQueued = new List<bool>();
                }
                if (views.Count < 2) continue;
                var front = views.LastOrDefault(v => !v.view.IsFaceDown).view;
                if (front == null) continue;
                var rest = views.Where(v => v.view != front).Reverse().ToList();
                front.HeadsPile = true;
                front.AttachedCards = rest.Select(v => v.spell).ToList();
                front.AttachedOwners = rest.Select(v => v.caster).ToList();
                front.AttachedHidden = rest.Select(v => v.view.IsFaceDown).ToList();
                front.AttachedQueued = rest.Select(_ => true).ToList();
                foreach (var v in rest) v.view.TuckedUnder = front;
            }
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
                    incoming.AttachedQueued = onIt.Select(_ => true).ToList();
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
                front.AttachedQueued = attached.Select(_ => false).Concat(queued.Select(_ => true)).ToList();

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
            GroupPiles();
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
            // A card picked up from the hand (the user, 2026-10-17): the slots it can be played on glow softly (an aimed spell's
            // targets, a creature's own slots, a Gift's empty ones on the other side), or the whole side for a spell that plays
            // anywhere and does something there
            var dragged = CardController.Dragged;
            if (dragged != glowingFor) {
                glowingFor = dragged;
                bool fromHand = dragged != null && (IsCardFromHand(dragged) || dragged.IsQueued);
                bool bySlot = fromHand && AimOf(dragged) == DropAim.Slot;
                bool gift = dragged?.GetLinkedCardInstance() is ICreature { IsGift: true };
                foreach (var slot in battlefieldSlots) {
                    slot.SetEligible(bySlot && CanAimAt(dragged, slot.Model) && !(gift && slot.Model.IsOccupied()));
                }
                SetSideEligible(fromHand && dragged.GetLinkedCardInstance() is Spell anywhere && !EffectResolver.IsAimed(anywhere)
                                && dragged.Player != null && EffectResolver.Reaches(anywhere, dragged.Player, Player));
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

        /// <summary>
        /// A right click on a creature, or one on its way, with spells of the player's queued on it: the newest of them goes
        /// back to their hand (the user), and the click does nothing else. False when there's none of theirs.
        /// </summary>
        private bool TakeBackSpellOn(CardController card) {
            if (card == null || (GameManager?.TurnManager?.IsResolving ?? true)) return false;
            var queue = GameManager.ActionsQueue;
            for (int i = card.AttachedCards.Count - 1; i >= 0; i--) {
                if (i >= card.AttachedQueued.Count || !card.AttachedQueued[i]) continue;
                var play = QueuedPlayOf(card.AttachedCards[i]);
                if (play == null || !FogOfWar.CanControl(play.GetOwner())) continue;
                if (queue.CancelPlay(card.AttachedCards[i])) {
                    GameInput.SwallowRightClick();
                    return true;
                }
            }
            return false;
        }

        private void OnGhostBeginDrag(CardController ghost) {
            draggedGhostPlay = QueuedPlayOf(ghost.GetLinkedCardInstance());
            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas != null) ghost.transform.SetParent(rootCanvas.transform, true); // Drawn over every slot while dragged
            UITheme.Lift(ghost.gameObject, UITheme.HandsLayer + 1); // Carried, over the arrows and the hands as a hand card is
        }

        private void OnGhostEndDrag(CardController ghost) {
            UITheme.Lower(ghost.gameObject);
            if (!ghost.DragCancelled) PlayOnTarget(ghost); // Let go near a slot, not on one: it lands in the one picked
            // Dropped where it can't be played, it keeps its old play: take it back. A valid drop already replaced it.
            // Called off with a right click, it keeps it as it was
            if (!ghost.DragCancelled && draggedGhostPlay != null && QueuedPlayOf(ghost.GetLinkedCardInstance()) == draggedGhostPlay) {
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
            controller.OnCardRightClicked.AddListener(c => { if (!TakeBackSpellOn(c)) OnCardClicked(c); }); // Your spells on it come back first
            controller.OnRightClickedAcross.AddListener(c => TakeBackSpellOn(c));
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
    }
}
