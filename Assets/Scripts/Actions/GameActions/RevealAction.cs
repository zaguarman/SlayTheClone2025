using System.Linq;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>
    /// Reveal (GDD 2.5 Scan): a one-time look at hidden information, for the player who cast it. Aimed at a slot it shows the
    /// slot's hidden Slot Effect, the creature there (what it is, for good, and what it plans this Day) and what is being
    /// summoned there; aimed at a player, their Watchdog conditions.
    /// Information is instant: a card that only reveals resolves as soon as it's played, during Planning.
    /// </summary>
    public class RevealAction : IGameAction {
        public BoardSlot Slot { get; }   // What's in this slot...
        public IPlayer Side { get; }     // ...or this player's Watchdogs
        public IPlayer Viewer { get; }   // Who gets to see it
        public ICreature Source { get; } // The creature whose effect it is, if any (Control Room)

        public RevealAction(BoardSlot slot, IPlayer viewer, ICreature source = null) {
            Slot = slot;
            Viewer = viewer;
            Source = source;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public RevealAction(IPlayer side, IPlayer viewer) {
            Side = side;
            Viewer = viewer;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"RevealAction: {(Slot != null ? Slot.ToString() : $"{Side?.Name}'s Watchdogs")} to {Viewer?.Name}";
    }

    public class RevealActionExecutor : ActionExecutor<RevealAction> {
        protected override void ExecuteAction(RevealAction reveal, ActionExecutionContext context) {
            if (reveal.Viewer == null) return;
            if (reveal.Side != null) {
                foreach (var watchdog in context.ActionsQueue.Watchdogs.Where(w => w.Controller == reveal.Side)) {
                    watchdog.RevealedTo.Add(reveal.Viewer);
                }
            }
            if (reveal.Slot != null) {
                reveal.Slot.Effect?.RevealedTo.Add(reveal.Viewer);
                var creature = reveal.Slot.OccupyingCreature;
                Intel.MakeKnown(creature, reveal.Viewer);
                if (creature != null && creature.Owner != reveal.Viewer) Intel.OnRevealed(creature, reveal.Source, context.ActionsQueue);
                foreach (var pending in context.ActionsQueue.GetPendingActions()
                             .Where(a => (creature != null && Intel.PlannerOf(a) == creature) || Intel.SummonInto(a) == reveal.Slot)) {
                    context.ActionsQueue.RevealTo(reveal.Viewer, pending);
                }
                context.GameMediator.NotifyBattlefieldStateChanged(reveal.Slot.Owner);
            }
            Log($"Revealed to {reveal.Viewer.Name}: {reveal}", LogTag.Effects);
            context.GameMediator.NotifyActionsQueueChanged();
        }
    }
}
