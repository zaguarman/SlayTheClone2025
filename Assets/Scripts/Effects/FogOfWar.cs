using System;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Who can see what (GDD 2.5 Fog of War & Intelligence). Your own side has no fog. Of the other side you see its planned
    /// actions, except the Concealed Actions of a creature that is Concealed or Undercover (and not Revealed); you don't see
    /// its hidden Slot Effects or its Watchdog conditions. You see that it's summoning, not what: a creature shows what it is
    /// when its summon finishes, unless it's stealthy. A Reveal shows one of them to whoever cast it.
    /// These are rules: Watchdogs can't match what their controller can't see, whatever the screen shows.
    /// </summary>
    public static class Intel {
        /// <summary>The creature whose planned action it is: an attack, a move, or the use of an active ability.</summary>
        public static ICreature PlannerOf(IGameAction action) => action switch {
            BattlefieldCombatAction attack when !attack.IsLastRites => attack.GetAttacker(),
            MoveCreatureAction move => move.GetCreature(),
            UseAbilityAction use => use.Creature,
            _ => null
        };

        /// <summary>Its planned actions are Concealed Actions: it's Concealed or Undercover, and not Revealed.</summary>
        public static bool IsConcealed(ICreature creature, IModifierManager modifiers) =>
            creature != null && modifiers != null
            && (modifiers.HasStatusEffect(creature, StatusEffectType.Concealed) || Abilities.Has(creature, CreatureAbility.Undercover))
            && !modifiers.HasStatusEffect(creature, StatusEffectType.Revealed);

        /// <summary>Watchdogs ignore its actions, and spend no charge on them.</summary>
        public static bool IsEncrypted(ICreature creature, IModifierManager modifiers) =>
            creature != null && modifiers != null && modifiers.HasStatusEffect(creature, StatusEffectType.Encrypted);

        public static bool CanSee(IPlayer viewer, IGameAction action, IActionsQueue queue, IModifierManager modifiers) {
            var planner = PlannerOf(action);
            if (planner == null || viewer == null || planner.Owner == viewer) return true;
            return !IsConcealed(planner, modifiers) || (queue?.IsRevealedTo(viewer, action) ?? false);
        }

        public static bool CanSee(IPlayer viewer, SlotEffect effect) =>
            effect == null || !effect.IsHidden || effect.Controller == viewer || effect.RevealedTo.Contains(viewer);

        public static bool CanSeeCondition(IPlayer viewer, Watchdog watchdog) =>
            watchdog.Controller == viewer || watchdog.RevealedTo.Contains(viewer);

        #region What a creature is
        /// <summary>Stealthy: it keeps what it is hidden when it enters play. Undercover (printed, even before it's ready) or Concealed, and not Revealed.</summary>
        public static bool IsStealthy(ICreature creature, IModifierManager modifiers) =>
            creature != null && modifiers != null
            && (creature.Ability == CreatureAbility.Undercover || modifiers.HasStatusEffect(creature, StatusEffectType.Concealed))
            && !modifiers.HasStatusEffect(creature, StatusEffectType.Revealed);

        /// <summary>Its summon finished: the other side sees what it is, unless it's stealthy.</summary>
        public static void OnEnteredPlay(ICreature creature, IModifierManager modifiers) {
            if (creature?.Owner == null || IsStealthy(creature, modifiers)) return;
            MakeKnown(creature, creature.Owner.Opponent);
        }

        public static void MakeKnown(ICreature creature, IPlayer viewer) {
            if (creature == null || viewer == null || viewer == creature.Owner) return;
            if (creature.KnownTo.Add(viewer)) Log($"{viewer.Name} now knows what {creature.Name} is", LogTag.Effects);
        }

        public static bool KnowsWhatItIs(IPlayer viewer, ICreature creature) =>
            creature == null || viewer == null || creature.Owner == viewer || creature.KnownTo.Contains(viewer);

        /// <summary>The slot a creature is being summoned into, for a queued play or summon of a creature.</summary>
        public static BoardSlot SummonInto(IGameAction action) => action switch {
            PlayCardAction play when play.GetCard() is ICreature => play.GetTarget() as BoardSlot,
            SummonCreatureAction summon => summon.GetTargetSlot() as BoardSlot,
            _ => null
        };

        /// <summary>Whose summon it is: the other side knows something is coming, not what, unless a Reveal showed it.</summary>
        public static bool CanSeeSummon(IPlayer viewer, IGameAction action, IActionsQueue queue) {
            var owner = action switch {
                PlayCardAction play => play.GetOwner(),
                SummonCreatureAction summon => summon.GetOwner(),
                _ => null
            };
            return owner == null || viewer == null || owner == viewer || (queue?.IsRevealedTo(viewer, action) ?? false);
        }
        #endregion
    }

    /// <summary>
    /// What the screen hides from the player at it: player 1, in hot-seat (player 2 plays on the same screen, so their own
    /// hidden things are hidden from them too). Switched off for debugging (Debug > Fog of War, or F9 in play), the screen shows
    /// everything; the rules in <see cref="Intel"/> don't change.
    /// </summary>
    public static class FogOfWar {
        public const UnityEngine.KeyCode ToggleKey = UnityEngine.KeyCode.F9;
        private const string PrefKey = "SlayTheClone.FogOfWar";

        private static bool? enabled;

        /// <summary>On unless switched off for debugging. In the editor the choice is kept between sessions.</summary>
        public static bool Enabled {
            get {
#if UNITY_EDITOR
                enabled ??= UnityEditor.EditorPrefs.GetBool(PrefKey, true);
#endif
                return enabled ?? true;
            }
            set {
                if (Enabled == value) return;
                enabled = value;
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetBool(PrefKey, value);
#endif
                Log($"Fog of War {(value ? "on" : "off (debug: the screen shows everything)")}", LogTag.UI);
                Changed?.Invoke();
            }
        }

        /// <summary>Raised when it's switched on or off, so the views redraw.</summary>
        public static event Action Changed;

        public static void Toggle() => Enabled = !Enabled;

        /// <summary>The player at the screen: player 1.</summary>
        public static IPlayer ViewerOf(IPlayer anyPlayer) => anyPlayer == null || anyPlayer.IsPlayer1 ? anyPlayer : anyPlayer.Opponent;

        public static bool Hides(IPlayer viewer, IGameAction action, IActionsQueue queue, IModifierManager modifiers) =>
            Enabled && !Intel.CanSee(viewer, action, queue, modifiers);

        public static bool Hides(IPlayer viewer, SlotEffect effect) => Enabled && !Intel.CanSee(viewer, effect);

        public static bool HidesCondition(IPlayer viewer, Watchdog watchdog) => Enabled && !Intel.CanSeeCondition(viewer, watchdog);

        /// <summary>The card shows as "???": the viewer doesn't know what this creature is.</summary>
        public static bool HidesIdentity(IPlayer viewer, ICreature creature) => Enabled && !Intel.KnowsWhatItIs(viewer, creature);

        /// <summary>A creature being summoned by the other side shows as "???" until its summon finishes.</summary>
        public static bool HidesSummon(IPlayer viewer, IGameAction action, IActionsQueue queue) => Enabled && !Intel.CanSeeSummon(viewer, action, queue);

        /// <summary>Whether this creature has a planned action the viewer can't see: its card shows "???".</summary>
        public static bool HidesPlansOf(ICreature creature, IPlayer viewer, IActionsQueue queue, IModifierManager modifiers) =>
            creature != null && queue != null
            && queue.GetPendingActions().Any(a => Intel.PlannerOf(a) == creature && Hides(viewer, a, queue, modifiers));
    }
}
