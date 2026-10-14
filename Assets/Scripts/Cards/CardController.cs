using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    [Serializable]
    public class CardUnityEvent : UnityEvent<CardController> { }

    public class CardController : UIComponent, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IDragHandler, IBeginDragHandler, IEndDragHandler {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI typeText;
        [FormerlySerializedAs("descriptionText")]
        [SerializeField] private TextMeshProUGUI rulesText;
        [SerializeField] private TextMeshProUGUI statsText; // Attack / speed / health, in the box at the bottom
        [FormerlySerializedAs("speedText")]
        [SerializeField] private TextMeshProUGUI readyText; // Turns until operative, in the title bar where Magic prints the mana cost
        [SerializeField] private GameObject statsBox;
        [FormerlySerializedAs("speedPip")]
        [SerializeField] private GameObject readyPip;
        [SerializeField] private Image artImage;
        [SerializeField] private TextMeshProUGUI statusText; // Icons of the statuses currently on the creature
        [SerializeField] private Image cardImage;
        [SerializeField] private Image frameImage;   // Takes the card's faction color, like a Magic frame
        [SerializeField] private Image typeBarImage; // The type line's band, in the faction's full color

        // Like Magic cards, flavor text is only printed when the whole text box still fits at this size or larger
        private const float MinFlavorFontSize = 8f;

        private RectTransform rectTransform;
        private Canvas parentCanvas;
        private CanvasGroup canvasGroup;
        private Vector3 originalPosition;
        // Dragging, Slay the Spire style: the card trails the pointer a touch behind and leans into the way it's moving
        private const float DragFollow = 24f;        // How fast it catches up (per second, exponential)
        private const float DragLeanPerSpeed = 1.6f; // Degrees per world unit a second of sideways speed
        private const float MaxDragLean = 16f;
        private Vector3 dragTarget;
        private float dragLean;
        private Transform originalParent;
        private bool isDragging;
        /// <summary>The card being dragged, if any: while the player is doing something, no tooltip opens.</summary>
        public static CardController Dragged { get; private set; }
        /// <summary>Dragged with the right button: an attack on a creature of its own side (Friendly Fire).</summary>
        public bool IsFriendlyFire { get; set; }
        /// <summary>A card tucked under a creature on the board, only its name showing: hovered for its enlarged view, never used.</summary>
        public bool IsAttachment { get; private set; }
        /// <summary>A creature on the board: the cards tucked under it, oldest first, then the spells queued on it.</summary>
        public IReadOnlyList<ICard> AttachedCards { get; set; } = new List<ICard>();
        /// <summary>For each attached card, who played it (the side whose frame it shows).</summary>
        public IReadOnlyList<IPlayer> AttachedOwners { get; set; } = new List<IPlayer>();

        public void SetAttachment() => IsAttachment = true;
        // Store both the base definition and the live instance
        private CardData baseCardData; // The original ScriptableObject definition
        private ICreature linkedCreature; // The live Creature instance (if applicable)
        private ICard linkedCardInstance; // General ICard instance (could be Spell or Creature)
        private CardViews cardViews; // Made this card; gives its color and makes its previews

        // Queued (played but not yet resolved) visuals
        private const float QueuedAlphaHigh = 0.75f;
        private const float QueuedAlphaLow = 0.4f;
        private GameObject queuedBadge;
        private Tween queuedPulse;
        public bool IsQueued { get; private set; }
        private bool canDragWhileQueued; // A queued card in the hand can be dragged again, to re-aim or take back its play

        // Battlefield card about to be replaced by a summon (queued, or previewed while a creature is dragged over it)
        private const float ReplacedAlpha = 0.45f;
        private static readonly Color ReplaceLabelColor = UITheme.HealthLow;
        private GameObject replaceBadge;
        private bool isMarkedForReplacement;
        private bool isReplacementPreviewed;

        // Set once the card has left the battlefield and is fading out before being destroyed
        public bool IsLeavingPlay { get; private set; }

        public CardUnityEvent OnBeginDragEvent = new CardUnityEvent();
        public CardUnityEvent OnEndDragEvent = new CardUnityEvent();
        public CardUnityEvent OnCardDropped = new CardUnityEvent();
        public CardUnityEvent OnCardClicked = new CardUnityEvent(); // Pressed and released without dragging
        public CardUnityEvent OnCardRightClicked = new CardUnityEvent();
        public Action OnPointerEnterHandler;
        public Action OnPointerExitHandler;

        public Transform OriginalParent => originalParent;
        public ICreature GetLinkedCreature() => linkedCreature;
        public ICard GetLinkedCardInstance() => linkedCardInstance;
        public CardData GetBaseCardData() => baseCardData; // Expose base data if needed
        /// <summary>The rules text box, where keyword links are hovered (see <see cref="Tooltip"/>).</summary>
        public TMP_Text RulesText => rulesText;
        /// <summary>The enlarged copy shown while a card is hovered: it only shows the card, it's never played or hovered itself.</summary>
        public bool IsHoverPreview { get; private set; }
        public bool IsPlayer1Card() => Player?.IsPlayer1 ?? false;

        protected override void Awake() {
            base.Awake();
            SetupComponents();
        }

        private void SetupComponents() {
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            cardImage = GetComponent<Image>();
        }

        // Called by CardViews.Create
        public void Setup(ICard cardInstance, IPlayer owner, CardViews views) {
            cardViews = views;
            baseCardData = cardInstance.SourceData;
            linkedCardInstance = cardInstance;
            linkedCreature = cardInstance as ICreature;
            if (!views.IsDisplayOnly) base.Initialize(owner, views.Mediator, views.GameManager); // Starts listening to the linked creature

            UpdateUI();
        }

        // Only this card's creature: a board full of cards doesn't hear about every other creature
        protected override void RegisterEvents() {
            if (linkedCreature == null) return;
            linkedCreature.Changed -= OnLinkedCreatureChanged; // Never subscribe twice
            linkedCreature.Changed += OnLinkedCreatureChanged;
        }

        protected override void UnregisterEvents() {
            if (linkedCreature != null) linkedCreature.Changed -= OnLinkedCreatureChanged;
        }

        // Health, armor, stats, statuses, readiness or death changed: redraw the card, and the tooltip if it shows this card
        private void OnLinkedCreatureChanged(ICreature creature) {
            if (creature.IsDead) {
                // The card goes back to its printed values
                Log($"Creature {creature.Name} died, updating UI (TargetID: {creature.TargetId.ToUpper()}) (Card TargetID: {GetInstanceID()})", LogTag.Creatures | LogTag.UI);
                UnregisterEvents();
                linkedCreature = null;
                linkedCardInstance = null;
                UpdateUI();
                Tooltip.Existing?.HideTooltip();
                return;
            }

            UpdateUI();
            var tooltip = Tooltip.Existing;
            if (tooltip != null && tooltip.gameObject.activeSelf) {
                tooltip.UpdateTooltipContent(this);
            }
        }

        public override void UpdateUI(IPlayer player = null) {
            // Use baseCardData as the primary source, override with linkedCardInstance if available
            if (baseCardData == null) {
                LogWarning($"CardController {gameObject.name} has no baseCardData! Cannot update UI.", LogTag.UI | LogTag.Cards);
                nameText.text = "No Data";
                typeText.text = "";
                rulesText.text = "";
                statsBox.SetActive(false);
                ShowPip(false);
                return;
            }

            UpdateCardText();
            UpdateCardVisuals();
        }

        private void UpdateCardText() {
            nameText.text = baseCardData.cardName;
            typeText.text = CardTextFormatter.GetTypeLine(baseCardData);
            // In play, a creature's ability as it is now: one gained from a spell or another creature is written in, the printed
            // one struck through (and one switched off, struck through)
            var ability = linkedCreature?.Ability;
            // Switched off only on the board, ready to act (a card in the hand, or still deploying, isn't)
            bool abilityOff = linkedCreature?.Slot != null && !linkedCreature.IsDead && linkedCreature.IsOperative
                              && linkedCreature.Ability != Enums.CreatureAbility.None && !Abilities.IsActive(linkedCreature);
            rulesText.text = ComposeTextBox(CardTextFormatter.GetRulesText(baseCardData, ability, abilityOff), baseCardData.description);

            bool isCreature = baseCardData is CreatureData;
            statsBox.SetActive(isCreature);
            ShowPip(false);
            if (isCreature) {
                UpdateCreatureStats((CreatureData)baseCardData);
            }
            UpdateStatusIcons();
        }

        private void UpdateStatusIcons() {
            if (linkedCreature == null || GameManager?.ModifierManager == null) {
                statusText.text = "";
                return;
            }

            var viewer = FogOfWar.ViewerOf(linkedCreature.Owner);
            var icons = GameManager.ModifierManager.GetActiveModifiersFor(linkedCreature)
                .OfType<StatusEffectModifier>()
                .Where(status => !FogOfWar.HidesStatus(viewer, linkedCreature, status.EffectType))
                .Select(status => IconLink($"{StatusIconPrefix}{status.EffectType}", EffectIcons.ForStatus(status.EffectType), StatusNumber(status)))
                .ToList();
            // Raised or lowered stats: an arrow up or down, before the statuses
            var changes = StatChanges();
            if (changes.Any(c => c.Value < 0)) icons.Insert(0, IconLink(StatsDownIcon, EffectIcons.Debuff));
            if (changes.Any(c => c.Value > 0)) icons.Insert(0, IconLink(StatsUpIcon, EffectIcons.Buff));
            statusText.text = string.Concat(icons);
        }

        // Each icon is a link, so hovering it can tell what it stands for (see Tooltip and DescribeIcon)
        private const string StatusIconPrefix = "icon:status:";
        private const string StatsUpIcon = "icon:stats up";
        private const string StatsDownIcon = "icon:stats down";
        // A small number at the icon's foot: the stacks of one that stacks (Blessed, Heat), otherwise the Dusks it has left
        private static string IconLink(string id, string icon, int number = 0) => string.IsNullOrEmpty(icon) ? ""
            : $"<link=\"{id}\">{EffectIcons.Tag(icon)}{(number > 0 ? $"<size=55%><voffset=-0.35em><b>{number}</b></voffset></size>" : "")}</link>";

        private int StatusNumber(StatusEffectModifier status) {
            if (status.EffectType is Enums.StatusEffectType.Blessed or Enums.StatusEffectType.HeatSignature) return status.Stacks;
            return status.DusksLeft(GameManager?.TurnManager?.TurnNumber ?? status.TurnApplied);
        }

        /// <summary>The icons above the art (statuses and stat changes); each is a link <see cref="DescribeIcon"/> explains.</summary>
        public TMP_Text StatusText => statusText;

        // The stat changes on the live creature: from effects (some for a few Days) and the ones that stay on the card
        private List<(int Value, string Text)> StatChanges() {
            var changes = new List<(int, string)>();
            if (!CreatureIsAlive || IsDisguised || GameManager?.ModifierManager == null) return changes;
            int today = GameManager.TurnManager?.TurnNumber ?? 0;
            foreach (var stat in GameManager.ModifierManager.GetActiveModifiersFor(linkedCreature).OfType<StatModifier>()) {
                if (stat.Value == 0) continue;
                string percent = stat.CalculationType == ModifierCalculationType.Percentage ? "%" : "";
                string lasts = stat is TimedStatModifier timed
                    ? DaysLeft(timed.TurnApplied + timed.DurationInTurns - today)
                    : "while it's in play";
                changes.Add((stat.Value, $"{stat.Value:+0;-0}{percent} {StatName(stat.StatToModify)}, {lasts}"));
            }
            var kept = linkedCreature.PersistentChanges;
            if (kept.attack != 0) changes.Add((kept.attack, $"{kept.attack:+0;-0} Attack, for good (it stays on the card)"));
            if (kept.health != 0) changes.Add((kept.health, $"{kept.health:+0;-0} Health, for good (it stays on the card)"));
            if (kept.speed != 0) changes.Add((kept.speed, $"{kept.speed:+0;-0} Speed, for good (it stays on the card)"));
            return changes;
        }

        private static string StatName(ModifiableStat stat) => stat == ModifiableStat.Health ? "Health" : stat.ToString();
        private static string DaysLeft(int days) => days <= 1 ? "until Dusk" : $"{days} Days left, today included";

        /// <summary>What an icon above the art stands for, for its popup: a title and a few lines. Null for no such icon.</summary>
        public (string Title, string Body)? DescribeIcon(string id) {
            if (id == StatsUpIcon || id == StatsDownIcon) {
                var changes = StatChanges();
                if (changes.Count == 0) return null;
                var data = linkedCreature.SourceData as CreatureData;
                string printed = data != null ? $"\n<i>Printed {data.attack}/{data.speed}/{data.health}, now {linkedCreature.Attack}/{linkedCreature.Speed}/{linkedCreature.Health}.</i>" : "";
                return ("Stat changes", string.Join("\n", changes.Select(c => c.Text)) + printed);
            }
            if (id != null && id.StartsWith(StatusIconPrefix) && System.Enum.TryParse(id.Substring(StatusIconPrefix.Length), out Enums.StatusEffectType type)) {
                var status = GameManager?.ModifierManager?.GetActiveModifiersFor(linkedCreature).OfType<StatusEffectModifier>()
                    .FirstOrDefault(s => s.EffectType == type);
                if (status == null) return null;
                string title = System.Text.RegularExpressions.Regex.Replace(type.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
                string meaning = Keywords.Exists(title) ? "\n" + Keywords.Describe(title) : "";
                return (title, status.Description + meaning);
            }
            return null;
        }

        // Alive: with Health left, or on the board at 0 Health on Borrowed Time (Die Another Day)
        private bool CreatureIsAlive => linkedCreature != null && (linkedCreature.Health > 0 || (!linkedCreature.IsDead && linkedCreature.Slot != null));

        // Live stats while the creature is alive on the board, printed stats otherwise
        private void UpdateCreatureStats(CreatureData data) {
            bool isLive = CreatureIsAlive && !IsDisguised; // A decoy shows its fake card's numbers
            int attack = isLive ? linkedCreature.Attack : data.attack;
            int health = isLive ? linkedCreature.Health : data.health;
            int speed = isLive ? linkedCreature.Speed : data.speed;
            int armor = isLive ? linkedCreature.CurrentArmorPool : 0;
            int turnsUntilOperative = isLive ? linkedCreature.TurnsUntilOperative : data.turnsUntilOperative;

            string armorPart = armor > 0 ? $"<color=#2f7fc1>+{armor}</color>" : "";
            // Against the printed numbers: green when better, red when worse (damage included)
            statsText.text = $"{Compared(attack, data.attack)}/{Compared(speed, data.speed)}/{Compared(health, data.health)}{armorPart}";
            readyText.text = $"{EffectIcons.Tag(EffectIcons.Days)}{turnsUntilOperative}"; // An hourglass: Days until it's ready
            ShowPip(turnsUntilOperative > 0);
        }

        private const string BetterColor = "#1d7a2f";
        private const string WorseColor = "#b3261e";
        private static string Compared(int now, int printed) =>
            now > printed ? $"<color={BetterColor}>{now}</color>" : now < printed ? $"<color={WorseColor}>{now}</color>" : now.ToString();

        /// <summary>
        /// The Deployment Time box's height, width (an hourglass and the Days) and distance from the card's top-left corner
        /// (card units): the left end of the title bar, as tall as it, so it reads as part of the bar.
        /// </summary>
        public const float PipSize = 20f;
        public const float PipWidth = 38f;
        public const float PipInset = 8f;
        private const float TitleBarLeft = 8f; // Where the title bar starts, from the card's left edge
        private const float NamePadding = 4f;

        // The Deployment Time square shows only when there is one; the name is centered in whatever room the bar has left
        private void ShowPip(bool shown) {
            readyPip.SetActive(shown);
            var pipRect = (RectTransform)readyPip.transform; // Also for a prefab baked when it was a square
            pipRect.sizeDelta = new Vector2(PipWidth, PipSize);
            pipRect.anchoredPosition = new Vector2(PipInset + PipWidth / 2f, -(PipInset + PipSize / 2f));
            var nameRect = nameText.rectTransform;
            float left = shown ? PipInset + PipWidth - TitleBarLeft + NamePadding : NamePadding;
            nameRect.offsetMin = new Vector2(left, nameRect.offsetMin.y);
        }

        // Rules text always fits: the text box auto-sizes its font down to its minimum.
        // Flavor text is extra and is left out when it would make the text too small to read.
        private string ComposeTextBox(string rules, string flavor) {
            if (string.IsNullOrEmpty(flavor)) return rules;

            string flavorLine = $"<i><color=#5a5146>{flavor}</color></i>";
            string withFlavor = string.IsNullOrEmpty(rules) ? flavorLine : $"{rules}\n<size=90%>{flavorLine}</size>";
            if (FitsInTextBox(withFlavor, MinFlavorFontSize)) return withFlavor;
            return string.IsNullOrEmpty(rules) ? flavorLine : rules;
        }

        private bool FitsInTextBox(string text, float fontSize) {
            float originalSize = rulesText.fontSize;
            bool originalAutoSize = rulesText.enableAutoSizing;
            rulesText.enableAutoSizing = false;
            rulesText.fontSize = fontSize;

            var box = rulesText.rectTransform.rect;
            var margin = rulesText.margin; // left, top, right, bottom
            float width = box.width - margin.x - margin.z;
            float height = box.height - margin.y - margin.w;
            bool fits = rulesText.GetPreferredValues(text, width, 0f).y <= height;

            rulesText.fontSize = originalSize;
            rulesText.enableAutoSizing = originalAutoSize;
            return fits;
        }

        private void UpdateCardVisuals() {
            if (frameImage != null) frameImage.color = UITheme.CardFrameColor(baseCardData.faction);
            if (typeBarImage != null) typeBarImage.color = UITheme.FactionColor(baseCardData.faction);
            ApplyShape(baseCardData is SpellData);

            artImage.sprite = baseCardData.artwork;
            artImage.color = baseCardData.artwork != null ? Color.white : new Color(0.25f, 0.25f, 0.3f);
        }

        #region Art window
        // Like Slay the Spire's attack, skill and power frames, the art window's shape says what the card is, with no icon to
        // read: a creature's window comes to a point at the bottom, like a pennant; a spell's ends in a shallow arch.
        // A cover in the frame's color hides the art outside the window, and a brass trim runs along its edge.
        private Image windowCover, windowTrim;

        /// <summary>Whether the card shows a spell's art window (arched) rather than a creature's (pointed).</summary>
        public bool IsSpellShaped { get; private set; }

        private void ApplyShape(bool spell) {
            IsSpellShaped = spell;
            if (artImage == null) return;
            var artFrame = artImage.transform.parent;
            if (windowCover == null) {
                windowCover = UITheme.NewImage("WindowCover", artFrame, Color.white);
                windowTrim = UITheme.NewImage("WindowTrim", artFrame, UITheme.GoldDim);
                foreach (var part in new[] { windowCover, windowTrim }) {
                    UITheme.Stretch(part.rectTransform);
                    part.transform.SetSiblingIndex(artImage.transform.GetSiblingIndex() + 1); // Over the art, under the status icons
                }
                windowTrim.transform.SetSiblingIndex(windowCover.transform.GetSiblingIndex() + 1);
                var rectTrim = artFrame.Find("Trim");
                if (rectTrim != null) rectTrim.gameObject.SetActive(false); // The window's own trim replaces it
            }
            windowCover.sprite = spell ? UITheme.SpellWindowCover : UITheme.CreatureWindowCover;
            windowTrim.sprite = spell ? UITheme.SpellWindowTrim : UITheme.CreatureWindowTrim;
            windowCover.color = UITheme.CardFrameColor(baseCardData.faction);
        }
        #endregion

        public void OnBeginDrag(PointerEventData eventData) {
            if (IsHoverPreview || IsAttachment) {
                eventData.pointerDrag = null;
                return;
            }
            // Right-drag: a creature on the board turns on one of its own side (Friendly Fire), with no targeting help
            bool friendlyFire = eventData.button == PointerEventData.InputButton.Right && linkedCreature?.Slot != null;
            if (eventData.button != PointerEventData.InputButton.Left && !friendlyFire) return;
            IsFriendlyFire = friendlyFire;

            // Queued cards are committed (except in the hand, where dragging re-aims or takes back the play),
            // and nothing can be played while a turn resolves; cancel the drag so no drop zone receives the card
            if ((IsQueued && !canDragWhileQueued) || (GameManager?.TurnManager?.IsResolving ?? false) || !FogOfWar.CanControl(Player)
                || (cardViews?.IsDisplayOnly ?? false)) {
                eventData.pointerDrag = null;
                return;
            }

            if (canvasGroup == null) {
                LogWarning($"CanvasGroup is missing, adding it now (Card TargetID: {GetInstanceID()})", LogTag.UI);
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            originalPosition = transform.position;
            originalParent = transform.parent;
            dragTarget = transform.position;
            dragLean = 0f;
            isDragging = true;
            Dragged = this;
            canvasGroup.blocksRaycasts = false;
            transform.SetAsLastSibling();

            // Hide tooltip when dragging starts
            Tooltip.Existing?.HideTooltip();

            OnBeginDragEvent.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData) {
            if (!isDragging) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                eventData.position,
                parentCanvas.worldCamera,
                out Vector2 localPoint)) {
                dragTarget = parentCanvas.transform.TransformPoint(localPoint); // It follows in Update
            }
        }

        private void Update() {
            if (!isDragging) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            var before = transform.position;
            transform.position = Vector3.Lerp(before, dragTarget, 1f - Mathf.Exp(-DragFollow * dt));
            float speed = (transform.position.x - before.x) / dt;
            float lean = Mathf.Clamp(-speed * DragLeanPerSpeed, -MaxDragLean, MaxDragLean);
            dragLean = Mathf.Lerp(dragLean, lean, 1f - Mathf.Exp(-14f * dt));
            transform.localRotation = Quaternion.Euler(0f, 0f, dragLean);
        }

        public void OnEndDrag(PointerEventData eventData) {
            if (!isDragging) return;

            isDragging = false;
            transform.position = dragTarget; // Where the pointer let go
            transform.localRotation = Quaternion.identity;
            if (Dragged == this) Dragged = null;
            canvasGroup.blocksRaycasts = true;

            OnEndDragEvent.Invoke(this);
            OnCardDropped.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData) {
            if (eventData.dragging || IsHoverPreview || IsAttachment) return;
            if (!FogOfWar.CanControl(Player)) return; // The other side's cards aren't yours to use
            if (eventData.button == PointerEventData.InputButton.Left) OnCardClicked.Invoke(this);
            else if (eventData.button == PointerEventData.InputButton.Right) OnCardRightClicked.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (!isDragging && !IsHoverPreview) {
                Tooltip.Shared.ShowTooltip(this);

                OnPointerEnterHandler?.Invoke();
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (!isDragging && !IsHoverPreview) {
                Tooltip.Existing?.ReleaseCard(this); // The pointer may be on its way to the preview, to read its keywords
                OnPointerExitHandler?.Invoke();
            }
        }

        #region Play Visuals
        /// <summary>
        /// Shows the card as played-but-unresolved: translucent, pulsing, with a "QUEUED" badge (or another word, like
        /// "MOVING" on a move ghost).
        /// </summary>
        public void SetQueued(bool queued, bool blockRaycasts = true, bool canBeDragged = false, string badge = "Queued") {
            IsQueued = queued;
            canDragWhileQueued = queued && canBeDragged;
            queuedPulse?.Kill();
            queuedPulse = null;

            if (queued) {
                canvasGroup.alpha = QueuedAlphaHigh;
                canvasGroup.blocksRaycasts = blockRaycasts;
                queuedPulse = FadeTo(QueuedAlphaLow, 0.8f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetTarget(transform);
                if (queuedBadge == null) queuedBadge = CreateBadge("QueuedBadge", badge, UITheme.Gold, 0.4f, 0.6f);
                queuedBadge.GetComponentInChildren<TextMeshProUGUI>().text = badge;
                queuedBadge.SetActive(true);
            }
            else {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                if (queuedBadge != null) queuedBadge.SetActive(false);
            }
        }

        /// <summary>
        /// Marks this battlefield card as replaced by a queued summon into its slot.
        /// </summary>
        public void SetMarkedForReplacement(bool marked) {
            isMarkedForReplacement = marked;
            RefreshReplacementVisual();
        }

        /// <summary>
        /// Previews the replacement while a creature from hand is dragged over this card's slot.
        /// </summary>
        public void SetReplacementPreview(bool previewed) {
            isReplacementPreviewed = previewed;
            RefreshReplacementVisual();
        }

        private void RefreshReplacementVisual() {
            if (IsLeavingPlay) return;

            bool show = isMarkedForReplacement || isReplacementPreviewed;
            canvasGroup.alpha = show ? ReplacedAlpha : 1f;
            // Bottom strip, so it stays visible under the raised ghost of the incoming creature
            if (show && replaceBadge == null) replaceBadge = CreateBadge("ReplaceBadge", "Replace", ReplaceLabelColor, 0.02f, 0.22f);
            if (replaceBadge != null) replaceBadge.SetActive(show);
        }

        /// <summary>Fog of War: this creature plans something the screen can't show. A "???" band across its art.</summary>
        public void SetPlansHidden(bool hidden) {
            ArePlansHidden = hidden;
            if (hidden && hiddenPlansBadge == null) hiddenPlansBadge = CreateBadge("HiddenPlansBadge", "???", UITheme.Gold, 0.6f, 0.76f);
            if (hiddenPlansBadge != null) hiddenPlansBadge.SetActive(hidden && !IsAnonymous); // A card already all ??? needs no band
        }

        public bool ArePlansHidden { get; private set; }
        private GameObject hiddenPlansBadge;

        #region Fog of War faces
        /// <summary>
        /// Fog of War: the player at the screen doesn't know what this creature is (being summoned by the other side, or stealthy).
        /// It shows as an empty black card with three question marks, and hovering it tells nothing.
        /// </summary>
        public void SetAnonymous(bool anonymous) {
            if (anonymous == IsAnonymous) return;
            IsAnonymous = anonymous;
            if (anonymous && unknownFace == null) unknownFace = CreateFace("UnknownFace", "FogOfWar/UnknownCreature");
            ShowFace(anonymous ? unknownFace : null);
        }

        /// <summary>Fog of War: a card in the enemy's hand. Only its back shows, and hovering it tells nothing.</summary>
        public void SetFaceDown(bool faceDown) {
            if (faceDown == IsFaceDown) return;
            IsFaceDown = faceDown;
            if (faceDown && backFace == null) backFace = CreateFace("CardBack", "FogOfWar/CardBack");
            ShowFace(faceDown ? backFace : null);
        }

        /// <summary>
        /// Fog of War: a decoy the player at the screen doesn't see through. The card shows the card it pretends to be, with
        /// that card's printed stats, and nothing of its own (null: its own face again).
        /// </summary>
        public void SetDisguise(CardData fake) {
            if (fake == disguise) return;
            if (disguise == null) realCardData = baseCardData;
            disguise = fake;
            baseCardData = fake ?? realCardData ?? baseCardData;
            UpdateUI();
        }

        public bool IsDisguised => disguise != null;
        private CardData disguise, realCardData;

        public bool IsAnonymous { get; private set; }
        public bool IsFaceDown { get; private set; }
        private GameObject unknownFace, backFace;
        private readonly List<GameObject> hiddenFace = new List<GameObject>();

        // Shows one of the Fog of War faces instead of the card's own (or the card's own again, for null). The card's parts are
        // switched off rather than covered: a queued card is translucent, and they would show through
        private void ShowFace(GameObject face) {
            foreach (var part in hiddenFace) if (part != null) part.SetActive(true);
            hiddenFace.Clear();
            if (unknownFace != null) unknownFace.SetActive(false);
            if (backFace != null) backFace.SetActive(false);
            if (face == null) return;

            foreach (Transform child in transform) {
                if (!child.gameObject.activeSelf) continue;
                hiddenFace.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }
            face.SetActive(true);
            face.transform.SetAsLastSibling();
        }

        // The whole card, drawn by Tools/generate_card_art.py
        private GameObject CreateFace(string objectName, string spritePath) {
            var face = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            face.layer = gameObject.layer;
            face.transform.SetParent(transform, false);
            var rect = (RectTransform)face.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = face.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(spritePath);
            image.color = image.sprite != null ? Color.white : UITheme.TableDark;
            image.raycastTarget = false;
            face.SetActive(false);
            return face;
        }
        #endregion

        // A navy band across the card with brass edges and a word on it
        /// <summary>
        /// A reminder: this creature could act this Day (attack, move, use its ability) and has nothing planned. A band across
        /// the top of its art, pulsing softly, while its player plans.
        /// </summary>
        public void SetIdle(bool idle) {
            IsIdle = idle;
            if (idle && idleBadge == null) {
                idleBadge = CreateBadge("IdleBadge", "Idle zZ", UITheme.Parchment, 0.66f, 0.77f); // On the art, under the name
                var group = idleBadge.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                DOTween.To(() => group.alpha, a => group.alpha = a, 0.55f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(idleBadge);
            }
            if (idleBadge != null) idleBadge.SetActive(idle);
        }

        public bool IsIdle { get; private set; }
        private GameObject idleBadge;

        /// <summary>A queued summon's place in the order its side's creatures arrive (null hides it), like an attack arrow's number.</summary>
        public void SetOrderNumber(int? number) {
            if (number == null) {
                if (orderBadge != null) orderBadge.SetActive(false);
                return;
            }
            if (orderBadge == null) {
                var image = UITheme.NewImage("OrderNumber", transform, UITheme.WithAlpha(UITheme.Hex(0x141420), 0.82f));
                UITheme.Round(image, image.color, 2f);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.6f);
                rect.sizeDelta = new Vector2(30f, 30f);
                var label = UITheme.NewText("Number", image.transform, "", 22f, Color.white);
                label.fontStyle = FontStyles.Bold;
                UITheme.Stretch(label.rectTransform);
                orderBadge = image.gameObject;
            }
            orderBadge.transform.SetAsLastSibling();
            orderBadge.GetComponentInChildren<TextMeshProUGUI>().text = number.Value.ToString();
            orderBadge.SetActive(true);
        }

        /// <summary>The number shown by <see cref="SetOrderNumber"/>, if any.</summary>
        public string OrderNumber => orderBadge != null && orderBadge.activeSelf ? orderBadge.GetComponentInChildren<TextMeshProUGUI>().text : null;
        private GameObject orderBadge;

        private GameObject CreateBadge(string objectName, string text, Color textColor, float anchorMinY, float anchorMaxY) {
            var badge = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            badge.layer = gameObject.layer;
            badge.transform.SetParent(transform, false);
            var badgeRect = (RectTransform)badge.transform;
            badgeRect.anchorMin = new Vector2(0f, anchorMinY);
            badgeRect.anchorMax = new Vector2(1f, anchorMaxY);
            badgeRect.offsetMin = badgeRect.offsetMax = Vector2.zero;
            var badgeImage = badge.GetComponent<Image>();
            badgeImage.color = UITheme.WithAlpha(UITheme.TableDark, 0.92f);
            badgeImage.raycastTarget = false;
            foreach (float edge in new[] { 0f, 1f }) {
                var line = UITheme.NewImage("Edge", badge.transform, UITheme.Gold);
                line.rectTransform.anchorMin = new Vector2(0f, edge);
                line.rectTransform.anchorMax = new Vector2(1f, edge);
                line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            }

            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.gameObject.layer = gameObject.layer;
            label.transform.SetParent(badge.transform, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.text = text;
            label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            label.characterSpacing = 6f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = textColor;
            label.enableAutoSizing = true;
            label.fontSizeMin = 4;
            label.fontSizeMax = 24;
            label.raycastTarget = false;
            return badge;
        }

        /// <summary>
        /// Pops the card into place when its creature is summoned.
        /// </summary>
        public void PlaySummonAnimation(float delay = 0f) {
            Vector3 endScale = transform.localScale;
            transform.localScale = endScale * 0.5f;
            canvasGroup.alpha = 0f;

            DOTween.Sequence().SetTarget(transform)
                .AppendInterval(delay)
                .Append(FadeTo(1f, 0.15f))
                .Join(transform.DOScale(endScale * 1.15f, 0.2f).SetEase(Ease.OutQuad))
                .Append(transform.DOScale(endScale, 0.15f).SetEase(Ease.InOutQuad));
        }

        /// <summary>
        /// Shrinks and fades the card out, then destroys it. Used when a creature leaves the battlefield.
        /// </summary>
        public void PlayLeavePlayAnimation() {
            if (IsLeavingPlay) return;
            IsLeavingPlay = true;

            if (replaceBadge != null) replaceBadge.SetActive(false);
            canvasGroup.blocksRaycasts = false;
            Tooltip.Existing?.HideTooltip();
            DOTween.Kill(transform);

            DOTween.Sequence().SetTarget(transform)
                .Append(FadeTo(0f, 0.3f))
                .Join(transform.DOScale(transform.localScale * 0.6f, 0.3f).SetEase(Ease.InBack))
                .OnComplete(() => Destroy(gameObject));
        }

        /// <summary>
        /// Slay the Spire's cast: the card leaps out of the hand to the point, straightening and swelling with a little
        /// overshoot, holds a beat, then snaps shut into a burst of light and is gone. Used when a spell resolves.
        /// It swells to a quarter more than its size, or to <paramref name="shownScale"/> (in the fly root's units) when
        /// given, for a card that starts small, like a spell's ghost on the board.
        /// </summary>
        public void PlayCastAnimation(Transform flyRoot, Vector3 worldTarget, float delay = 0f, float? shownScale = null) {
            SetQueued(false);
            canvasGroup.blocksRaycasts = false;
            Tooltip.Existing?.HideTooltipFor(this);
            DOTween.Kill(transform);

            float unit = CardFx.UnitOf(transform.parent, flyRoot);
            transform.SetParent(flyRoot, true);
            transform.SetAsLastSibling();
            worldTarget.z = transform.position.z;
            Vector3 shown = shownScale.HasValue ? Vector3.one * shownScale.Value : transform.localScale * 1.25f;

            DOTween.Sequence().SetTarget(transform)
                .AppendInterval(delay)
                .Append(transform.DOMove(worldTarget, 0.3f).SetEase(Ease.OutCubic))
                .Join(transform.DORotate(Vector3.zero, 0.3f).SetEase(Ease.OutCubic))
                .Join(transform.DOScale(shown, 0.3f).SetEase(Ease.OutBack))
                .Append(transform.DOPunchScale(shown * 0.08f, 0.22f, 6, 0.5f))
                .AppendCallback(() => CardFx.Burst(flyRoot, transform.position, 0.12f, unit))
                .Append(transform.DOScale(shown * 0.15f, 0.2f).SetEase(Ease.InBack))
                .Join(transform.DORotate(new Vector3(0f, 0f, -30f), 0.2f))
                .Join(FadeTo(0f, 0.2f).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(gameObject));
        }

        private Tween FadeTo(float alpha, float duration) {
            return DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, alpha, duration);
        }
        #endregion

        // Make sure to clean up when the card is destroyed
        protected override void OnDestroy() {
            if (Dragged == this) Dragged = null;
            Tooltip.Existing?.HideTooltipFor(this);
            DOTween.Kill(transform);
            if (idleBadge != null) DOTween.Kill(idleBadge);
            CleanupEvents();

            base.OnDestroy(); // Unregisters mediator events
        }

        // --- Hover preview ---
        /// <summary>
        /// A non-interactive copy of this card, linked to the same live instance so it shows current stats.
        /// Used for the enlarged preview shown while hovering.
        /// </summary>
        public CardController CreatePreview(Transform parent) {
            var preview = cardViews.Create(linkedCardInstance, Player, parent);
            if (preview == null) return null;
            preview.SetDisguise(disguise); // Enlarged, a decoy still shows its fake card

            preview.IsHoverPreview = true;
            var group = preview.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true; // The pointer can rest on it without hovering the board underneath
            group.interactable = false;
            return preview;
        }

        /// <summary>An enlarged, non-interactive copy of another card (one tucked under this one), for the hover preview.</summary>
        public CardController CreatePreviewOf(ICard card, IPlayer owner, Transform parent) {
            var preview = cardViews.Create(card, owner ?? Player, parent);
            if (preview == null) return null;
            preview.IsHoverPreview = true;
            var group = preview.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = false;
            return preview;
        }

        /// <summary>What the card face can't show: the modifiers currently affecting this creature. Empty if none.</summary>
        public string GetActiveModifiersText() {
            if (!CreatureIsAlive || GameManager?.ModifierManager == null || IsDisguised) return "";

            var viewer = FogOfWar.ViewerOf(linkedCreature.Owner);
            var modifiers = GameManager.ModifierManager.GetActiveModifiersFor(linkedCreature)
                .Where(m => !(m is StatusEffectModifier s && FogOfWar.HidesStatus(viewer, linkedCreature, s.EffectType))).ToList();
            var lines = modifiers.Select(m => {
                string icon = m is StatusEffectModifier status ? EffectIcons.Tag(EffectIcons.ForStatus(status.EffectType)) : "";
                return $"{icon}{m.Description}";
            }).ToList(); // Its ability as it is now (gained, lost, switched off) is written in its text (UpdateCardText)

            if (lines.Count == 0) return "";
            return "<b>Active effects</b>\n" + string.Join("\n", lines);
        }

        private void CleanupEvents() {
            OnBeginDragEvent.RemoveAllListeners();
            OnEndDragEvent.RemoveAllListeners();
            OnCardDropped.RemoveAllListeners();
            OnCardClicked.RemoveAllListeners();
            OnCardRightClicked.RemoveAllListeners();
            OnPointerEnterHandler = null;
            OnPointerExitHandler = null;
        }
    }
}
