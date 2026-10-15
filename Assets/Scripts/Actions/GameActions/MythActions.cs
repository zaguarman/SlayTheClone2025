using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Revive (Myth): a creature that died rises again at the next Dawn, its card taken back from the discard pile: to the
    /// slot where it fell if it's free, else the first free one on its side, with its full Health. It rises ready (no
    /// Deployment Time) with the stat changes it had, but not its statuses; its Arrival happens again (the user, 2026-10-15).
    /// No free slot: it stays dead. Its OnDeath effect, so its Last Will still happens.
    /// </summary>
    public class RevivalAction : IGameAction {
        public ICreature Creature { get; }
        public int SlotIndex { get; } // Where it fell
        public int Days { get; } // It rises at the Dawn this many Days on (1: the next Dawn)
        public bool Ready { get; } // Rises ready, no Deployment Time (Phoenix Feather)
        public (int attack, int health, int speed) Kept { get; } // Stat changes it had, back on it when it rises

        public RevivalAction(ICreature creature, int slotIndex, int days = 1, bool ready = false, (int attack, int health, int speed) kept = default) {
            Creature = creature;
            SlotIndex = slotIndex;
            Days = System.Math.Max(1, days);
            Ready = ready;
            Kept = kept;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        /// <summary>The stat changes a creature has in play (over its card's stats), to give back when it rises.</summary>
        public static (int attack, int health, int speed) StatChangesOn(ICreature creature) =>
            (creature.Attack - creature.BaseAttack, creature.MaxHealth - creature.BaseHealth, creature.Speed - creature.BaseSpeed);

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
            home.GoneDark.Add(new GoneDark(risen, System.Math.Max(0, revival.SlotIndex), context.TurnClock.TurnNumber + revival.Days, reborn: true,
                ready: revival.Ready, kept: revival.Kept));
            context.GameMediator.NotifyBattlefieldStateChanged(home); // Its slot shows it'll rise
            Log($"{dead.Name} will rise again in {revival.Days} Day(s)", LogTag.Effects | LogTag.Creatures);
        }
    }

    /// <summary>
    /// Resurrection (Phoenix Feather): the creature is sacrificed now, for real (its Last Will happens, a Blessing doesn't
    /// stop it), and rises again at the Dawn so many Days on: its card back from the discard pile into its slot (or the first
    /// free one), ready at once with full Health and the stat changes it had; its statuses are gone, as it died. Its Arrival
    /// happens again. Something that keeps it from dying (Borrowed Time) keeps it from rising too.
    /// Myth's loophole: if the creature was already given up this Day (a Sacrifice took it first, Necronomicon's), that
    /// offering counts, and it rises all the same.
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
            if (creature == null) return;
            int day = context.TurnClock.TurnNumber;
            var offering = Offering.Of(creature, day);
            if (creature.IsDead) {
                if (offering == null) return; // Dead some other way: nothing to raise
                Log($"{creature.Name} was already offered this Day: it rises again in {resurrection.Days} Days all the same", LogTag.Effects | LogTag.Creatures);
                context.ActionsQueue.AddAction(new RevivalAction(creature, offering.SlotIndex, resurrection.Days, ready: true, kept: offering.Kept));
                return;
            }
            offering ??= Offering.Make(creature, day);
            Log($"{creature.Name} is sacrificed, to rise again in {resurrection.Days} Days", LogTag.Effects | LogTag.Creatures);
            context.ActionsQueue.AddAction(new DestroyCreatureAction(creature, unconditional: true, controller: resurrection.Controller, sacrifice: true));
            context.ActionsQueue.AddAction(new RevivalAction(creature, offering.SlotIndex, resurrection.Days, ready: true, kept: offering.Kept));
        }
    }

    /// <summary>
    /// A creature its player gave up on a Day (a Sacrifice, Phoenix Feather), with where it stood and the stat changes it
    /// had. Myth bends the rules: one offering pays every Sacrifice of its Day.
    /// </summary>
    public class Offering {
        public ICreature Creature { get; }
        public int Day { get; }
        public int SlotIndex { get; }
        public (int attack, int health, int speed) Kept { get; }

        private Offering(ICreature creature, int day) {
            Creature = creature;
            Day = day;
            SlotIndex = creature.Slot?.Index ?? 0;
            Kept = RevivalAction.StatChangesOn(creature);
        }

        /// <summary>The creature's offering of this Day, if it was given up.</summary>
        public static Offering Of(ICreature creature, int day) =>
            (creature?.DeckOwner ?? creature?.Owner)?.Offerings.FirstOrDefault(o => o.Creature == creature && o.Day == day);

        /// <summary>Records the creature as given up today, while it still stands (its slot, its stat changes); older Days are forgotten.</summary>
        public static Offering Make(ICreature creature, int day) {
            var side = creature.DeckOwner ?? creature.Owner;
            side.Offerings.RemoveAll(o => o.Day != day);
            var offering = new Offering(creature, day);
            side.Offerings.Add(offering);
            return offering;
        }
    }

    /// <summary>
    /// A sacrifice (Myth's cost, like Magic's "sacrifice a creature"): to play the card, its side's weakest creatures die
    /// when it resolves (the lowest Attack, then the lowest Health), before it does anything. They really die: their Last
    /// Will happens. It can't be played with fewer creatures than that on the board, and if they're gone by the time it
    /// resolves, it fizzles.
    /// Myth's loophole (the user, 2026-10-15: bending the rules with God's blessing is central to every religion): one
    /// offering pays every Sacrifice of its Day. A creature sacrificed this Day, or one a Phoenix Feather is taking this Day,
    /// pays first, in place of the weakest: Necronomicon and Phoenix Feather on the same creature cost one death.
    /// </summary>
    public static class Sacrifice {
        public static int Of(ICard card) => card?.SourceData?.sacrifice ?? 0;

        /// <summary>The creatures it would take now, weakest first.</summary>
        public static List<ICreature> Victims(ICard card, IPlayer owner) {
            if (owner == null) return new List<ICreature>();
            return owner.Battlefield.Select(s => s.OccupyingCreature).Where(c => c != null && !c.IsDead && c != card)
                .OrderBy(c => c.Attack).ThenBy(c => c.Health).Take(Of(card)).ToList();
        }

        /// <summary>
        /// Whether it can be played: enough creatures on the board, counting those queued to be summoned this Day (summons
        /// resolve before spells, so they're there when it's paid).
        /// </summary>
        public static bool CanPay(ICard card, IPlayer owner, IActionsQueue queue = null) =>
            Of(card) <= 0 || Victims(card, owner).Count + Incoming(owner, queue) >= Of(card);

        /// <summary>The creatures this side has queued to be summoned this Day.</summary>
        public static int Incoming(IPlayer owner, IActionsQueue queue) => queue?.GetPendingActions().Count(a =>
            (a is PlayCardAction play && play.GetOwner() == owner && play.GetCard() is ICreature)
            || (a is SummonCreatureAction summon && (summon.GetTargetSlot() as BoardSlot)?.Owner == owner)) ?? 0;

        /// <summary>When the card resolves: the victims die first. False: there aren't enough, so it fizzles.</summary>
        internal static bool Pay(ICard card, IPlayer owner, IActionsQueue queue, int day) {
            int cost = Of(card);
            if (cost <= 0) return true;
            // This Day's offerings pay first: the ones already given, then the ones a Phoenix Feather is about to take
            var offered = owner.Offerings.Where(o => o.Day == day).Select(o => o.Creature)
                .Concat(Promised(owner, queue)).Distinct().Take(cost).ToList();
            var victims = offered.Concat(Victims(card, owner).Where(v => !offered.Contains(v))).Take(cost).ToList();
            if (victims.Count < cost) {
                Log($"{card.Name} needs {cost} creature(s) to sacrifice; {owner.Name} has {victims.Count}: it fizzles", LogTag.Cards | LogTag.Actions);
                return false;
            }
            foreach (var victim in victims) {
                if (Offering.Of(victim, day) != null) {
                    Log($"{victim.Name}, already offered this Day, pays for {card.Name} too", LogTag.Cards | LogTag.Actions);
                    continue;
                }
                Log($"{owner.Name} sacrifices {victim.Name} to play {card.Name}", LogTag.Cards | LogTag.Actions);
                Offering.Make(victim, day);
                queue.AddAction(new DestroyCreatureAction(victim, unconditional: true, controller: owner, sacrifice: true));
            }
            return true;
        }

        // The creatures of this side a Phoenix Feather played this Day will take (it hasn't resolved yet)
        private static IEnumerable<ICreature> Promised(IPlayer owner, IActionsQueue queue) {
            if (queue == null) yield break;
            foreach (var action in queue.GetPendingActions()) {
                var (spell, caster, target) = action switch {
                    PlayCardAction play => (play.GetCard() as Spell, play.GetOwner(), play.GetTarget()),
                    PlaySpellAction cast => (cast.GetSpell(), cast.GetOwner(), cast.GetTarget()),
                    _ => (null, null, null)
                };
                if (spell == null || caster != owner || !spell.Effects.SelectMany(e => e.actions).Any(a => a.actionType == ActionType.Resurrect)) continue;
                var creature = target switch {
                    HomingTarget homing => homing.CreatureInPlay,
                    BoardSlot slot => slot.OccupyingCreature,
                    ICreature c => c,
                    _ => null
                };
                if (creature != null && !creature.IsDead && creature.Owner == owner) yield return creature;
            }
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

        public static bool CanPay(ICard card, IPlayer owner, IModifierManager modifiers, IActionsQueue queue = null) =>
            Of(card) <= 0 || Condemned(card, owner, modifiers) != null || Sacrifice.Incoming(owner, queue) > 0;

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
    public class GraceAction : IGameAction, ISourcedAction {
        public ICard Source { get; set; }
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
            if (grace.Player.GraceUntilDay != null) { // It doesn't stack: the Dusk already set is the one
                Log($"{grace.Player.Name} is already in Grace until the Dusk of Day {grace.Player.GraceUntilDay}: no longer", LogTag.Players | LogTag.Effects);
                return;
            }
            grace.Player.GraceUntilDay = context.TurnClock.TurnNumber + grace.Days;
            grace.Player.GraceCard = grace.Source;
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
                player.GraceCard = null;
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
