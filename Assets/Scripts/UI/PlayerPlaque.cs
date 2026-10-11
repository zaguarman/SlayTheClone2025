using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// A player's nameplate, Pokémon style: faction emblem, name, an HP bar that drains green to yellow to red,
    /// the health numbers (the scene's health text, which <see cref="PlayerUI"/> keeps up to date), and how many
    /// cards are left in the deck and the discard pile. It flashes and shakes when the player is hurt.
    /// </summary>
    public class PlayerPlaque : MonoBehaviour {
        private static readonly Vector2 Size = new Vector2(340f, 150f);
        private const float BarWidth = 160f;

        private IPlayer player;
        private IGameMediator mediator;
        private RectTransform barFill;
        private Image barFillImage;
        private Image flash;
        private TextMeshProUGUI pilesText;
        private int shownHealth;

        /// <summary>Builds the plaque under the player's UI and moves their health text into it.</summary>
        public static PlayerPlaque Create(Transform parent, IPlayer player, Faction faction, TextMeshProUGUI healthText, IGameMediator mediator) {
            var background = UITheme.NewImage($"Plaque {player.Name}", parent, UITheme.Panel);
            UITheme.StylePanel(background, trim: UITheme.GoldDim);
            var rect = background.rectTransform;
            rect.sizeDelta = Size;
            if (healthText != null) {
                rect.localScale = healthText.transform.localScale;
                rect.localPosition = healthText.transform.localPosition;
            }

            var plaque = background.gameObject.AddComponent<PlayerPlaque>();
            plaque.Build(player, faction, healthText);
            plaque.Bind(player, mediator);
            return plaque;
        }

        private void Build(IPlayer owner, Faction faction, TextMeshProUGUI healthText) {
            var factionColor = UITheme.FactionColor(faction);

            // Emblem: a coin in the faction's color with its glyph
            var emblem = UITheme.NewImage("Emblem", transform, factionColor);
            UITheme.Round(emblem, factionColor, 0.5f);
            PlaceTopLeft(emblem.rectTransform, 16f, 25f, 100f, 100f);
            UITheme.AddTrim(emblem.transform, UITheme.Gold, 0.5f);
            var glyph = UITheme.NewText("Glyph", emblem.transform, UITheme.FactionGlyph(faction), 60f, UITheme.Ink);
            glyph.fontStyle = FontStyles.Bold;
            UITheme.Stretch(glyph.rectTransform);

            var nameText = UITheme.NewText("Name", transform, owner.Name, 26f, UITheme.Parchment, TextAlignmentOptions.Left);
            nameText.fontStyle = FontStyles.Bold;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 16;
            nameText.fontSizeMax = 26;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            PlaceTopLeft(nameText.rectTransform, 128f, 12f, 200f, 32f);

            var factionText = UITheme.NewText("Faction", transform, faction == Faction.None ? "Unaligned" : CardTextFormatter.FactionName(faction), 17f, factionColor, TextAlignmentOptions.Left);
            factionText.fontStyle = FontStyles.SmallCaps | FontStyles.Bold;
            factionText.characterSpacing = 2f;
            PlaceTopLeft(factionText.rectTransform, 128f, 44f, 200f, 22f);

            var hpLabel = UITheme.NewText("HP", transform, "HP", 20f, UITheme.Gold, TextAlignmentOptions.Left);
            hpLabel.fontStyle = FontStyles.Bold;
            PlaceTopLeft(hpLabel.rectTransform, 128f, 70f, 36f, 24f);

            var bar = UITheme.NewImage("HealthBar", transform, UITheme.Hex(0x0A070D));
            UITheme.Round(bar, bar.color, 3f);
            PlaceTopLeft(bar.rectTransform, 166f, 74f, BarWidth, 18f);
            barFillImage = UITheme.NewImage("Fill", bar.transform, UITheme.HealthHigh);
            UITheme.Round(barFillImage, UITheme.HealthHigh, 4f);
            barFill = barFillImage.rectTransform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.offsetMin = new Vector2(2f, 2f);
            barFill.offsetMax = new Vector2(-2f, -2f);
            UITheme.AddShine(barFill, 0.35f).rectTransform.offsetMin = Vector2.zero;
            UITheme.AddTrim(bar.transform, UITheme.GoldDim, 3f);

            if (healthText != null) {
                healthText.transform.SetParent(transform, false);
                healthText.transform.localScale = Vector3.one;
                healthText.transform.localRotation = Quaternion.identity;
                PlaceTopLeft(healthText.rectTransform, 166f, 94f, BarWidth, 26f);
                healthText.transform.localPosition = new Vector3(healthText.transform.localPosition.x, healthText.transform.localPosition.y, 0f);
                healthText.alignment = TextAlignmentOptions.Right;
                healthText.fontSize = 22;
                healthText.fontStyle = FontStyles.Bold;
                healthText.color = UITheme.Parchment;
                healthText.enableWordWrapping = false;
                healthText.raycastTarget = false;
            }

            pilesText = UITheme.NewText("Piles", transform, "", 16f, UITheme.TextMuted, TextAlignmentOptions.Left);
            PlaceTopLeft(pilesText.rectTransform, 128f, 120f, 200f, 22f);

            flash = UITheme.NewImage("Flash", transform, Color.clear);
            UITheme.Round(flash, Color.clear);
            UITheme.Stretch(flash.rectTransform);
        }

        private void Bind(IPlayer owner, IGameMediator gameMediator) {
            player = owner;
            mediator = gameMediator;
            shownHealth = player.Health;
            ShowHealth(player.Health, animate: false);
            ShowPiles(player);

            player.HealthChanged += OnHealthChanged;
            if (mediator != null) {
                mediator.HandStateChanged += ShowPiles;
                mediator.BattlefieldStateChanged += ShowPiles;
                mediator.TurnEnded += OnTurnEnded;
            }
        }

        private void OnDestroy() {
            DOTween.Kill(transform);
            if (barFill != null) DOTween.Kill(barFill);
            if (flash != null) DOTween.Kill(flash);
            if (player != null) player.HealthChanged -= OnHealthChanged;
            if (mediator != null) {
                mediator.HandStateChanged -= ShowPiles;
                mediator.BattlefieldStateChanged -= ShowPiles;
                mediator.TurnEnded -= OnTurnEnded;
            }
        }

        private void OnTurnEnded(int turn) => ShowPiles(player);

        private void OnHealthChanged(int health) {
            if (health < shownHealth) Flash(UITheme.HealthLow, shake: true);
            else if (health > shownHealth) Flash(UITheme.HealthHigh, shake: false);
            shownHealth = health;
            ShowHealth(health, animate: true);
        }

        private void ShowHealth(int health, bool animate) {
            float fraction = Mathf.Clamp01(health / (float)Player.MaxHealth);
            var color = UITheme.HealthColor(fraction);
            DOTween.Kill(barFill);
            if (!animate) {
                SetFill(fraction);
                barFillImage.color = color;
                return;
            }

            DOTween.To(() => barFill.anchorMax.x, SetFill, fraction, 0.45f).SetEase(Ease.OutCubic).SetTarget(barFill);
            DOTween.To(() => barFillImage.color, c => barFillImage.color = c, color, 0.45f).SetTarget(barFill);
        }

        private void SetFill(float fraction) {
            barFill.anchorMax = new Vector2(fraction, 1f);
            barFillImage.enabled = fraction > 0f;
        }

        private void Flash(Color color, bool shake) {
            DOTween.Kill(flash);
            flash.color = new Color(color.r, color.g, color.b, 0.45f);
            DOTween.To(() => flash.color.a, a => flash.color = new Color(color.r, color.g, color.b, a), 0f, 0.5f).SetTarget(flash);
            if (shake) {
                DOTween.Kill(transform, complete: true);
                transform.DOPunchPosition(new Vector3(0.12f, 0f, 0f), 0.35f, 14, 0.8f).SetTarget(transform);
            }
        }

        private void ShowPiles(IPlayer changed) {
            if (changed != player || pilesText == null || player.Deck == null) return;
            int discarded = player.Deck.GetDiscardPilePreview()?.Count ?? 0;
            pilesText.text = $"Deck {player.Deck.CardsRemaining}   ·   Discard {discarded}";
        }

        // Positions from the plaque's top-left corner, in plaque units
        private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
