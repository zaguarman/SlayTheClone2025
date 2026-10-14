using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// A player's nameplate, Pokémon style: faction emblem, name, an HP bar that drains green to yellow to red,
    /// the health numbers (the scene's health text, which <see cref="PlayerUI"/> keeps up to date), and how many
    /// cards are left in the deck and the discard pile. It flashes and shakes when the player is hurt. In Grace (God's Grace)
    /// the card lies over it with a countdown to the Dusk it ends: both players see who can't die yet, and when they will.
    /// Built once (in the editor too, see <see cref="BoardSkin"/>); <see cref="Bind"/> connects it to a player.
    /// </summary>
    public class PlayerPlaque : MonoBehaviour {
        private const string ObjectName = "Plaque";
        private static readonly Vector2 Size = new Vector2(340f, 150f);
        private const float BarWidth = 160f;
        private const float EmblemCornerScale = 0.32f; // Corners half the emblem's size: a circle

        [SerializeField] private Image emblem;
        [SerializeField] private TextMeshProUGUI glyphText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI factionText;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private Image barFillImage;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI pilesText;
        [SerializeField] private Image flash;

        private IPlayer player;
        private IGameMediator mediator;
        private IGameManager manager;
        private CardViews cardViews;
        private int shownHealth;

        // Grace: its card over the plaque and the seal counting down the Days to its Dusk
        private const float GraceCardHeight = 168f; // In plaque units: a little taller than the plaque, lying over its emblem
        private static readonly Color SealDark = UITheme.Hex(0x3A0606);
        private static readonly Color SealBright = UITheme.Hex(0xC81E1E);
        private CardController graceCard;
        private Image graceSeal;
        private TextMeshProUGUI graceCount;
        private TextMeshProUGUI graceCaption;
        private (ICard card, int? until, int turn) shownGrace;

        /// <summary>The player it follows.</summary>
        public IPlayer Owner => player;
        /// <summary>The Grace card over the plaque, while the player is in Grace (for tests).</summary>
        public CardController GraceView => graceCard;
        /// <summary>The countdown's lines, number then caption (for tests).</summary>
        public string GraceCountdown => graceSeal != null ? $"{graceCount.text} {graceCaption.text}" : null;

        /// <summary>The plaque under the player's UI, built (and given their health text) if it isn't there yet.</summary>
        public static PlayerPlaque Ensure(Transform parent, TextMeshProUGUI health) {
            var existing = parent.Find(ObjectName);
            if (existing != null && existing.TryGetComponent(out PlayerPlaque found)) return found;

            var background = UITheme.NewImage(ObjectName, parent, UITheme.Panel);
            UITheme.StylePanel(background, trim: UITheme.GoldDim);
            var rect = background.rectTransform;
            rect.sizeDelta = Size;
            if (health != null) {
                rect.localScale = health.transform.localScale;
                rect.localPosition = health.transform.localPosition;
            }

            var plaque = background.gameObject.AddComponent<PlayerPlaque>();
            plaque.Build(health);
            return plaque;
        }

        private void Build(TextMeshProUGUI health) {
            // Emblem: a coin in the faction's color with its glyph
            emblem = UITheme.NewImage("Emblem", transform, UITheme.Gold);
            UITheme.Round(emblem, UITheme.Gold, EmblemCornerScale);
            PlaceTopLeft(emblem.rectTransform, 16f, 25f, 100f, 100f);
            UITheme.AddTrim(emblem.transform, UITheme.Gold, EmblemCornerScale);
            UITheme.AddHardShadow(emblem, 4f);
            glyphText = UITheme.NewText("Glyph", emblem.transform, "", 60f, UITheme.Ink);
            glyphText.fontStyle = FontStyles.Bold;
            UITheme.Stretch(glyphText.rectTransform);

            nameText = UITheme.NewText("Name", transform, "", 26f, UITheme.Parchment, TextAlignmentOptions.Left);
            nameText.fontStyle = FontStyles.Bold;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 16;
            nameText.fontSizeMax = 26;
            nameText.overflowMode = TextOverflowModes.Truncate; // The font has no ellipsis character
            PlaceTopLeft(nameText.rectTransform, 128f, 12f, 200f, 32f);

            factionText = UITheme.NewText("Faction", transform, "", 17f, UITheme.Gold, TextAlignmentOptions.Left);
            factionText.fontStyle = FontStyles.SmallCaps | FontStyles.Bold;
            factionText.characterSpacing = 2f;
            PlaceTopLeft(factionText.rectTransform, 128f, 44f, 200f, 22f);

            var hpLabel = UITheme.NewText("HP", transform, "HP", 20f, UITheme.Gold, TextAlignmentOptions.Left);
            hpLabel.fontStyle = FontStyles.Bold;
            PlaceTopLeft(hpLabel.rectTransform, 128f, 70f, 36f, 24f);

            var bar = UITheme.NewImage("HealthBar", transform, UITheme.Hex(0x060A14));
            UITheme.Round(bar, bar.color, 3f);
            PlaceTopLeft(bar.rectTransform, 166f, 74f, BarWidth, 18f);
            barFillImage = UITheme.NewImage("Fill", bar.transform, UITheme.HealthHigh);
            UITheme.Round(barFillImage, UITheme.HealthHigh, 4f);
            barFill = barFillImage.rectTransform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.offsetMin = new Vector2(2f, 2f);
            barFill.offsetMax = new Vector2(-2f, -2f);
            UITheme.AddTrim(bar.transform, UITheme.GoldDim, 3f);

            healthText = health;
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
                healthText.textWrappingMode = TextWrappingModes.NoWrap;
                healthText.raycastTarget = false;
            }

            pilesText = UITheme.NewText("Piles", transform, "", 16f, UITheme.TextMuted, TextAlignmentOptions.Left);
            PlaceTopLeft(pilesText.rectTransform, 128f, 120f, 200f, 22f);

            flash = UITheme.NewImage("Flash", transform, Color.clear);
            UITheme.Round(flash, Color.clear, 1.5f);
            UITheme.Stretch(flash.rectTransform);
        }

        /// <summary>
        /// Labels the plaque: name, faction and its colors. Outside play it also shows full health and the deck's size,
        /// as a preview of the game's start.
        /// </summary>
        public void Show(string playerName, Faction faction, DeckScriptableObject deck) {
            var factionColor = UITheme.FactionColor(faction);
            emblem.color = factionColor;
            emblem.pixelsPerUnitMultiplier = EmblemCornerScale;
            var emblemTrim = emblem.transform.Find("Trim");
            if (emblemTrim != null) emblemTrim.GetComponent<Image>().pixelsPerUnitMultiplier = EmblemCornerScale;
            glyphText.text = UITheme.FactionGlyph(faction);
            nameText.text = playerName;
            nameText.overflowMode = TextOverflowModes.Truncate; // Also for plaques baked before the font's missing ellipsis was noticed
            factionText.text = faction == Faction.None ? "Unaligned" : CardTextFormatter.FactionName(faction);
            factionText.color = factionColor;

            if (Application.isPlaying) return;
            SetFill(1f);
            barFillImage.color = UITheme.HealthHigh;
            if (healthText != null) healthText.text = $"{Player.MaxHealth}/{Player.MaxHealth}";
            pilesText.text = PilesLine(deck != null ? deck.Cards.Count : 0, 0);
        }

        /// <summary>Follows the player's health and piles from now on.</summary>
        public void Bind(IPlayer owner, IGameMediator gameMediator, IGameManager gameManager = null, CardViews views = null) {
            Unbind();
            player = owner;
            mediator = gameMediator;
            manager = gameManager;
            cardViews = views;
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

        private void Unbind() {
            if (player != null) player.HealthChanged -= OnHealthChanged;
            if (mediator != null) {
                mediator.HandStateChanged -= ShowPiles;
                mediator.BattlefieldStateChanged -= ShowPiles;
                mediator.TurnEnded -= OnTurnEnded;
            }
            player = null;
            mediator = null;
            HideGrace();
        }

        private void OnDestroy() {
            HideGrace();
            DOTween.Kill(transform);
            if (barFill != null) DOTween.Kill(barFill);
            if (flash != null) DOTween.Kill(flash);
            Unbind();
        }

        private void OnTurnEnded(int turn) => ShowPiles(player);

        // Grace starts when its card resolves and ends at a Dusk; the countdown moves with the Days
        private void Update() {
            if (player == null) return;
            var now = (player.GraceCard, player.GraceUntilDay, manager?.TurnManager?.TurnNumber ?? 0);
            if (now == shownGrace) return;
            shownGrace = now;
            if (player.GraceUntilDay == null) HideGrace();
            else ShowGrace(player.GraceUntilDay.Value - now.Item3);
        }

        // The card lies tilted over the plaque's emblem (name and Health stay in sight), the seal on its lower half. Days
        // left: 1 or more, they can't die at this Dusk; 0, this Dusk is the one (still at 0 Health then, they're executed)
        private void ShowGrace(int daysLeft) {
            if (graceCard == null && player.GraceCard != null && cardViews != null) {
                graceCard = cardViews.Create(player.GraceCard, player, transform);
                if (graceCard != null) {
                    graceCard.name = "GraceCard";
                    graceCard.SetAttachment();
                    var rect = (RectTransform)graceCard.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                    rect.localScale = Vector3.one * (GraceCardHeight / Mathf.Max(1f, rect.rect.height));
                    rect.anchoredPosition = new Vector2(64f, 8f);
                    rect.localRotation = Quaternion.Euler(0f, 0f, -8f);
                    rect.SetAsLastSibling();
                    rect.DOPunchScale(Vector3.one * 0.25f * rect.localScale.x, 0.5f, 6).SetTarget(rect);
                }
            }
            if (graceSeal == null) BuildSeal();
            graceSeal.transform.SetAsLastSibling();

            bool last = daysLeft <= 0;
            graceCount.text = Mathf.Max(0, daysLeft).ToString();
            graceCaption.text = last ? "DAYS LEFT" : daysLeft == 1 ? "DAY TO LIVE" : "DAYS TO LIVE";

            // A heartbeat, faster and redder on the last Day
            var seal = graceSeal.rectTransform;
            DOTween.Kill(seal);
            DOTween.Kill(graceSeal);
            seal.localScale = Vector3.one;
            graceSeal.color = SealDark;
            float beat = last ? 0.35f : 0.8f;
            seal.DOScale(last ? 1.18f : 1.08f, beat).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(seal).SetLink(seal.gameObject);
            var image = graceSeal;
            DOTween.To(() => image.color, c => image.color = c, last ? SealBright : UITheme.Hex(0x6A0E0E), beat)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(graceSeal).SetLink(graceSeal.gameObject);
            if (last) seal.DOPunchPosition(new Vector3(0.08f, 0f, 0f), 0.6f, 20, 0.8f).SetTarget(seal);
        }

        private void BuildSeal() {
            graceSeal = UITheme.NewImage("GraceCountdown", transform, SealDark);
            UITheme.Round(graceSeal, SealDark, 0.6f);
            UITheme.AddTrim(graceSeal.transform, SealBright, 0.6f);
            UITheme.AddHardShadow(graceSeal, 4f);
            graceSeal.raycastTarget = false;
            var seal = graceSeal.rectTransform;
            seal.anchorMin = seal.anchorMax = new Vector2(0f, 0.5f);
            seal.pivot = new Vector2(0.5f, 0.5f);
            seal.sizeDelta = new Vector2(124f, 92f);
            seal.anchoredPosition = new Vector2(64f, -58f);

            graceCount = UITheme.NewText("Count", seal, "", 56f, UITheme.Parchment);
            graceCount.fontStyle = FontStyles.Bold;
            graceCount.raycastTarget = false;
            var count = graceCount.rectTransform;
            count.anchorMin = new Vector2(0f, 0.42f);
            count.anchorMax = Vector2.one;
            count.offsetMin = count.offsetMax = Vector2.zero;

            graceCaption = UITheme.NewText("Caption", seal, "", 20f, UITheme.Parchment);
            graceCaption.fontStyle = FontStyles.Bold;
            graceCaption.characterSpacing = 1f;
            graceCaption.enableAutoSizing = true;
            graceCaption.fontSizeMin = 9;
            graceCaption.fontSizeMax = 20;
            graceCaption.raycastTarget = false;
            var caption = graceCaption.rectTransform;
            caption.anchorMin = new Vector2(0.05f, 0.04f);
            caption.anchorMax = new Vector2(0.95f, 0.46f);
            caption.offsetMin = caption.offsetMax = Vector2.zero;
        }

        private void HideGrace() {
            shownGrace = default;
            if (graceCard != null) {
                DOTween.Kill(graceCard.transform);
                Destroy(graceCard.gameObject);
            }
            if (graceSeal != null) {
                DOTween.Kill(graceSeal.rectTransform);
                DOTween.Kill(graceSeal);
                Destroy(graceSeal.gameObject);
            }
            graceCard = null;
            graceSeal = null;
        }

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
            flash.color = UITheme.WithAlpha(color, 0.45f);
            DOTween.To(() => flash.color.a, a => flash.color = UITheme.WithAlpha(color, a), 0f, 0.5f).SetTarget(flash);
            if (shake) {
                DOTween.Kill(transform, complete: true);
                transform.DOPunchPosition(new Vector3(0.12f, 0f, 0f), 0.35f, 14, 0.8f).SetTarget(transform);
            }
        }

        private void ShowPiles(IPlayer changed) {
            if (changed != player || pilesText == null || player.Deck == null) return;
            pilesText.text = PilesLine(player.Deck.CardsRemaining, player.Deck.GetDiscardPilePreview()?.Count ?? 0);
        }

        private static string PilesLine(int deck, int discard) => $"Deck {deck}   ·   Discard {discard}";

        // Positions from the plaque's top-left corner, in plaque units
        private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
