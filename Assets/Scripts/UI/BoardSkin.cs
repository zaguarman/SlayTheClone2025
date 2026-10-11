using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Dresses the game scene as a card table (see <see cref="UITheme"/>): felt under everything, a line between the two
    /// sides, each side's mat trimmed in its faction's color, a plaque per player with a health bar, and the turn,
    /// End Turn, weather and pile controls placed around the edges of whatever screen shape the camera shows.
    /// Only rearranges and decorates what the scene already has; the views keep doing the work.
    /// </summary>
    public class BoardSkin : MonoBehaviour {
        private const float ScreenMargin = 0.22f; // World units kept clear at the edges of the screen

        private Camera tableCamera;
        private RectTransform canvasRoot;
        private RectTransform table;
        private RectTransform riverLine;
        private RectTransform turnBanner;
        private RectTransform weatherPill;
        private RectTransform endTurnButton;
        private RectTransform endTurnGlow;
        private RectTransform deckButton;
        private RectTransform discardButton;
        private PlayerPlaque player1Plaque;
        private PlayerPlaque player2Plaque;
        private BattlefieldUI player1Battlefield;
        private BattlefieldUI player2Battlefield;
        private HandUI player1Hand;
        private Vector2Int laidOutFor;

        public bool IsApplied { get; private set; }

        public void Apply(IGameReferences references, IGameManager manager, IGameMediator mediator) {
            if (IsApplied) return;

            var p1 = references.Player1References;
            var p2 = references.Player2References;
            player1Battlefield = p1.BattlefieldUI;
            player2Battlefield = p2.BattlefieldUI;
            player1Hand = p1.HandUI;
            canvasRoot = (RectTransform)player1Battlefield.GetComponentInParent<Canvas>().rootCanvas.transform;
            tableCamera = player1Battlefield.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            if (tableCamera == null) tableCamera = Camera.main;
            if (tableCamera != null) tableCamera.backgroundColor = UITheme.TableDark;

            var player1Faction = DeckCatalog.FactionOf(references.Player1Deck);
            var player2Faction = DeckCatalog.FactionOf(references.Player2Deck);

            BuildTable();
            TrimMat(player1Battlefield, player1Faction);
            TrimMat(player2Battlefield, player2Faction);
            player1Plaque = PlayerPlaque.Create(p1.PlayerUI.transform, manager.Player1, player1Faction, p1.HealthText, mediator);
            player2Plaque = PlayerPlaque.Create(p2.PlayerUI.transform, manager.Player2, player2Faction, p2.HealthText, mediator);
            StyleTurnBanner(FindObjectOfType<TurnUI>());
            StyleEndTurn(references.GetResolveActionsButton());
            StyleWeather(references.GetWeatherText(), references.GetWeatherCycleButton());
            deckButton = StylePileButton(references.GetDeckViewButton(), flip: false);
            discardButton = StylePileButton(references.GetDiscardViewButton(), flip: true);
            StyleDeckView(references.GetDeckViewUI());

            IsApplied = true;
            Layout();
        }

        // The screen can change shape (window resized, another Game view aspect): keep the edges tidy
        private void LateUpdate() {
            if (IsApplied && laidOutFor != new Vector2Int(Screen.width, Screen.height)) Layout();
        }

        #region Table
        private void BuildTable() {
            var tableImage = UITheme.NewImage("Table", canvasRoot, UITheme.TableFelt);
            table = tableImage.rectTransform;
            table.SetAsFirstSibling(); // Under everything
            table.localScale = player1Battlefield.transform.localScale;

            var weave = UITheme.NewImage("Weave", table, Color.white);
            weave.sprite = UITheme.Felt;
            weave.type = Image.Type.Tiled;
            UITheme.Stretch(weave.rectTransform);

            var lamp = UITheme.NewImage("Lamp", table, UITheme.Hex(0xFFD98A, 0.07f));
            lamp.sprite = UITheme.SoftCircle;
            lamp.rectTransform.anchorMin = new Vector2(0.1f, 0f);
            lamp.rectTransform.anchorMax = new Vector2(0.9f, 1f);
            lamp.rectTransform.offsetMin = lamp.rectTransform.offsetMax = Vector2.zero;

            var vignette = UITheme.NewImage("Vignette", table, new Color(0f, 0f, 0f, 0.85f));
            vignette.sprite = UITheme.Vignette;
            UITheme.Stretch(vignette.rectTransform);

            // The line between the two sides of the table, fading at both ends
            var river = UITheme.NewImage("River", canvasRoot, UITheme.Hex(0xE8B84A, 0.45f));
            riverLine = river.rectTransform;
            riverLine.SetSiblingIndex(1);
            riverLine.localScale = table.localScale;
            riverLine.sizeDelta = new Vector2(1500f, 2f);
            foreach (float side in new[] { -1f, 1f }) {
                var fade = UITheme.NewImage(side < 0 ? "FadeLeft" : "FadeRight", riverLine, UITheme.TableFelt);
                fade.sprite = UITheme.SoftCircle;
                UITheme.Place(fade.rectTransform, new Vector2(side < 0 ? 0f : 1f, 0.5f), Vector2.zero, new Vector2(500f, 60f));
            }
        }

        private static void TrimMat(BattlefieldUI battlefield, Faction faction) {
            var color = UITheme.FactionColor(faction);
            var trim = UITheme.AddTrim(battlefield.transform, new Color(color.r, color.g, color.b, 0.85f), 1f, 0f, "MatTrim");
            trim.transform.SetAsFirstSibling(); // Under the slots and their cards
            var glow = UITheme.AddTrim(battlefield.transform, new Color(color.r, color.g, color.b, 0.25f), 0.6f, -6f, "MatGlow");
            glow.transform.SetAsFirstSibling();
        }
        #endregion

        #region Controls
        private void StyleTurnBanner(TurnUI turnUI) {
            if (turnUI == null) return;
            var turnText = turnUI.GetComponent<TextMeshProUGUI>() ?? turnUI.GetComponentInChildren<TextMeshProUGUI>();
            if (turnText == null) return;

            var banner = UITheme.NewImage("TurnBanner", canvasRoot, UITheme.Panel);
            turnBanner = banner.rectTransform;
            turnBanner.localScale = turnText.transform.lossyScale;
            turnBanner.sizeDelta = new Vector2(240f, 64f);
            UITheme.StylePanel(banner, trim: UITheme.Gold);

            turnText.transform.SetParent(turnBanner, false);
            turnText.transform.localScale = Vector3.one;
            turnText.transform.localPosition = Vector3.zero;
            UITheme.Stretch(turnText.rectTransform, 4f);
            turnText.alignment = TextAlignmentOptions.Center;
            turnText.fontSize = 34;
            UITheme.StyleHeading(turnText);
        }

        private void StyleEndTurn(Button button) {
            if (button == null) return;
            endTurnButton = (RectTransform)button.transform;
            endTurnButton.sizeDelta = new Vector2(250f, 86f);
            UITheme.StyleButton(button, UITheme.ButtonKind.Primary);

            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) {
                label.fontSize = 34;
                label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                label.characterSpacing = 3f;
            }

            // A slow pulse of light behind it, so the next step is always obvious
            var glow = UITheme.NewImage("EndTurnGlow", endTurnButton.parent, UITheme.Hex(0xFFC860, 0.45f));
            glow.sprite = UITheme.SoftCircle;
            endTurnGlow = glow.rectTransform;
            endTurnGlow.localScale = endTurnButton.localScale;
            endTurnGlow.sizeDelta = new Vector2(420f, 200f);
            endTurnGlow.SetSiblingIndex(endTurnButton.GetSiblingIndex());
            DOTween.To(() => glow.color.a, a => glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, a), 0.15f, 1.4f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(glow);
        }

        private void StyleWeather(TextMeshProUGUI weatherText, Button cycleButton) {
            if (weatherText == null) return;

            var pill = UITheme.NewImage("WeatherPill", canvasRoot, UITheme.Panel);
            weatherPill = pill.rectTransform;
            weatherPill.localScale = weatherText.transform.lossyScale;
            weatherPill.sizeDelta = new Vector2(cycleButton != null ? 520f : 420f, 54f);
            UITheme.StylePanel(pill, trim: UITheme.GoldDim);

            weatherText.transform.SetParent(weatherPill, false);
            weatherText.transform.localScale = Vector3.one;
            weatherText.transform.localPosition = Vector3.zero;
            weatherText.rectTransform.anchorMin = Vector2.zero;
            weatherText.rectTransform.anchorMax = Vector2.one;
            weatherText.rectTransform.offsetMin = new Vector2(18f, 2f);
            weatherText.rectTransform.offsetMax = new Vector2(cycleButton != null ? -110f : -18f, -2f);
            weatherText.alignment = TextAlignmentOptions.Center;
            weatherText.enableAutoSizing = true;
            weatherText.fontSizeMin = 14;
            weatherText.fontSizeMax = 24;
            weatherText.color = UITheme.Parchment;
            weatherText.fontStyle = FontStyles.Italic;

            if (cycleButton == null) return;
            var buttonRect = (RectTransform)cycleButton.transform;
            buttonRect.SetParent(weatherPill, false);
            buttonRect.localScale = Vector3.one;
            UITheme.Place(buttonRect, new Vector2(1f, 0.5f), new Vector2(-54f, 0f), new Vector2(96f, 40f));
            buttonRect.localPosition = new Vector3(buttonRect.localPosition.x, buttonRect.localPosition.y, 0f);
            UITheme.StyleButton(cycleButton);
            var label = cycleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) {
                label.text = "Change";
                label.fontSize = 18;
                label.enableAutoSizing = false;
            }
        }

        // A little stack of card backs beside the label, like the draw and discard piles of Slay the Spire
        private static RectTransform StylePileButton(Button button, bool flip) {
            if (button == null) return null;
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(220f, 70f);
            UITheme.StyleButton(button);

            var pile = new GameObject("Pile", typeof(RectTransform));
            pile.layer = button.gameObject.layer;
            pile.transform.SetParent(rect, false);
            UITheme.Place((RectTransform)pile.transform, new Vector2(flip ? 1f : 0f, 0.5f), new Vector2(flip ? -34f : 34f, 0f), new Vector2(36f, 48f));
            for (int i = 0; i < 3; i++) {
                var back = UITheme.NewImage("CardBack", pile.transform, i == 2 ? UITheme.Hex(0x5A3A6E) : UITheme.Hex(0x3A2648));
                UITheme.Round(back, back.color, 4f);
                UITheme.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 4f, (i - 1) * 4f), new Vector2(30f, 42f));
                UITheme.AddTrim(back.transform, UITheme.Gold, 4f);
            }

            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) {
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(flip ? 10f : 62f, 4f);
                label.rectTransform.offsetMax = new Vector2(flip ? -62f : -10f, -4f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 14;
                label.fontSizeMax = 24;
                label.transform.SetAsLastSibling();
            }
            return rect;
        }

        private static void StyleDeckView(DeckViewUI deckView) {
            var panel = deckView != null ? deckView.DeckViewPanel : null;
            if (panel == null) return;

            UITheme.StylePanel(panel.GetComponent<Image>(), trim: UITheme.Gold);
            foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>(true)) {
                if (text.transform.parent == panel.transform) UITheme.StyleHeading(text, text.fontSize >= 30 ? UITheme.Gold : UITheme.Parchment);
            }
            var scroll = panel.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null && scroll.TryGetComponent(out Image scrollImage)) UITheme.Round(scrollImage, UITheme.Hex(0x0E0A13, 0.7f), 1.5f);
            if (deckView.CloseButton != null) {
                UITheme.Round(deckView.CloseButton.image, UITheme.Hex(0x9A3A34), 3f);
            }
        }
        #endregion

        #region Layout
        // Puts the table under the whole view and the controls at the edges of the screen. Everything is placed in
        // world space, since the board lives on a world-space canvas seen by an orthographic camera.
        private void Layout() {
            laidOutFor = new Vector2Int(Screen.width, Screen.height);
            if (tableCamera == null) return;

            float halfHeight = tableCamera.orthographicSize;
            float halfWidth = halfHeight * tableCamera.aspect;
            Vector3 center = tableCamera.transform.position;
            float left = center.x - halfWidth + ScreenMargin;
            float right = center.x + halfWidth - ScreenMargin;

            float scale = table.lossyScale.x;
            table.sizeDelta = new Vector2(halfWidth * 2.2f, halfHeight * 2.2f) / scale;
            PlaceAt(table, center.x, center.y);

            var mat1 = WorldBounds(player1Battlefield.transform);
            var mat2 = WorldBounds(player2Battlefield.transform);
            float riverY = (mat1.yMax + mat2.yMin) / 2f;
            PlaceAt(riverLine, center.x, riverY);
            riverLine.sizeDelta = new Vector2((right - left) / riverLine.lossyScale.x, riverLine.sizeDelta.y);

            PlaceAtLeft(player1Plaque != null ? (RectTransform)player1Plaque.transform : null, left, mat1.center.y);
            PlaceAtLeft(player2Plaque != null ? (RectTransform)player2Plaque.transform : null, left, mat2.center.y);
            PlaceAtLeft(turnBanner, left, riverY);
            if (weatherPill != null) PlaceAt(weatherPill, center.x, riverY);
            PlaceAtRight(endTurnButton, right, riverY);
            if (endTurnGlow != null && endTurnButton != null) PlaceAt(endTurnGlow, endTurnButton.position.x, endTurnButton.position.y);

            float handY = WorldBounds(player1Hand.transform).center.y;
            PlaceAtLeft(deckButton, left, handY);
            PlaceAtRight(discardButton, right, handY);
        }

        private static Rect WorldBounds(Transform transform) {
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void PlaceAt(RectTransform rect, float x, float y) {
            if (rect == null) return;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.position = new Vector3(x, y, rect.position.z);
        }

        private static void PlaceAtLeft(RectTransform rect, float left, float y) {
            if (rect == null) return;
            PlaceAt(rect, left + rect.rect.width * rect.lossyScale.x / 2f, y);
        }

        private static void PlaceAtRight(RectTransform rect, float right, float y) {
            if (rect == null) return;
            PlaceAt(rect, right - rect.rect.width * rect.lossyScale.x / 2f, y);
        }
        #endregion

        private void OnDestroy() {
            if (endTurnGlow != null) DOTween.Kill(endTurnGlow.GetComponent<Image>());
        }
    }
}
