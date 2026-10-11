using DG.Tweening;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

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

        // Like Magic cards, flavor text is only printed when the whole text box still fits at this size or larger
        private const float MinFlavorFontSize = 8f;

        private RectTransform rectTransform;
        private Canvas parentCanvas;
        private CanvasGroup canvasGroup;
        private Vector3 originalPosition;
        private Transform originalParent;
        private bool isDragging;
        // Store both the base definition and the live instance
        private CardData baseCardData; // The original ScriptableObject definition
        private ICreature linkedCreature; // The live Creature instance (if applicable)
        private ICard linkedCardInstance; // General ICard instance (could be Spell or Creature)
        private Tooltip tooltip;

        // Queued (played but not yet resolved) visuals
        private const float QueuedAlphaHigh = 0.75f;
        private const float QueuedAlphaLow = 0.4f;
        private GameObject queuedBadge;
        private Tween queuedPulse;
        public bool IsQueued { get; private set; }

        // Battlefield card about to be replaced by a summon (queued, or previewed while a creature is dragged over it)
        private const float ReplacedAlpha = 0.45f;
        private static readonly Color ReplaceLabelColor = new Color(1f, 0.45f, 0.35f);
        private GameObject replaceBadge;
        private bool isMarkedForReplacement;
        private bool isReplacementPreviewed;

        // Set once the card has left the battlefield and is fading out before being destroyed
        public bool IsLeavingPlay { get; private set; }

        public CardUnityEvent OnBeginDragEvent = new CardUnityEvent();
        public CardUnityEvent OnEndDragEvent = new CardUnityEvent();
        public CardUnityEvent OnCardDropped = new CardUnityEvent();
        public CardUnityEvent OnCardClicked = new CardUnityEvent(); // Pressed and released without dragging
        public Action OnPointerEnterHandler;
        public Action OnPointerExitHandler;

        public Transform OriginalParent => originalParent;
        public ICreature GetLinkedCreature() => linkedCreature;
        public ICard GetLinkedCardInstance() => linkedCardInstance;
        public CardData GetBaseCardData() => baseCardData; // Expose base data if needed
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

        // Updated Setup method to accept ICard and store both base data and linked instance
        public void Setup(CardData data, IPlayer owner, ICard cardInstance, IGameMediator mediator, IGameReferences references, IGameManager manager) {
            // Call base Initialize with dependencies FIRST
            // Pass all required dependencies, including the manager
            base.Initialize(owner, mediator, references, manager);

            // Now do CardController specific setup
            baseCardData = data;
            linkedCardInstance = cardInstance; // Store the passed instance
            linkedCreature = cardInstance as ICreature; // Try casting to ICreature

            UpdateUI();
        }

        protected override void RegisterEvents() {
            if (GameMediator != null) {
                GameMediator.AddCreatureDamagedListener(OnCreatureDamaged);
                GameMediator.AddCreatureHealedListener(OnCreatureHealed);
                GameMediator.AddCreatureDiedListener(OnCreatureDied);
                GameMediator.AddCreatureArmorChangedListener(OnCreatureArmorChanged);
                GameMediator.AddCreatureStatsChangedListener(OnCreatureStatsChanged);
                GameMediator.AddCreatureStatusChangedListener(OnCreatureStatusChanged);
            }
        }

        protected override void UnregisterEvents() {
            if (GameMediator != null) {
                GameMediator.RemoveCreatureDamagedListener(OnCreatureDamaged);
                GameMediator.RemoveCreatureHealedListener(OnCreatureHealed);
                GameMediator.RemoveCreatureDiedListener(OnCreatureDied);
                GameMediator.RemoveCreatureArmorChangedListener(OnCreatureArmorChanged);
                GameMediator.RemoveCreatureStatsChangedListener(OnCreatureStatsChanged);
                GameMediator.RemoveCreatureStatusChangedListener(OnCreatureStatusChanged);
            }
        }

        private bool IsLinkedTo(ICreature creature) => linkedCreature != null && creature != null && creature.TargetId == linkedCreature.TargetId;

        private void OnCreatureDamaged(ICreature creature, int damage) => RefreshIfLinked(creature);

        private void OnCreatureHealed(ICreature creature, int amount) => RefreshIfLinked(creature);

        private void OnCreatureStatsChanged(ICreature creature) => RefreshIfLinked(creature);

        // Health or stats changed: redraw the card and the tooltip if it is showing this card
        private void RefreshIfLinked(ICreature creature) {
            if (!IsLinkedTo(creature)) return;

            UpdateUI();
            var tooltip = GetTooltip();
            if (tooltip != null && tooltip.gameObject.activeSelf) {
                tooltip.UpdateTooltipContent(this);
            }
        }

        private void OnCreatureArmorChanged(ICreature creature, int newArmor) {
            if (!IsLinkedTo(creature)) return;

            Log($"Creature {creature.Name} armor changed to {newArmor}, updating UI (TargetID: {creature.TargetId.ToUpper()}) (Card TargetID: {GetInstanceID()})", LogTag.Creatures | LogTag.UI | LogTag.Effects);
            UpdateUI();
        }

        private void OnCreatureStatusChanged(ICreature creature, StatusEffectType status, bool applied) {
            if (IsLinkedTo(creature)) UpdateStatusIcons();
        }

        private void OnCreatureDied(ICreature creature) {
            if (!IsLinkedTo(creature)) return;

            Log($"Creature {creature.Name} died, updating UI (TargetID: {creature.TargetId.ToUpper()}) (Card TargetID: {GetInstanceID()})", LogTag.Creatures | LogTag.UI);
            linkedCreature = null;
            linkedCardInstance = null;
            UpdateUI();
            GetTooltip()?.HideTooltip();
        }

        // Null once GameReferences or the cached tooltip has been destroyed
        private Tooltip GetTooltip() {
            if (tooltip == null && GameReferences is UnityEngine.Object references && references != null) {
                tooltip = GameReferences.GetTooltip();
            }
            return tooltip != null ? tooltip : null;
        }

        public override void UpdateUI(IPlayer player = null) {
            // Use baseCardData as the primary source, override with linkedCardInstance if available
            if (baseCardData == null) {
                LogWarning($"CardController {gameObject.name} has no baseCardData! Cannot update UI.", LogTag.UI | LogTag.Cards);
                nameText.text = "No Data";
                typeText.text = "";
                rulesText.text = "";
                statsBox.SetActive(false);
                readyPip.SetActive(false);
                return;
            }

            UpdateCardText();
            UpdateCardVisuals();
        }

        private void UpdateCardText() {
            nameText.text = baseCardData.cardName;
            typeText.text = CardTextFormatter.GetTypeLine(baseCardData);
            rulesText.text = ComposeTextBox(CardTextFormatter.GetRulesText(baseCardData), baseCardData.description);

            bool isCreature = baseCardData is CreatureData;
            statsBox.SetActive(isCreature);
            readyPip.SetActive(isCreature);
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

            var icons = GameManager.ModifierManager.GetActiveModifiersFor(linkedCreature)
                .OfType<StatusEffectModifier>()
                .Select(status => EffectIcons.Tag(EffectIcons.ForStatus(status.EffectType)));
            statusText.text = string.Concat(icons);
        }

        // Live stats while the creature is alive on the board, printed stats otherwise
        private void UpdateCreatureStats(CreatureData data) {
            bool isLive = linkedCreature != null && linkedCreature.Health > 0;
            int attack = isLive ? linkedCreature.Attack : data.attack;
            int health = isLive ? linkedCreature.Health : data.health;
            int speed = isLive ? linkedCreature.Speed : data.speed;
            int armor = isLive ? linkedCreature.CurrentArmorPool : 0;
            int turnsUntilOperative = isLive ? linkedCreature.TurnsUntilOperative : data.turnsUntilOperative;

            string armorPart = armor > 0 ? $"<color=#2f7fc1>+{armor}</color>" : "";
            statsText.text = $"{attack}/{speed}/{health}{armorPart}";
            readyText.text = turnsUntilOperative.ToString();
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
            // The outer border shows who owns the card
            if (cardImage != null && Player != null && GameReferences != null) {
                cardImage.color = GameReferences.GetCardColor(Player);
            }

            artImage.sprite = baseCardData.artwork;
            artImage.color = baseCardData.artwork != null ? Color.white : new Color(0.25f, 0.25f, 0.3f);
        }

        public void OnBeginDrag(PointerEventData eventData) {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            // Queued cards are already committed, and nothing can be played while a turn resolves;
            // cancel the drag so no drop zone receives the card
            if (IsQueued || (GameManager?.TurnManager?.IsResolving ?? false)) {
                eventData.pointerDrag = null;
                return;
            }

            if (canvasGroup == null) {
                LogWarning($"CanvasGroup is missing, adding it now (Card TargetID: {GetInstanceID()})", LogTag.UI);
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            originalPosition = transform.position;
            originalParent = transform.parent;
            isDragging = true;
            canvasGroup.blocksRaycasts = false;
            transform.SetAsLastSibling();

            // Hide tooltip when dragging starts
            GetTooltip()?.HideTooltip();

            OnBeginDragEvent.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData) {
            if (!isDragging) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                eventData.position,
                parentCanvas.worldCamera,
                out Vector2 localPoint)) {
                transform.position = parentCanvas.transform.TransformPoint(localPoint);
            }
        }

        public void OnEndDrag(PointerEventData eventData) {
            if (!isDragging) return;

            isDragging = false;
            canvasGroup.blocksRaycasts = true;

            OnEndDragEvent.Invoke(this);
            OnCardDropped.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData) {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging) return;
            OnCardClicked.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (!isDragging) {
                // Always show tooltip on hover
                var tooltip = GetTooltip();
                if (tooltip != null) {
                    tooltip.ShowTooltip(this);
                }

                OnPointerEnterHandler?.Invoke();
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (!isDragging) {
                GetTooltip()?.HideTooltip();
                OnPointerExitHandler?.Invoke();
            }
        }

        #region Play Visuals
        /// <summary>
        /// Shows the card as played-but-unresolved: translucent, pulsing, with a "QUEUED" badge.
        /// </summary>
        public void SetQueued(bool queued, bool blockRaycasts = true) {
            IsQueued = queued;
            queuedPulse?.Kill();
            queuedPulse = null;

            if (queued) {
                canvasGroup.alpha = QueuedAlphaHigh;
                canvasGroup.blocksRaycasts = blockRaycasts;
                queuedPulse = FadeTo(QueuedAlphaLow, 0.8f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetTarget(transform);
                if (queuedBadge == null) queuedBadge = CreateBadge("QueuedBadge", "QUEUED", new Color(0.95f, 0.78f, 0.35f), 0.4f, 0.6f);
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
            if (show && replaceBadge == null) replaceBadge = CreateBadge("ReplaceBadge", "REPLACE", ReplaceLabelColor, 0.02f, 0.22f);
            if (replaceBadge != null) replaceBadge.SetActive(show);
        }

        private GameObject CreateBadge(string objectName, string text, Color textColor, float anchorMinY, float anchorMaxY) {
            var badge = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            badge.layer = gameObject.layer;
            badge.transform.SetParent(transform, false);
            var badgeRect = (RectTransform)badge.transform;
            badgeRect.anchorMin = new Vector2(0f, anchorMinY);
            badgeRect.anchorMax = new Vector2(1f, anchorMaxY);
            badgeRect.offsetMin = badgeRect.offsetMax = Vector2.zero;
            var badgeImage = badge.GetComponent<Image>();
            badgeImage.color = new Color(0.08f, 0.06f, 0.1f, 0.9f);
            badgeImage.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.gameObject.layer = gameObject.layer;
            label.transform.SetParent(badge.transform, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.text = text;
            label.fontStyle = FontStyles.Bold;
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
            GetTooltip()?.HideTooltip();
            DOTween.Kill(transform);

            DOTween.Sequence().SetTarget(transform)
                .Append(FadeTo(0f, 0.3f))
                .Join(transform.DOScale(transform.localScale * 0.6f, 0.3f).SetEase(Ease.InBack))
                .OnComplete(() => Destroy(gameObject));
        }

        /// <summary>
        /// Detaches the card, flies it to a point, flashes it and destroys it. Used when a spell resolves.
        /// </summary>
        public void PlayCastAnimation(Transform flyRoot, Vector3 worldTarget, float delay = 0f) {
            SetQueued(false);
            canvasGroup.blocksRaycasts = false;
            GetTooltip()?.HideTooltip();

            transform.SetParent(flyRoot, true);
            transform.SetAsLastSibling();
            worldTarget.z = transform.position.z;
            Vector3 baseScale = transform.localScale;

            DOTween.Sequence().SetTarget(transform)
                .AppendInterval(delay)
                .Append(transform.DOMove(worldTarget, 0.35f).SetEase(Ease.OutCubic))
                .Join(transform.DOScale(baseScale * 1.35f, 0.35f).SetEase(Ease.OutBack))
                .AppendInterval(0.25f)
                .Append(FadeTo(0f, 0.25f))
                .Join(transform.DOScale(baseScale * 1.6f, 0.25f))
                .OnComplete(() => Destroy(gameObject));
        }

        private Tween FadeTo(float alpha, float duration) {
            return DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, alpha, duration);
        }
        #endregion

        // Make sure to clean up when the card is destroyed
        protected override void OnDestroy() {
            GetTooltip()?.HideTooltip();
            DOTween.Kill(transform);
            CleanupEvents();

            base.OnDestroy(); // Unregisters mediator events
        }

        // --- Hover preview ---
        /// <summary>
        /// A non-interactive copy of this card, linked to the same live instance so it shows current stats.
        /// Used for the enlarged preview shown while hovering.
        /// </summary>
        public CardController CreatePreview(Transform parent) {
            var preview = CardFactory.CreateCardController(linkedCardInstance, baseCardData, Player, parent, GameMediator, GameReferences, GameManager);
            if (preview == null) return null;

            var group = preview.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return preview;
        }

        /// <summary>What the card face can't show: the modifiers currently affecting this creature. Empty if none.</summary>
        public string GetActiveModifiersText() {
            if (linkedCreature == null || linkedCreature.Health <= 0 || GameManager?.ModifierManager == null) return "";

            var modifiers = GameManager.ModifierManager.GetActiveModifiersFor(linkedCreature).ToList();
            if (modifiers.Count == 0) return "";

            var lines = modifiers.Select(m => {
                string icon = m is StatusEffectModifier status ? EffectIcons.Tag(EffectIcons.ForStatus(status.EffectType)) : "";
                return $"{icon}{m.Description}";
            });
            return "<b>Active effects</b>\n" + string.Join("\n", lines);
        }

        private void CleanupEvents() {
            OnBeginDragEvent.RemoveAllListeners();
            OnEndDragEvent.RemoveAllListeners();
            OnCardDropped.RemoveAllListeners();
            OnCardClicked.RemoveAllListeners();
            OnPointerEnterHandler = null;
            OnPointerExitHandler = null;
        }
    }
}
