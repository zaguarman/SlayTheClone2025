using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Reveal (GDD 2.5 Scan): a one-time look at hidden information, for the player who cast it. Aimed at a slot it shows the
    /// slot's hidden Slot Effect and what the creature there plans to do this Day; aimed at a player, their Watchdog conditions.
    /// Information is instant: a card that only reveals resolves as soon as it's played, during Planning.
    /// </summary>
    public class RevealAction : IGameAction {
        public BoardSlot Slot { get; }   // What's in this slot...
        public IPlayer Side { get; }     // ...or this player's Watchdogs
        public IPlayer Viewer { get; }   // Who gets to see it

        public RevealAction(BoardSlot slot, IPlayer viewer) {
            Slot = slot;
            Viewer = viewer;
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
                if (creature != null) {
                    foreach (var planned in context.ActionsQueue.GetPendingActions().Where(a => Intel.PlannerOf(a) == creature)) {
                        context.ActionsQueue.RevealTo(reveal.Viewer, planned);
                    }
                }
                context.GameMediator.NotifyBattlefieldStateChanged(reveal.Slot.Owner);
            }
            Log($"Revealed to {reveal.Viewer.Name}: {reveal}", LogTag.Effects);
            context.GameMediator.NotifyActionsQueueChanged();
        }
    }
}
