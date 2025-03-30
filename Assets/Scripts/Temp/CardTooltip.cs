using UnityEngine;
using TMPro;

/// <summary>
/// Manages a single reusable card tooltip for the game.
/// </summary>
public class CardTooltip : MonoBehaviour {
    // References to tooltip components
    private TextMeshProUGUI tooltipText;
    private RectTransform tooltipRect;
    private CanvasGroup canvasGroup;

    // Tracking the active card
    private CardController activeCard;
    private string currentContent;

    private void Awake() {
        // Try to get references if not already assigned
        if (tooltipText == null)
            tooltipText = GetComponentInChildren<TextMeshProUGUI>();

        if (tooltipRect == null)
            tooltipRect = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // Ensure tooltip starts hidden
        HideTooltip();
    }

    /// <summary>
    /// Sets up references to tooltip components
    /// </summary>
    public void SetupReferences(TextMeshProUGUI text, RectTransform rect, CanvasGroup group) {
        tooltipText = text;
        tooltipRect = rect;
        canvasGroup = group;
    }

    /// <summary>
    /// Shows the tooltip for the specified card
    /// </summary>
    public void ShowTooltip(CardController card) {
        if (card == null || card.GetCardData() == null) return;

        // Store active card
        activeCard = card;

        // Always update tooltip content when showing it
        UpdateTooltipContent(card);

        // Position at mouse with offset
        Vector2 mousePos = Input.mousePosition;
        tooltipRect.position = new Vector2(mousePos.x, mousePos.y + 40);

        // Make sure it's on screen
        AdjustPosition();

        // Show the tooltip
        canvasGroup.alpha = 1;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Updates the tooltip content for the specified card
    /// </summary>
    public void UpdateTooltipContent(CardController card) {
        if (card == null || card.GetCardData() == null) return;

        // Always regenerate content to ensure it's up to date
        var content = GenerateTooltipContent(card);
        currentContent = content;
        tooltipText.text = content;

        // Use a fixed size for all tooltips regardless of content
        tooltipText.ForceMeshUpdate();
        // Set fixed size for all tooltip types
        float width = 350;
        float height = 250; // Increased from 180 to ensure there's enough space for description
        tooltipRect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>
    /// Hides the tooltip
    /// </summary>
    public void HideTooltip() {
        activeCard = null;
        canvasGroup.alpha = 0;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Generates tooltip content for a specific card
    /// </summary>
    private string GenerateTooltipContent(CardController card) {
        var cardData = card.GetCardData();
        if (cardData == null) return "";

        string description = string.IsNullOrEmpty(cardData.description) ? "No description" : cardData.description;
        string cardTypeInfo = "";

        // Generate based on card type
        if (cardData is CreatureData creatureData) {
            var creature = card.GetLinkedCreature();
            if (creature != null) {
                cardTypeInfo = $"<b>{cardData.cardName}</b> ({creature.Attack}/{creature.Health})\n<i>Creature</i>\n\n";
            } else {
                cardTypeInfo = $"<b>{cardData.cardName}</b> ({creatureData.attack}/{creatureData.health})\n<i>Creature</i>\n\n";
            }
        } else if (cardData is SpellData) {
            // Add extra padding to spell cards to make content more consistent with creatures
            cardTypeInfo = $"<b>{cardData.cardName}</b>\n<i>Spell</i>\n\n";
        }

        // Add effects info
        string effectsInfo = "";
        if (cardData.effects != null && cardData.effects.Count > 0) {
            effectsInfo = "\n<b>Effects:</b>\n";
            foreach (var effect in cardData.effects) {
                effectsInfo += $"• {DescribeEffect(effect)}\n";
            }
        }

        return $"{cardTypeInfo}{description}{effectsInfo}";
    }

    private string DescribeEffect(CardEffect effect) {
        string triggerText = effect.trigger.ToString();
        string actionText = "";

        foreach (var action in effect.actions) {
            string target = action.targetType.ToString();
            switch (action.actionType) {
                case Enums.ActionType.Damage:
                    actionText += $"Deal {action.value} damage to {target}";
                    break;
                case Enums.ActionType.Heal:
                    actionText += $"Heal {action.value} to {target}";
                    break;
                case Enums.ActionType.Draw:
                    actionText += $"Draw {action.value} card(s)";
                    break;
                default:
                    actionText += $"{action.actionType} {action.value} to {target}";
                    break;
            }
        }

        return $"{triggerText}: {actionText}";
    }

    /// <summary>
    /// Keeps the tooltip on screen by adjusting position
    /// </summary>
    private void AdjustPosition() {
        if (tooltipRect == null) return;

        // Get dimensions
        Vector3[] corners = new Vector3[4];
        tooltipRect.GetWorldCorners(corners);
        float tooltipWidth = corners[3].x - corners[0].x;
        float tooltipHeight = corners[1].y - corners[0].y;

        // Get current position
        Vector2 pos = tooltipRect.position;

        // Adjust for right edge of screen
        float rightEdgeScreen = Screen.width;
        if (pos.x + tooltipWidth / 2 > rightEdgeScreen) {
            pos.x = rightEdgeScreen - tooltipWidth / 2;
        }

        // Adjust for left edge of screen
        if (pos.x - tooltipWidth / 2 < 0) {
            pos.x = tooltipWidth / 2;
        }

        // Adjust for top edge of screen
        float topEdgeScreen = Screen.height;
        if (pos.y + tooltipHeight > topEdgeScreen) {
            // If tooltip would go off top edge, place it below the cursor instead
            pos.y = Input.mousePosition.y - 20 - tooltipHeight;
        }

        // Ensure the tooltip doesn't go below the bottom edge of the screen
        if (pos.y - tooltipHeight < 0) {
            pos.y = tooltipHeight;
        }

        // Set the adjusted position
        tooltipRect.position = pos;
    }

    /// <summary>
    /// Updates tooltip position when needed in LateUpdate
    /// </summary>
    private void LateUpdate() {
        // Only update position if tooltip is active
        if (gameObject.activeSelf && activeCard != null) {
            Vector2 mousePos = Input.mousePosition;
            tooltipRect.position = new Vector2(mousePos.x, mousePos.y + 40);
            AdjustPosition();
        }
    }

    /// <summary>
    /// Enables tooltip functionality for a card while disabling dragging
    /// </summary>
    public void EnableTooltipOnly(CardController card) {
        if (card == null) return;

        // We no longer need to set up event listeners here
        // as the DeckViewCardInterceptor component will handle all interactions

        // The interceptor directly calls ShowTooltip and HideTooltip methods
        // on this CardTooltip instance

        // Make sure the card can still receive pointer events for tooltips
        var canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup != null) {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
    }
}