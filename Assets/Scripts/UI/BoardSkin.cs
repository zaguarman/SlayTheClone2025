using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Dresses the game scene as a card table (see <see cref="UITheme"/>): a patterned table whose two halves take
    /// their owners' faction colors, a brass rule between them, each side's mat and slot markers, a plaque per player
    /// with a health bar, and the turn, End Turn, weather and pile controls placed around the edges of whatever screen
    /// shape the camera shows. Only rearranges and decorates what the scene already has; the views keep doing the work.
    /// <para>
    /// Dressing finds what an earlier dressing made instead of making it again, so it is baked into the scene in the
    /// editor (menu UI > Apply Theme To Scenes) to show there without playing, and runs again when the game starts to
    /// recolor the table for the decks actually chosen and to connect the plaques to the players.
    /// </para>
    /// </summary>
    public class BoardSkin : MonoBehaviour {
        private const float ScreenMargin = 0.22f; // World units kept clear at the edges of the screen

        private Camera tableCamera;
        private RectTransform canvasRoot;
        private RectTransform table;
        private RectTransform riverLine;
        private RectTransform player1Side;
        private RectTransform player2Side;
        private RectTransform turnBanner;
        private RectTransform weatherPill;
        private RectTransform endTurnButton;
        private RectTransform deckButton;
        private RectTransform discardButton;
        private RectTransform queueToggle;
        private RectTransform queuePanel;
        private PlayerPlaque player1Plaque;
        private PlayerPlaque player2Plaque;
        private BattlefieldUI player1Battlefield;
        private BattlefieldUI player2Battlefield;
        private HandUI player1Hand;
        private Vector2Int laidOutFor;

        public bool IsApplied { get; private set; }

        /// <summary>When the game starts: dresses the table for the chosen decks and connects the plaques to the players.</summary>
        public void Apply(IGameReferences references, IGameManager manager, IGameMediator mediator) {
            if (IsApplied) return;

            Dress(references, manager.Player1.Name, manager.Player2.Name);
            player1Plaque.Bind(manager.Player1, mediator);
            player2Plaque.Bind(manager.Player2, mediator);
            IsApplied = true;
        }

        /// <summary>
        /// Styles and lays out the scene's table, also in the editor (no game running): the player names are only
        /// labels, and each side takes the faction of the deck <paramref name="references"/> gives it.
        /// </summary>
        public void Dress(IGameReferences references, string player1Name, string player2Name) {
            var p1 = references.Player1References;
            var p2 = references.Player2References;
            player1Battlefield = p1.BattlefieldUI;
            player2Battlefield = p2.BattlefieldUI;
            player1Hand = p1.HandUI;
            var rootCanvas = player1Battlefield.GetComponentInParent<Canvas>().rootCanvas;
            canvasRoot = (RectTransform)rootCanvas.transform;
            tableCamera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
            if (tableCamera != null) tableCamera.backgroundColor = UITheme.TableDark;

            var player1Faction = DeckCatalog.FactionOf(references.Player1Deck);
            var player2Faction = DeckCatalog.FactionOf(references.Player2Deck);

            BuildTable(player1Faction, player2Faction);
            DressSide(p1, player1Faction);
            DressSide(p2, player2Faction);
            player1Plaque = PlayerPlaque.Ensure(p1.PlayerUI.transform, p1.HealthText);
            player1Plaque.Show(player1Name, player1Faction, references.Player1Deck);
            player2Plaque = PlayerPlaque.Ensure(p2.PlayerUI.transform, p2.HealthText);
            player2Plaque.Show(player2Name, player2Faction, references.Player2Deck);
            StyleTurnBanner(FindFirstObjectByType<TurnUI>(FindObjectsInactive.Include));
            StyleEndTurn(references.GetResolveActionsButton());
            StyleWeather(references.GetWeatherText(), references.GetWeatherCycleButton());
            deckButton = StylePileButton(references.GetDeckViewButton(), flip: true);
            discardButton = StylePileButton(references.GetDiscardViewButton(), flip: false);
            StyleDeckView(references.GetDeckViewUI());

            Layout();
        }

        /// <summary>The Queue button goes at the right edge level with your mat; its panel fills the corner above End Turn.</summary>
        public void PlaceQueue(RectTransform toggle, RectTransform panel) {
            queueToggle = toggle;
            queuePanel = panel;
            Layout();
        }

        // The screen can change shape (window resized, another Game view aspect): keep the edges tidy
        private void LateUpdate() {
            if (IsApplied && laidOutFor != new Vector2Int(Screen.width, Screen.height)) Layout();
        }

        // The object under the parent with this name, or a new one made by create
        private static T FindOrCreate<T>(Transform parent, string name, System.Func<T> create) where T : Component {
            var existing = parent.Find(name);
            return existing != null && existing.TryGetComponent(out T found) ? found : create();
        }

        #region Table
        // Like a two-player playmat: each half of the table takes its owner's faction color, with a solid band along
        // its outer edge, and a brass double rule with diamond ends splits the two halves
        private void BuildTable(Faction player1Faction, Faction player2Faction) {
            table = FindOrCreate(canvasRoot, "Table", () => UITheme.AddBackdrop(canvasRoot, "Table"));
            table.SetAsFirstSibling();
            table.localScale = player1Battlefield.transform.localScale;

            player2Side = BuildSide("Player2Side", player2Faction, edgeAtTop: true);
            player1Side = BuildSide("Player1Side", player1Faction, edgeAtTop: false);

            riverLine = FindOrCreate(canvasRoot, "River", () => {
                var river = new GameObject("River", typeof(RectTransform)).GetComponent<RectTransform>();
                river.gameObject.layer = canvasRoot.gameObject.layer;
                river.SetParent(canvasRoot, false);
                river.sizeDelta = new Vector2(1500f, 10f);
                foreach (float y in new[] { 1f, 0f }) {
                    var line = UITheme.NewImage("Rule", river, UITheme.WithAlpha(UITheme.Gold, 0.7f));
                    line.rectTransform.anchorMin = new Vector2(0f, y);
                    line.rectTransform.anchorMax = new Vector2(1f, y);
                    line.rectTransform.sizeDelta = new Vector2(-20f, 2f);
                }
                foreach (float x in new[] { 0f, 1f }) {
                    var diamond = UITheme.NewDiamond("Diamond", river, UITheme.Gold, 14f);
                    diamond.rectTransform.anchorMin = diamond.rectTransform.anchorMax = new Vector2(x, 0.5f);
                    diamond.rectTransform.anchoredPosition = Vector2.zero;
                }
                return river;
            });
            riverLine.SetSiblingIndex(3);
            riverLine.localScale = table.localScale;
        }

        private RectTransform BuildSide(string name, Faction faction, bool edgeAtTop) {
            var side = FindOrCreate(canvasRoot, name, () => {
                var image = UITheme.NewImage(name, canvasRoot, Color.clear);
                var edge = UITheme.NewImage("Edge", image.transform, Color.clear);
                edge.rectTransform.anchorMin = new Vector2(0f, edgeAtTop ? 1f : 0f);
                edge.rectTransform.anchorMax = new Vector2(1f, edgeAtTop ? 1f : 0f);
                edge.rectTransform.pivot = new Vector2(0.5f, edgeAtTop ? 1f : 0f);
                edge.rectTransform.sizeDelta = new Vector2(0f, 8f);
                return image.rectTransform;
            });
            side.SetSiblingIndex(1);
            side.localScale = table.localScale;

            var color = UITheme.FactionColor(faction);
            side.GetComponent<Image>().color = UITheme.WithAlpha(color, 0.025f);
            side.Find("Edge").GetComponent<Image>().color = UITheme.WithAlpha(color, 0.9f);
            return side;
        }

        // The mat: a solid border in the faction's color with a thin brass line inside it, and a marker per slot.
        // The hand area is invisible (it only tints while a card is dragged over it).
        private static void DressSide(GameReferences.PlayerUIReferences side, Faction faction) {
            var battlefield = side.BattlefieldUI;
            battlefield.ApplyMatLook();

            var inner = FindOrCreate(battlefield.transform, "MatInnerLine",
                () => UITheme.AddTrim(battlefield.transform, Color.clear, 2.5f, 7f, "MatInnerLine"));
            inner.color = UITheme.WithAlpha(UITheme.Gold, 0.45f);
            inner.transform.SetSiblingIndex(0);
            var trim = FindOrCreate(battlefield.transform, "MatTrim",
                () => UITheme.AddTrim(battlefield.transform, Color.clear, 1.2f, 0f, "MatTrim"));
            trim.color = UITheme.FactionColor(faction);
            trim.transform.SetSiblingIndex(0);

            var positions = battlefield.SlotPositions(Player.BattlefieldSize);
            for (int i = 0; i < positions.Count; i++) {
                string markName = $"SlotMark_{i}";
                var mark = FindOrCreate(battlefield.transform, markName, () => {
                    var fill = UITheme.NewImage(markName, battlefield.transform, UITheme.SlotEmpty);
                    UITheme.Round(fill, UITheme.SlotEmpty, 1.6f);
                    var brackets = UITheme.AddTrim(fill.transform, BattlefieldSlot.MarkerColor, 1f, -3f, "Brackets");
                    brackets.sprite = UITheme.CornerBrackets;
                    return fill;
                });
                UITheme.Place(mark.rectTransform, new Vector2(0.5f, 0.5f), positions[i], BattlefieldSlot.Size);
                mark.transform.SetSiblingIndex(2 + i); // Above the trims, under the slots made when the game starts
            }

            var hand = side.HandUI.GetComponent<Image>();
            if (hand != null) hand.color = Color.clear;
        }
        #endregion

        #region Controls
        private void StyleTurnBanner(TurnUI turnUI) {
            if (turnUI == null) return;
            var turnText = turnUI.GetComponent<TextMeshProUGUI>() ?? turnUI.GetComponentInChildren<TextMeshProUGUI>();
            if (turnText == null) return;

            turnBanner = FindOrCreate(canvasRoot, "TurnBanner", () => {
                var banner = UITheme.NewImage("TurnBanner", canvasRoot, UITheme.Panel);
                banner.rectTransform.localScale = turnText.transform.lossyScale;
                return banner.rectTransform;
            });
            turnBanner.sizeDelta = new Vector2(240f, 64f);
            UITheme.StylePanel(turnBanner.GetComponent<Image>(), trim: UITheme.Gold);

            if (turnText.transform.parent != turnBanner) {
                turnText.transform.SetParent(turnBanner, false);
                turnText.transform.localScale = Vector3.one;
                turnText.transform.localPosition = Vector3.zero;
            }
            UITheme.Stretch(turnText.rectTransform, 4f);
            turnText.alignment = TextAlignmentOptions.Center;
            turnText.fontSize = 34;
            UITheme.StyleHeading(turnText);
            if (!Application.isPlaying) turnText.text = "Turn 0";
        }

        private void StyleEndTurn(Button button) {
            if (button == null) return;
            endTurnButton = (RectTransform)button.transform;
            endTurnButton.sizeDelta = new Vector2(250f, 86f);
            UITheme.StyleButton(button, UITheme.ButtonKind.Primary);
            UITheme.AddHardShadow(button.image, 7f);

            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) {
                label.text = "End Turn";
                label.fontSize = 34;
                label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                label.characterSpacing = 3f;
            }
        }

        private void StyleWeather(TextMeshProUGUI weatherText, Button cycleButton) {
            if (weatherText == null) return;

            weatherPill = FindOrCreate(canvasRoot, "WeatherPill", () => {
                var pill = UITheme.NewImage("WeatherPill", canvasRoot, UITheme.Panel);
                pill.rectTransform.localScale = weatherText.transform.lossyScale;
                return pill.rectTransform;
            });
            weatherPill.sizeDelta = new Vector2(cycleButton != null ? 520f : 420f, 54f);
            UITheme.StylePanel(weatherPill.GetComponent<Image>(), trim: UITheme.GoldDim);

            if (weatherText.transform.parent != weatherPill) {
                weatherText.transform.SetParent(weatherPill, false);
                weatherText.transform.localScale = Vector3.one;
                weatherText.transform.localPosition = Vector3.zero;
            }
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
            if (buttonRect.parent != weatherPill) {
                buttonRect.SetParent(weatherPill, false);
                buttonRect.localScale = Vector3.one;
            }
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

        // A little stack of card backs beside the label, on the outer side (flip puts it on the right)
        private static RectTransform StylePileButton(Button button, bool flip) {
            if (button == null) return null;
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(220f, 70f);
            UITheme.StyleButton(button);

            var pile = rect.Find("Pile");
            if (pile == null) {
                pile = new GameObject("Pile", typeof(RectTransform)).transform;
                pile.gameObject.layer = button.gameObject.layer;
                pile.SetParent(rect, false);
                for (int i = 0; i < 3; i++) {
                    var back = UITheme.NewImage("CardBack", pile, i == 2 ? UITheme.Hex(0x2E4170) : UITheme.Hex(0x1C2944));
                    UITheme.Round(back, back.color, 4f);
                    UITheme.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 4f, (i - 1) * 4f), new Vector2(30f, 42f));
                    UITheme.AddTrim(back.transform, UITheme.Gold, 4f);
                }
            }
            UITheme.Place((RectTransform)pile, new Vector2(flip ? 1f : 0f, 0.5f), new Vector2(flip ? -34f : 34f, 0f), new Vector2(36f, 48f));

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
            if (!Application.isPlaying) panel.SetActive(false); // Closed until the game opens it, so the table shows
            panel.transform.SetAsLastSibling(); // Over the table's controls when open

            UITheme.StylePanel(panel.GetComponent<Image>(), UITheme.WithAlpha(UITheme.Panel, 1f), UITheme.Gold);
            foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>(true)) {
                if (text.transform.parent == panel.transform) UITheme.StyleHeading(text, text.fontSize >= 30 ? UITheme.Gold : UITheme.Parchment);
            }
            var scroll = panel.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null && scroll.TryGetComponent(out Image scrollImage)) UITheme.Round(scrollImage, UITheme.Hex(0x0A0F1C, 0.7f), 1.5f);
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
            FillBetween(player1Side, center.x, halfWidth, center.y - halfHeight, riverY);
            FillBetween(player2Side, center.x, halfWidth, riverY, center.y + halfHeight);

            PlaceAtLeft(player1Plaque != null ? (RectTransform)player1Plaque.transform : null, left, mat1.center.y);
            PlaceAtLeft(player2Plaque != null ? (RectTransform)player2Plaque.transform : null, left, mat2.center.y);
            PlaceAtLeft(turnBanner, left, riverY);
            if (weatherPill != null) PlaceAt(weatherPill, center.x, riverY);
            PlaceAtRight(endTurnButton, right, riverY);

            float handY = WorldBounds(player1Hand.transform).center.y;
            PlaceAtLeft(discardButton, left, handY);
            PlaceAtRight(deckButton, right, handY);

            if (queueToggle != null && queuePanel != null && endTurnButton != null) {
                PlaceAtRight(queueToggle, right, mat1.center.y);
                float top = center.y + halfHeight - ScreenMargin;
                float bottom = WorldBounds(endTurnButton).yMax + ScreenMargin;
                queuePanel.sizeDelta = new Vector2(queuePanel.sizeDelta.x, (top - bottom) / queuePanel.lossyScale.y);
                PlaceAtRight(queuePanel, right, (top + bottom) / 2f);
            }
        }

        // Stretches a band across the whole view, from bottom to top (world units)
        private static void FillBetween(RectTransform band, float centerX, float halfWidth, float bottom, float top) {
            if (band == null) return;
            float scale = band.lossyScale.x;
            band.sizeDelta = new Vector2(halfWidth * 2f, top - bottom) / scale;
            PlaceAt(band, centerX, (bottom + top) / 2f);
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
    }
}
