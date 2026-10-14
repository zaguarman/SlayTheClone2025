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
        public int Days { get; } // It rises at the Dawn this many Days on (1: the next Dawn)

        public RevivalAction(ICreature creature, int slotIndex, int days = 1) {
            Creature = creature;
            SlotIndex = slotIndex;
            Days = System.Math.Max(1, days);
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"RevivalAction: {Creature?.Name} will rise {(Days == 1 ? "at the next Dawn" : $"at the Dawn {Days} Days on")}";
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
            home.GoneDark.Add(new GoneDark(risen, System.Math.Max(0, revival.SlotIndex), context.TurnClock.TurnNumber + revival.Days, reborn: true));
            Log($"{dead.Name} will rise again in {revival.Days} Day(s)", LogTag.Effects | LogTag.Creatures);
        }
    }

    /// <summary>
    /// Resurrection (Phoenix Feather): the creature is sacrificed now, for real (its Last Will happens, a Blessing doesn't
    /// stop it), and rises again at the Dawn so many Days on, like a Revive: its card back from the discard pile, full
    /// Health, Deployment Time again. Something that keeps it from dying (Borrowed Time) keeps it from rising too.
    /// </summary>
    public class ResurrectionAction : IGameAction {
        public ICreature Creature { get; }
        public IPlayer Controller { get; }
        public int Days { get; }

        public ResurrectionAction(ICreature creature, IPlayer controller, int days) {
            Creature = creature;
            Controller = controller;
            Days = days;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"ResurrectionAction: {Creature?.Name} dies, to rise in {Days} Days";
    }

    public class ResurrectionActionExecutor : ActionExecutor<ResurrectionAction> {
        protected override void ExecuteAction(ResurrectionAction resurrection, ActionExecutionContext context) {
            var creature = resurrection.Creature;
            if (creature == null || creature.IsDead) return;
            int slot = creature.Slot?.Index ?? 0;
            Log($"{creature.Name} is sacrificed, to rise again in {resurrection.Days} Days", LogTag.Effects | LogTag.Creatures);
            context.ActionsQueue.AddAction(new DestroyCreatureAction(creature, unconditional: true, controller: resurrection.Controller, sacrifice: true));
            context.ActionsQueue.AddAction(new RevivalAction(creature, slot, resurrection.Days));
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
                queue.AddAction(new DestroyCreatureAction(victim, unconditional: true, controller: owner, sacrifice: true));
            }
            return true;
        }
    }

    /// <summary>
    /// Doom N, a cost (Doomscrolling): to play the card, its side's weakest creature that isn't Doomed yet (the lowest Attack,
    /// then the lowest Health) is Doomed N Days when it resolves, before it does anything. It can't be played without one, and
    /// if it's gone by the time it resolves, it fizzles. A Blessed creature stops the Doom with a stack, like any status.
    /// </summary>
    public static class DoomCost {
        public static int Of(ICard card) => card?.SourceData?.doomCost ?? 0;

        /// <summary>The creature it would doom now; null when it costs nothing or nobody can pay.</summary>
        public static ICreature Condemned(ICard card, IPlayer owner, IModifierManager modifiers) {
            if (Of(card) <= 0 || owner == null) return null;
            return owner.Battlefield.Select(s => s.OccupyingCreature)
                .Where(c => c != null && !c.IsDead && c != card && !(modifiers?.HasStatusEffect(c, StatusEffectType.Doomed) ?? false))
                .OrderBy(c => c.Attack).ThenBy(c => c.Health).FirstOrDefault();
        }

        public static bool CanPay(ICard card, IPlayer owner, IModifierManager modifiers) => Of(card) <= 0 || Condemned(card, owner, modifiers) != null;

        /// <summary>When the card resolves: the Doom lands first. False: nobody can take it, so it fizzles.</summary>
        internal static bool Pay(ICard card, IPlayer owner, IActionsQueue queue) {
            if (Of(card) <= 0) return true;
            var condemned = Condemned(card, owner, queue.Modifiers);
            if (condemned == null) {
                Log($"{card.Name} needs a creature to doom; {owner.Name} has none: it fizzles", LogTag.Cards | LogTag.Actions);
                return false;
            }
            Log($"{owner.Name} dooms {condemned.Name} to play {card.Name}", LogTag.Cards | LogTag.Actions);
            queue.AddAction(new ApplyStatusEffectAction(condemned, StatusEffectType.Doomed, Of(card), 0, controller: owner));
            return true;
        }
    }

    /// <summary>
    /// God's Grace (Myth): the player's Die Another Day. Until the Dusk of the next Day their Health can hit 0 and they don't
    /// lose; at that Dusk, still at 0, they do. Healed above 0 in time, they live. If both players fall to 0, the one in
    /// Grace wins: only the other one loses when they hit 0.
    /// </summary>
    public class GraceAction : IGameAction {
        public IPlayer Player { get; }
        public int Days { get; }

        public GraceAction(IPlayer player, int days) {
            Player = player;
            Days = System.Math.Max(1, days);
            Log($"Created {this}", LogTag.Actions | LogTag.Players);
        }

        public override string ToString() => $"GraceAction: {Player?.Name} is in Grace for {Days} Day(s)";
    }

    public class GraceActionExecutor : ActionExecutor<GraceAction> {
        protected override void ExecuteAction(GraceAction grace, ActionExecutionContext context) {
            if (grace.Player == null) return;
            grace.Player.GraceUntilDay = context.TurnClock.TurnNumber + grace.Days;
            Log($"{grace.Player.Name} is in Grace until the Dusk of Day {grace.Player.GraceUntilDay}", LogTag.Players | LogTag.Effects);
        }
    }

    public static class Grace {
        /// <summary>Out of the game: at 0 Health and not held up by Grace.</summary>
        public static bool IsDown(IPlayer player) => player != null && player.Health <= 0 && player.GraceUntilDay == null;

        /// <summary>At Dusk, after what Dusk does: a Grace whose last Day this was ends, and a player still at 0 loses.</summary>
        public static void ProcessDusk(IEnumerable<IPlayer> players, int endedTurn, IGameMediator mediator) {
            foreach (var player in players) {
                if (player.GraceUntilDay == null || player.GraceUntilDay > endedTurn) continue;
                player.GraceUntilDay = null;
                Log($"{player.Name}'s Grace ends at {player.Health} Health", LogTag.Players);
                if (player.Health <= 0 && !mediator.IsGameOver) mediator.NotifyGameOver(player.Opponent);
            }
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
