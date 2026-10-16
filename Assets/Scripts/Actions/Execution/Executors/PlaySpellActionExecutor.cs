using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class PlaySpellActionExecutor : ActionExecutor<PlaySpellAction> {
        protected override void ExecuteAction(PlaySpellAction spellAction, ActionExecutionContext context) {
            var spell = spellAction.GetSpell();
            var owner = spellAction.GetOwner();

            if (spell == null || owner == null) {
                LogError("PlaySpellActionExecutor: Spell or owner is null", LogTag.Actions);
                return;
            }

            var actionsQueue = context.ActionsQueue;
            if (actionsQueue == null) {
                LogError($"PlaySpellActionExecutor: ActionsQueue is null in context. Cannot play spell {spell.Name}.", LogTag.Actions | LogTag.Cards);
                return;
            }

            Combo.Charge(spell, owner, actionsQueue); // Combo: 1 more for each other spell of its player's today
            owner.SpellsToday++;
            // The spell queues its own game actions; a Prophecy waits for its Dawn
            int foretold = Prophecy.DaysOf(spell);
            if (foretold > 0) {
                owner.Prophecies.Add(Prophecy.Foretell(spell, owner, spellAction.GetTarget(), context.TurnClock.TurnNumber));
                Log($"{owner.Name} foretells {spell.Name}: it comes true in {foretold} Day(s)", LogTag.Actions | LogTag.Cards);
            }
            else spell.Play(owner, actionsQueue, spellAction.GetTarget());
            if (Policy.Has(spell)) Policy.Enact(spell, owner); // In force: it happens again each Dawn
            else if (spell.SourceData?.singleUse == true) owner.SpendForGood(spell); // One bullet: gone for the rest of the combat
            else owner.DiscardCard(spell);

            Log($"Executed PlaySpellAction via Executor for {spell.Name}", LogTag.Actions | LogTag.Cards);
        }
    }
}
