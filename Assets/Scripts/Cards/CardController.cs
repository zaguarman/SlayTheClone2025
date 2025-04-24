using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using static DebugLogger;
using static Enums;

[Serializable]
public class CardUnityEvent : UnityEvent<CardController> { }

public class CardController : UIComponent, IPointerEnterHandler, IPointerExitHandler, IDragHandler, IBeginDragHandler, IEndDragHandler {
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image cardImage;

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

    public CardUnityEvent OnBeginDragEvent = new CardUnityEvent();
    public CardUnityEvent OnEndDragEvent = new CardUnityEvent();
    public CardUnityEvent OnCardDropped = new CardUnityEvent();
    public Action OnPointerEnterHandler;
    public Action OnPointerExitHandler;

    public Transform OriginalParent => originalParent;
    public ICreature GetLinkedCreature() => linkedCreature;
    public ICard GetLinkedCardInstance() => linkedCardInstance;
    public CardData GetBaseCardData() => baseCardData; // Expose base data if needed
    public bool IsPlayer1Card() => Player?.IsPlayer1() ?? false;

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
    public void Setup(CardData data, IPlayer owner, ICard cardInstance, IGameMediator mediator, IGameReferences references) {
        // Call base Initialize with dependencies FIRST
        base.Initialize(owner, mediator, references);

        // Now do CardController specific setup
        baseCardData = data;
        linkedCardInstance = cardInstance; // Store the passed instance
        linkedCreature = cardInstance as ICreature; // Try casting to ICreature

        UpdateUI();
    }

    protected override void RegisterEvents() {
        if (gameMediator != null) {
            // Listen for CreatureDamaged event to update Health/Armor display
            gameMediator.AddCreatureDamagedListener(OnCreatureDamaged);
            gameMediator.AddCreatureDiedListener(OnCreatureDied);
            gameMediator.AddCreatureArmorChangedListener(OnCreatureArmorChanged); // Listen for armor changes
            // Also listen for general game state changes which might affect modifiers
            gameMediator.AddGameStateChangedListener(OnGameStateChanged);
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            gameMediator.RemoveCreatureDamagedListener(OnCreatureDamaged);
            gameMediator.RemoveCreatureDiedListener(OnCreatureDied);
            gameMediator.RemoveCreatureArmorChangedListener(OnCreatureArmorChanged);
            gameMediator.RemoveGameStateChangedListener(OnGameStateChanged);
        }
    }

    private void OnGameStateChanged() {
        // If this card is linked to a creature or any card instance, update its UI as modifiers might have changed
        // Also update if it's just linked to an ICard (in case spell state could change, though unlikely)
        if (linkedCardInstance != null) {
            UpdateUI();
        }
    }

    private void OnCreatureDamaged(ICreature creature, int damage) {
        if (linkedCreature != null && creature.TargetId == linkedCreature.TargetId) {
            Log($"Creature {creature.Name} took {damage} damage, updating UI (TargetID: {creature.TargetId.ToUpper()}) (Card TargetID: {GetInstanceID().ToString().ToUpper()})", LogTag.Creatures | LogTag.UI);
            linkedCreature = creature;
            UpdateUI();

            // If tooltip is showing this card, update it
            var tooltip = GetTooltip();
            if (tooltip != null && tooltip.gameObject.activeSelf) {
                tooltip.UpdateTooltipContent(this);
            }
        }
    }

    // NEW: Listener for Armor Pool changes
    private void OnCreatureArmorChanged(ICreature creature, int newArmor) {
        if (linkedCreature != null && creature.TargetId == linkedCreature.TargetId) {
            Log($"Creature {creature.Name} armor changed to {newArmor}, updating UI (TargetID: {creature.TargetId.ToUpper()}) (Card TargetID: {GetInstanceID().ToString().ToUpper()})", LogTag.Creatures | LogTag.UI | LogTag.Effects);
            // No need to reassign linkedCreature, just update UI
            UpdateUI();
        }
    }

    private void OnCreatureDied(ICreature creature) {
        if (linkedCreature != null && creature.TargetId == linkedCreature.TargetId) {
            Log($"Creature {creature.Name} died, updating UI (TargetID: {creature.TargetId.ToUpper()}) (Card TargetID: {GetInstanceID().ToString().ToUpper()})", LogTag.Creatures | LogTag.UI);
            linkedCreature = null;
            linkedCardInstance = null; // Also clear general instance link
            UpdateUI();

            // Hide tooltip if showing
            GetTooltip()?.HideTooltip();
        }
    }

    private Tooltip GetTooltip() {
        // Use gameReferences from base class
        // Check if gameReferences itself is valid before accessing GetTooltip
        if (tooltip == null && gameReferences != null) { // Add null check for gameReferences
             // Check if the GameReferences object hasn't been destroyed
             if ((UnityEngine.Object)gameReferences != null) // Cast to UnityEngine.Object for null check
             {
                tooltip = gameReferences.GetTooltip();
             }
             else {
                 // GameReferences was destroyed, can't get tooltip
                 return null;
             }
        }
        // Check if the tooltip we have cached hasn't been destroyed
        if (tooltip != null && tooltip.gameObject == null) {
            tooltip = null; // Clear the cached reference if destroyed
        }
        return tooltip;
    }

    public override void UpdateUI(IPlayer player = null) {
        // Use baseCardData as the primary source, override with linkedCardInstance if available
        if (baseCardData == null) {
            LogWarning($"CardController {gameObject.name} has no baseCardData! Cannot update UI.", LogTag.UI | LogTag.Cards);
            // Optionally hide or show placeholder visuals
            if(nameText) nameText.text = "No Data";
            if(statsText) statsText.text = "";
            if(descriptionText) descriptionText.text = "";
            return;
        }

        UpdateCardText();
        UpdateCardVisuals();
    }

    private void UpdateCardText() {
        if (nameText == null || statsText == null) { return; } // Basic check

        nameText.text = baseCardData.cardName;

        // Optional on-card description
        if (descriptionText != null) {
            // Show base description, could potentially show modified description later
            string desc = baseCardData.description ?? "";
            // Simple truncate logic
            if (desc.Length > 30) desc = desc.Substring(0, 27) + "...";
            descriptionText.text = desc;
        }

        // --- Determine Stats to Display ---
        bool isCreature = baseCardData is CreatureData;
        bool isSpell = baseCardData is SpellData;

        if (isCreature) {
            statsText.gameObject.SetActive(true);
            string atk, health, speed, healthArmorPart;
            int armor = 0;

            // Prioritize LIVE creature state if linked and alive
            if (linkedCreature != null && linkedCreature.Health > 0) {
                atk = linkedCreature.Attack.ToString();
                health = linkedCreature.Health.ToString(); // Current Health
                speed = linkedCreature.Speed.ToString();
                armor = linkedCreature.CurrentArmorPool; // Current Armor Pool
            }
            // Otherwise, show BASE stats from CardData
            else if (baseCardData is CreatureData creatureData) {
                atk = creatureData.attack.ToString();
                health = creatureData.health.ToString(); // Base Health
                speed = creatureData.speed.ToString();
                // No armor shown for base stats or dead creatures
            }
            else { // Should not happen if isCreature is true
                atk = "?"; health = "?"; speed = "?";
            }

            // Format: A / H(+Arm) / S
            healthArmorPart = health;
            if (armor > 0) {
                healthArmorPart += $"<color=#ADD8E6>+{armor}</color>"; // Light blue for armor
            }
            statsText.text = $"{atk} / {healthArmorPart} / {speed}";
        } else if (isSpell) {
            statsText.gameObject.SetActive(true);
            statsText.text = "Spell";
        } else {
            statsText.gameObject.SetActive(false); // Hide for unknown types
        }
    }

    private void UpdateCardVisuals() {
        // Update color based on owner
        if (cardImage != null && Player != null && gameReferences != null) {
            cardImage.color = Player.IsPlayer1()
                ? gameReferences.GetPlayer1CardColor()
                : gameReferences.GetPlayer2CardColor();
        }
        // Update frame, art etc. based on baseCardData if needed
        // e.g., if (frameImage != null && baseCardData.frameSprite != null) frameImage.sprite = baseCardData.frameSprite;
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (canvasGroup == null) {
            LogWarning("CanvasGroup is missing, adding it now (Card TargetID: " + GetInstanceID().ToString().ToUpper() + ")", LogTag.UI);
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

        gameMediator?.NotifyGameStateChanged();
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

    // Make sure to clean up when the card is destroyed
    protected override void OnDestroy() {
        // Check if gameReferences is still valid *before* trying to use it
        if (gameReferences != null) // Use the inherited field
        {
            var tooltip = GetTooltip(); // GetTooltip also uses gameReferences
            if (tooltip != null) // Check if tooltip is valid too
            {
                // Check if the tooltip GameObject itself hasn't been destroyed
                // This uses Unity's overloaded '==' operator for destroyed objects
                if(tooltip.gameObject != null)
                {
                    tooltip.HideTooltip();
                }
            }
        }

        // Existing cleanup
        if (transform != null) {
            DOTween.Kill(transform);
        }
        CleanupEvents();

        // Call the base OnDestroy last
        base.OnDestroy(); // base.OnDestroy handles UnregisterEvents etc.
    }

    // --- Tooltip Content Generation ---
    public string GetFormattedTooltipText() {
        if (baseCardData == null) return "Error: No Card Data";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{baseCardData.cardName}</b>");

        string statsLine = "";
        int maxHealth = 0;

        if (baseCardData is CreatureData baseCreatureData) {
             string atk, health, speed, armorStr = "";

            // Prioritize LIVE creature state
            if (linkedCreature != null && linkedCreature.Health > 0) {
                atk = linkedCreature.Attack.ToString();
                health = linkedCreature.Health.ToString();
                speed = linkedCreature.Speed.ToString();
                maxHealth = linkedCreature.MaxHealth; // Get live max health
                int armor = linkedCreature.CurrentArmorPool;
                if (armor > 0) armorStr = $" + <color=#ADD8E6>{armor} Armor</color>";

                 // Show comparison to base if different
                 if (linkedCreature.Attack != baseCreatureData.attack) atk = $"{atk} ({baseCreatureData.attack})";
                 if (linkedCreature.MaxHealth != baseCreatureData.health) health = $"{health}/{maxHealth} ({baseCreatureData.health})"; else health = $"{health}/{maxHealth}";
                 if (linkedCreature.Speed != baseCreatureData.speed) speed = $"{speed} ({baseCreatureData.speed})";
            }
            // Show BASE stats
            else {
                atk = baseCreatureData.attack.ToString();
                health = baseCreatureData.health.ToString();
                speed = baseCreatureData.speed.ToString();
                maxHealth = baseCreatureData.health; // Base max health
                health = $"{health}/{maxHealth}"; // Format as current/max
            }
            statsLine = $"<i>Stats:</i> {atk} Atk / {health} HP{armorStr} / {speed} Spd";
        } else if (baseCardData is SpellData) {
            statsLine = "<i>Type:</i> Spell";
        }

        if (!string.IsNullOrEmpty(statsLine)) {
            sb.AppendLine(statsLine);
            sb.AppendLine("---");
        }

        // Description
        if (!string.IsNullOrEmpty(baseCardData.description)) {
            sb.AppendLine(baseCardData.description);
        }

        // Effects (Show effects from LIVE creature if available, else from BASE data)
        List<CardEffect> effectsToShow = null;
        if (linkedCreature != null && linkedCreature.Health > 0) {
            effectsToShow = linkedCreature.Effects;
        } else {
            effectsToShow = baseCardData.effects;
        }

        if (effectsToShow != null && effectsToShow.Count > 0) {
            sb.AppendLine("---");
            sb.AppendLine("<b>Effects:</b>");
            foreach (var effect in effectsToShow) {
                sb.AppendLine($"<i>[{effect.trigger}]</i> {FormatEffectActions(effect.actions)}");
            }
        }

        // Add current modifiers from ModifierManager if linked creature exists
         if (linkedCreature != null && linkedCreature.Health > 0 && gameManager?.ModifierManager != null) {
             var activeModifiers = gameManager.ModifierManager.GetActiveModifiersFor(linkedCreature as Creature); // Need concrete type
             if (activeModifiers.Any()) {
                 sb.AppendLine("---");
                 sb.AppendLine("<b>Active Modifiers:</b>");
                 foreach(var mod in activeModifiers) {
                     sb.AppendLine($"- {mod.Description}"); // Use modifier's description
                 }
             }
         }

        return sb.ToString();
    }

    private string FormatEffectActions(List<EffectAction> actions) {
        if (actions == null || actions.Count == 0) return "No actions.";

        List<string> actionDescriptions = new List<string>();
        foreach(var action in actions) {
            string targetDesc = action.targetType.ToString();
            if (action.targetModifier != TargetModifier.None) {
                targetDesc += $" ({action.targetModifier})";
            }
            string desc = $"Target {targetDesc}: ";

            switch (action.actionType) {
                case ActionType.Damage: desc += $"Deal {action.value} damage."; break;
                case ActionType.Heal: desc += $"Heal {action.value}."; break;
                case ActionType.Draw: desc += $"Draw {action.value} card(s)."; break;
                case ActionType.Armor: desc += $"Modify Armor by {action.value}."; break;
                case ActionType.Stun: desc += $"Stun for {action.value} turn(s)."; break;
                case ActionType.ModifyStat:
                    List<string> mods = new List<string>();
                    if (action.modifyAttack) mods.Add("Attack");
                    if (action.modifyHealth) mods.Add("Health");
                    if (action.modifySpeed) mods.Add("Speed");
                    desc += $"Modify {string.Join(", ", mods)} by {action.value}.";
                    break;
                case ActionType.ApplyStatus:
                     desc += $"Apply {action.statusEffectToApply} (Dur: {action.statusDuration}, Pot: {action.statusPotency}).";
                     break;
                case ActionType.Summon: desc += $"Summon {action.value} creature(s)."; break;
                default: desc += $"Unknown action ({action.actionType})"; break;
            }
            actionDescriptions.Add(desc);
        }
        return string.Join(" ", actionDescriptions); // Join multiple actions for the same trigger
    }

    private void CleanupEvents() {
        OnBeginDragEvent.RemoveAllListeners();
        OnEndDragEvent.RemoveAllListeners();
        OnCardDropped.RemoveAllListeners();
        OnPointerEnterHandler = null;
        OnPointerExitHandler = null;
    }
}