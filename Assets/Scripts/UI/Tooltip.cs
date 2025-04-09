using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages a single reusable card tooltip for the game.
/// </summary>
public class Tooltip : MonoBehaviour {
    /// <summary>
    /// Creates and initializes a new tooltip instance
    /// </summary>
    public static Tooltip Create(Transform parent) {
        // Create tooltip GameObject
        GameObject tooltipObj = new GameObject("Tooltip");
        tooltipObj.transform.SetParent(parent, false);

        // Add Canvas component for UI rendering
        Canvas tooltipCanvas = tooltipObj.AddComponent<Canvas>();
        tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tooltipCanvas.sortingOrder = 10000; // Make sure it's always on top

        // Add CanvasScaler for consistent UI sizing
        CanvasScaler scaler = tooltipObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Add CanvasGroup for fade control
        CanvasGroup canvasGroup = tooltipObj.AddComponent<CanvasGroup>();

        // Create background panel
        GameObject bgPanel = new GameObject("Background");
        bgPanel.transform.SetParent(tooltipObj.transform, false);

        // Add Image component to background
        Image background = bgPanel.AddComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        // Configure the RectTransform for proper sizing and positioning
        RectTransform bgRect = background.rectTransform;
        bgRect.anchorMin = new Vector2(0.5f, 0);
        bgRect.anchorMax = new Vector2(0.5f, 0);
        bgRect.pivot = new Vector2(0.5f, 0);
        bgRect.sizeDelta = new Vector2(300, 150);

        // Add text for tooltip content
        GameObject textObj = new GameObject("TooltipText");
        textObj.transform.SetParent(bgPanel.transform, false);

        // Add TextMeshProUGUI component
        TextMeshProUGUI tooltipText = textObj.AddComponent<TextMeshProUGUI>();
        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltipText.fontSize = 16;
        tooltipText.color = Color.white;
        tooltipText.enableWordWrapping = true;

        // Configure text RectTransform
        RectTransform textRect = tooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);

        // Add shadow to make text more readable
        Shadow shadow = bgPanel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(2, -2);

        // Add Tooltip component
        Tooltip tooltip = tooltipObj.AddComponent<Tooltip>();

        // Set references
        tooltip.SetupReferences(tooltipText, bgRect, canvasGroup);

        // Hide initially
        tooltipObj.SetActive(false);

        return tooltip;
    }

    // References to tooltip components
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
    /// Shows a text tooltip with the specified content
    /// </summary>
    public void ShowTooltip(string content) {
        if (string.IsNullOrEmpty(content)) return;

        // Clear active card
        activeCard = null;
        currentContent = content;
        tooltipText.text = content;

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
        float height = 280; // Increased from 250 to ensure there's enough space for target ID
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
        // Truncate card ID to show only first 4 and last 4 characters
        string truncatedCardId = TruncateId(cardData.cardId);
        string targetIdInfo = $"<color=#888888>ID: {truncatedCardId}</color>";

        // Generate based on card type
        if (cardData is CreatureData creatureData) {
            var creature = card.GetLinkedCreature();
            if (creature != null) {
                cardTypeInfo = $"<b>{cardData.cardName}</b> ({creature.Attack}/{creature.Health})\n<i>Creature</i>\n\n";
                // If we have a creature, add its target ID too (truncated)
                string truncatedTargetId = TruncateId(creature.TargetId);
                targetIdInfo = $"<color=#888888>Card ID: {truncatedCardId}\nTarget ID: {truncatedTargetId}</color>";
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

        return $"{cardTypeInfo}{description}{effectsInfo}\n\n{targetIdInfo}";
    }

    /// <summary>
    /// Truncates an ID to show only first 4 and last 4 characters with ... in between, all in uppercase
    /// </summary>
    private string TruncateId(string id) {
        if (string.IsNullOrEmpty(id) || id.Length <= 8) {
            return id.ToUpper();
        }
        return $"{id.Substring(0, 4).ToUpper()}...{id.Substring(id.Length - 4).ToUpper()}";
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
        var canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup != null) {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
    }
}