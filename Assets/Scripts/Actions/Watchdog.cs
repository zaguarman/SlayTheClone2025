using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// An Intelligence asset that watches the action queue (GDD 3.11 Watchdogs). It isn't a creature and never attacks: when
    /// an action matches its condition it nullifies, redirects, reorders or reveals it, using a charge.
    /// </summary>
    public class Watchdog {
        public WatchdogSpec Spec { get; }
        public IPlayer Controller { get; }
        public ICreature WatchedCreature { get; } // "attacks by Creature C"; null: a whole side
        public int TurnCreated { get; }
        public int Days { get; }                  // 0: until its charges are used
        public int Sequence { get; }              // The oldest triggers when several match
        public int ChargesLeft { get; private set; }
        public int BriefedOn { get; private set; } = -1; // The last Day it briefed

        /// <summary>Players who saw its condition through Intelligence (a Reveal), besides its controller.</summary>
        public System.Collections.Generic.HashSet<IPlayer> RevealedTo { get; } = new System.Collections.Generic.HashSet<IPlayer>();

        public Watchdog(WatchdogSpec spec, IPlayer controller, ICreature watchedCreature, int turnCreated, int days, int sequence) {
            Spec = spec;
            Controller = controller;
            WatchedCreature = watchedCreature;
            TurnCreated = turnCreated;
            Days = days;
            Sequence = sequence;
            ChargesLeft = System.Math.Max(1, spec.charges);
        }

        public bool IsSpent => ChargesLeft <= 0;
        public bool HasExpired(int turn) => Days > 0 && turn >= TurnCreated + Days;

        /// <summary>
        /// A Briefing Watchdog briefs once a Day, from the Day it's set (the user, 2026-10-17: online at once, like a counter;
        /// it used to wait for the next Day).
        /// </summary>
        public bool CanBrief(int turn) => Spec.timing == WatchdogTiming.Briefing && BriefedOn < turn;

        public void Briefed(int turn) => BriefedOn = turn;

        public void SpendCharge() => ChargesLeft--;

        /// <summary>Whether it watches this action: its kind, whose it is, from which creature, and how strong.</summary>
        public bool Matches(IGameAction action, ICreature source) {
            if (Spec.watches == WatchedAction.Intelligence) {
                if (!WatchdogRules.Reveals(action)) return false;
            }
            else if (WatchdogRules.KindOf(action, source) != Spec.watches) return false;

            var owner = WatchdogRules.OwnerOf(action, source);
            if (Spec.side == WatchdogSide.Enemy && (owner == null || owner == Controller)) return false;
            if (Spec.side == WatchdogSide.Friendly && owner != Controller) return false;

            var actor = WatchdogRules.ActorOf(action, source);
            if (WatchedCreature != null && actor != WatchedCreature) return false;

            if (Spec.atMost > 0 && WatchdogRules.StrengthOf(action) > Spec.atMost) return false;
            if (Spec.damaging && !WatchdogRules.Damages(action)) return false;
            return true;
        }

        /// <summary>
        /// What a player sees of it: its controller everything; the other side only what it watches, with the condition
        /// as "???" (GDD 3.11 Visibility) until Intelligence reveals it.
        /// </summary>
        public string Describe(bool forController) {
            string watched = WatchedCreature != null ? WatchedCreature.Name : Spec.side switch {
                WatchdogSide.Enemy => forController ? "the enemy" : "your side",
                WatchdogSide.Friendly => forController ? "your side" : "their side",
                _ => "both sides"
            };
            if (!forController) return $"Watching {watched}: ???";
            string what = Spec.watches.ToString().ToLower() + (Spec.watches == WatchedAction.Ability ? "" : "s");
            string atMost = Spec.atMost > 0 ? $" of {Spec.atMost} or less" : "";
            if (Spec.damaging) what = "damaging " + what;
            return $"{what}{atMost} by {watched} -> {WatchdogRules.NameOf(Spec.effect)} ({ChargesLeft} left{(Spec.timing == WatchdogTiming.Briefing ? ", Briefing" : "")})";
        }

        public override string ToString() =>
            $"Watchdog ({Controller?.Name}): {Spec.side} {Spec.watches}{(Spec.atMost > 0 ? $" {Spec.atMost} or less" : "")}" +
            $"{(WatchedCreature != null ? $" by {WatchedCreature.Name}" : "")} -> {Spec.effect}, {ChargesLeft} charge(s) ({Spec.timing})";
    }

    /// <summary>How Watchdogs read and change actions (GDD 3.11).</summary>
    public static class WatchdogRules {
        /// <summary>The kind of action a Watchdog condition names; null for actions none watches (draws, deaths...).</summary>
        public static WatchedAction? KindOf(IGameAction action, ICreature source) => action switch {
            BattlefieldCombatAction => WatchedAction.Attack,
            PlayCardAction play => play.GetCard() is ICreature ? WatchedAction.Summon : WatchedAction.Spell,
            PlaySpellAction => WatchedAction.Spell,
            SummonCreatureAction => WatchedAction.Summon,
            MoveCreatureAction => WatchedAction.Move,
            ApplyStatusEffectAction => WatchedAction.Status,
            _ => source != null ? WatchedAction.Ability : (WatchedAction?)null
        };

        /// <summary>The creature acting: the attacker, mover, the creature summoned, or the one whose ability it is.</summary>
        public static ICreature ActorOf(IGameAction action, ICreature source) => action switch {
            BattlefieldCombatAction attack => attack.GetAttacker(),
            MoveCreatureAction move => move.GetCreature(),
            PlayCardAction play => play.GetCard() as ICreature,
            SummonCreatureAction summon => summon.GetCreature(),
            ApplyStatusEffectAction status => status.Source ?? source,
            _ => source
        };

        /// <summary>Whose action it is.</summary>
        public static IPlayer OwnerOf(IGameAction action, ICreature source) => action switch {
            PlayCardAction play => play.GetOwner(),
            PlaySpellAction play => play.GetOwner(),
            SummonCreatureAction summon => summon.GetOwner(),
            MoveCreatureAction move => move.GetPlayer(),
            ApplyStatusEffectAction status => status.Controller ?? ActorOf(action, source)?.Owner,
            RevealAction reveal => reveal.Viewer,
            _ => ActorOf(action, source)?.Owner
        };

        /// <summary>
        /// Intelligence: what an Intelligence Watchdog watches (Glomar Response). A Reveal, Revealed landing, or the play of a
        /// spell that does either.
        /// </summary>
        public static bool Reveals(IGameAction action) => action switch {
            RevealAction => true,
            ApplyStatusEffectAction status => status.GetStatusType() == StatusEffectType.Revealed,
            PlayCardAction play => play.GetCard() is Spell spell && RevealsSomething(spell),
            PlaySpellAction cast => RevealsSomething(cast.GetSpell()),
            _ => false
        };

        /// <summary>Whether it deals damage: an attack, or a spell with a damage effect.</summary>
        public static bool Damages(IGameAction action) => action switch {
            BattlefieldCombatAction => true,
            PlayCardAction play => play.GetCard() is Spell spell && DealsDamage(spell),
            PlaySpellAction cast => DealsDamage(cast.GetSpell()),
            _ => false
        };

        private static bool DealsDamage(Spell spell) => spell != null && spell.Effects.SelectMany(e => e.actions).Any(a => a.actionType == ActionType.Damage);

        private static bool RevealsSomething(Spell spell) => spell != null && spell.Effects.SelectMany(e => e.actions)
            .Any(a => a.actionType == ActionType.Reveal || (a.actionType == ActionType.ApplyStatus && a.statusEffectToApply == StatusEffectType.Revealed));

        /// <summary>What players call each effect (the user, 2026-10-16): Nullify is Disable, Backfire a Redirect to the attacker.</summary>
        public static string NameOf(WatchdogEffect effect) => effect switch {
            WatchdogEffect.Nullify => "Disable",
            WatchdogEffect.Backfire => "Redirect to the attacker",
            _ => effect.ToString()
        };

        /// <summary>Backfire: the attack is replaced by its own Attack as damage to the attacker. Other actions are just nullified (null).</summary>
        public static IGameAction Backfire(IGameAction action) =>
            action is BattlefieldCombatAction attack && attack.GetAttacker() is ICreature attacker && attacker.Attack > 0
                ? new DamageCreatureAction(attacker, attacker.Attack) : null;

        /// <summary>The number "N or less" compares: an attack's Attack. Other actions have none (0): each Watchdog card gets
        /// its own filter, and a number for other actions waits until a card needs one.</summary>
        public static int StrengthOf(IGameAction action) => action is BattlefieldCombatAction attack ? attack.GetAttacker()?.Attack ?? 0 : 0;

        /// <summary>
        /// Redirect: the same action aimed at another creature, on either side, chosen at random; Heat Signature draws it to
        /// the creature with the most Heat first. Never at the creature acting or the one it was aimed at. Itself if there's none.
        /// </summary>
        public static IGameAction Redirect(IGameAction action, ICreature source) {
            var actor = ActorOf(action, source);
            switch (action) {
                case BattlefieldCombatAction attack when attack.GetTargetSlot() != null:
                    var slot = NewTarget(attack.GetTargetSlot(), actor, attack.GetAttacker()?.Owner);
                    return slot != null ? new BattlefieldCombatAction(attack.GetAttacker(), slot, attack.IsLastRites) : action;
                case PlayCardAction play when play.GetCard() is Spell && AimedSlot(play.GetTarget()) is BoardSlot aimed:
                    var newSlot = NewTarget(aimed, actor, play.GetOwner());
                    return newSlot != null ? new PlayCardAction(play.GetCard(), play.GetOwner(), newSlot) : action;
                default:
                    return action;
            }
        }

        private static BoardSlot AimedSlot(ITarget target) => target switch {
            HomingTarget homing => homing.CurrentSlot,
            BoardSlot slot => slot,
            ICreature creature => creature.Slot,
            _ => null
        };

        private static BoardSlot NewTarget(BoardSlot from, ICreature actor, IPlayer side) {
            var exclude = new[] { actor, from.OccupyingCreature };
            var hottest = HeatRules.Hottest(actor, side, exclude);
            if (hottest != null) return hottest.Slot;

            var board = side != null ? side.Battlefield.Concat(side.Opponent?.Battlefield ?? Enumerable.Empty<BoardSlot>()) : from.Owner.Battlefield;
            var others = board.Where(s => s.IsOccupied() && !exclude.Contains(s.OccupyingCreature)).ToList();
            if (others.Count == 0) return null;
            return others[GameRandom.Next(others.Count)];
        }

        public static void LogTrigger(Watchdog watchdog, IGameAction action, string what) =>
            Log($"{action.GetType().Name} {what} by {watchdog}", LogTag.Actions | LogTag.Effects);
    }
}
