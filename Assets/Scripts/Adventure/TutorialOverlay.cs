using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zeitgeist {
    /// <summary>
    /// The tutor of the first fight (see <see cref="Tutorial"/>): a note at the top of the screen that says what to do next and
    /// moves on when it's done. It watches the game rather than scripting it: a creature of yours queued, the Day ended, an
    /// attack planned, the Day ended again, a spell played. The last note waits for the fight to end. Got it closes a note early.
    /// </summary>
    public class TutorialOverlay : MonoBehaviour {
        private const int OverlayOrder = UITheme.WindowsLayer + 400;
        public const string GotItButtonName = "Tutorial Got it";

        private struct Step {
            public string Text;
            public System.Func<TutorialOverlay, bool> Done; // Null: waits for Got it
        }

        private static readonly Step[] Steps = {
            new Step { Text = "<b>Welcome.</b> The cards at the bottom are your hand; the five slots in front of you are your side of the table. " +
                              "Drag a creature from your hand onto one of your empty slots. A creature in the middle front slot (the Vanguard) takes the melee; the two beside it shoot.",
                       Done = t => t.MyQueued<ICreature>() },
            new Step { Text = "Nothing happens until the Day ends: you and the enemy plan, then everything resolves in order. Press <b>End Turn</b> on the right and watch.",
                       Done = t => t.DayPassed() },
            new Step { Text = "Your creature is ready. Drag it onto the enemy creature in front of it to attack, or onto an empty enemy slot to hit the player. " +
                              "An attack only reaches the slots its position allows: the game shows which.",
                       Done = t => t.MyAttackQueued() },
            new Step { Text = "The <b>Queue</b> at the right lists every action planned this Day, yours and the enemy's, in the order they'll resolve. " +
                              "Hover one to read it. End the turn when you're ready.",
                       Done = t => t.DayPassed() },
            new Step { Text = "Spells: drag one onto a creature, a slot or a player. Hover any word in gold on a card and it explains itself. " +
                              "Right-click a planned card to take it back.",
                       Done = t => t.MyQueued<Spell>() || t.DayPassed() },
            new Step { Text = "That's the whole loop: plan, end the Day, read what happened. The enemy's plans resolve with yours, and some cards wait for a later Day. " +
                              "Win this fight to finish the tutorial.", Done = null },
        };

        private int step;
        private int dayAtStep;
        private GameManager game;
        private TextMeshProUGUI text;
        private Button gotIt;

        private void Start() {
            game = FindFirstObjectByType<GameManager>();
            BuildOverlay();
            dayAtStep = Day;
            ShowStep();
        }

        private int Day => game?.TurnManager?.TurnNumber ?? 0;
        private bool DayPassed() => Day > dayAtStep;
        private bool MyQueued<T>() where T : class => game != null && game.ActionsQueue.GetPendingActions().OfType<PlayCardAction>()
            .Any(p => p.GetOwner() == game.Player1 && p.GetCard() is T);
        private bool MyAttackQueued() => game != null && game.ActionsQueue.GetPendingActions().OfType<BattlefieldCombatAction>()
            .Any(a => a.GetAttacker()?.Owner == game.Player1);

        private void Update() {
            if (game == null || game.Player1 == null) return;
            if (game.Player1.Health <= 0 || game.Player2.Health <= 0) { // The fight is over: the adventure takes it from here
                Tutorial.FightEnded(game.Player2.Health <= 0 && game.Player1.Health > 0);
                Destroy(gameObject);
                return;
            }
            if (step < Steps.Length && Steps[step].Done != null && Steps[step].Done(this)) Next();
        }

        /// <summary>On to the next note (Got it, or the step's condition met).</summary>
        public void Next() {
            step = Mathf.Min(step + 1, Steps.Length - 1);
            dayAtStep = Day;
            ShowStep();
        }

        private void ShowStep() {
            if (text == null) return;
            text.text = $"<size=70%><color=#B8B0A0>Tutorial {step + 1} of {Steps.Length}</color></size>\n{Steps[step].Text}";
            if (gotIt != null) gotIt.GetComponentInChildren<TextMeshProUGUI>().text = step == Steps.Length - 1 ? "Close" : "Got it";
        }

        private void BuildOverlay() {
            var canvas = new GameObject("Tutorial Panel", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlayOrder;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            var panel = UITheme.NewImage("Panel", canvas.transform, UITheme.Panel, raycastTarget: true);
            UITheme.StylePanel(panel, trim: UITheme.Gold);
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f);
            rect.sizeDelta = new Vector2(760f, 0f);
            var row = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 12, 10, 10);
            row.spacing = 12f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            text = UITheme.NewText("Text", panel.transform, "", 20f, UITheme.Parchment);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            var textElement = text.gameObject.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1;
            textElement.preferredWidth = 600f;

            gotIt = MenuWidgets.AddButton(panel.transform, GotItButtonName, "Got it", 110f, () => { if (step == Steps.Length - 1) Destroy(gameObject); else Next(); });
            gotIt.GetComponent<LayoutElement>().preferredHeight = 44f;
        }
    }
}
