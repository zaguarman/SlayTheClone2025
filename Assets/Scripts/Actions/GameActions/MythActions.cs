using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Revive (Myth): a creature that died rises again at the next Dawn, its card taken back from the discard pile: to the
    /// slot where it fell if it's free, else the first free one on its side, with its full Health. It's a new arrival:
    /// Deployment Time again, no statuses. No free slot: it stays dead. Its OnDeath effect, so its Last Will still happens.
    /// </summary>
    public class RevivalAction : IGameAction {
        public ICreature Creature { get; }
        public int SlotIndex { get; } // Where it fell

        public RevivalAction(ICreature creature, int slotIndex) {
            Creature = creature;
            SlotIndex = slotIndex;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"RevivalAction: {Creature?.Name} will rise at the next Dawn";
    }

    public class RevivalActionExecutor : ActionExecutor<RevivalAction> {
        protected override void ExecuteAction(RevivalAction revival, ActionExecutionContext context) {
            var dead = revival.Creature;
            if (dead == null || !dead.IsDead || dead.IsToken) return; // Saved after all, or a token: no card to raise
            var home = dead.DeckOwner ?? dead.Owner;
            if (home?.Deck.TakeFromDiscardPile(dead) is not ICreature risen) {
                Log($"{dead.Name}'s card isn't in the discard pile: it can't rise", LogTag.Effects);
                return;
            }
            risen.SetOwner(home);
            home.GoneDark.Add(new GoneDark(risen, System.Math.Max(0, revival.SlotIndex), context.TurnClock.TurnNumber + 1, reborn: true));
            Log($"{dead.Name} will rise again at the next Dawn", LogTag.Effects | LogTag.Creatures);
        }
    }

    /// <summary>
    /// A sacrifice (Myth's cost, like Magic's "sacrifice a creature"): to play the card, its side's weakest creatures die
    /// when it resolves (the lowest Attack, then the lowest Health), before it does anything. They really die: their Last
    /// Will happens. It can't be played with fewer creatures than that on the board, and if they're gone by the time it
    /// resolves, it fizzles.
    /// </summary>
    public static class Sacrifice {
        public static int Of(ICard card) => card?.SourceData?.sacrifice ?? 0;

        /// <summary>The creatures it would take now, weakest first.</summary>
        public static List<ICreature> Victims(ICard card, IPlayer owner) {
            if (owner == null) return new List<ICreature>();
            return owner.Battlefield.Select(s => s.OccupyingCreature).Where(c => c != null && !c.IsDead && c != card)
                .OrderBy(c => c.Attack).ThenBy(c => c.Health).Take(Of(card)).ToList();
        }

        public static bool CanPay(ICard card, IPlayer owner) => Of(card) <= 0 || Victims(card, owner).Count >= Of(card);

        /// <summary>When the card resolves: the victims die first. False: there aren't enough, so it fizzles.</summary>
        internal static bool Pay(ICard card, IPlayer owner, IActionsQueue queue) {
            int cost = Of(card);
            if (cost <= 0) return true;
            var victims = Victims(card, owner);
            if (victims.Count < cost) {
                Log($"{card.Name} needs {cost} creature(s) to sacrifice; {owner.Name} has {victims.Count}: it fizzles", LogTag.Cards | LogTag.Actions);
                return false;
            }
            foreach (var victim in victims) {
                Log($"{owner.Name} sacrifices {victim.Name} to play {card.Name}", LogTag.Cards | LogTag.Actions);
                queue.AddAction(new DestroyCreatureAction(victim, unconditional: true, controller: owner));
            }
            return true;
        }
    }

    /// <summary>
    /// Prophecy (Myth): a spell foretold now that comes true later. Played, it does nothing yet; at the Dawn so many Days on,
    /// its effects happen, aimed where it was played. Everyone hears it: the queue lists what's been foretold.
    /// </summary>
    public class Prophecy {
        public Spell Spell { get; }
        public IPlayer Caster { get; }
        public ITarget Target { get; }
        public int DueOnTurn { get; } // The Day whose Dawn fulfils it

        public Prophecy(Spell spell, IPlayer caster, ITarget target, int dueOnTurn) {
            Spell = spell;
            Caster = caster;
            Target = target;
            DueOnTurn = dueOnTurn;
        }

        public static int DaysOf(ICard card) => card?.SourceData?.prophecy ?? 0;

        /// <summary>At Dawn: the prophecies due today come true, after the creatures that went dark come back.</summary>
        public static void ProcessDawn(IEnumerable<IPlayer> players, int turn, IActionsQueue queue) {
            foreach (var player in players) {
                foreach (var due in player.Prophecies.Where(p => p.DueOnTurn <= turn).ToList()) {
                    player.Prophecies.Remove(due);
                    Log($"{player.Name}'s prophecy comes true: {due.Spell.Name}", LogTag.Effects | LogTag.Turns);
                    EffectResolver.QueueSpellEffects(due.Spell, due.Caster, due.Target, queue);
                }
            }
        }
    }
}
