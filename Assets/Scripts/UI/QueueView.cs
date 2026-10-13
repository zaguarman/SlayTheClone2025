using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The action queue as one player sees it (the Queue panel, see <see cref="QueuePanel"/>): what will happen, in the order it
    /// resolves, grouped by layer (GDD 3.7), with who does what to whom. Fog of War applies (GDD 2.5): the other side's Concealed
    /// plans, unknown creatures, the spells it plays from its hand and its hidden traps and Watchdogs show as "???". With the fog
    /// switched off for debugging (F9) everything shows.
    /// </summary>
    public static class QueueView {
        private const string OursColor = "#8FE3A8";
        private const string TheirsColor = "#F19A8B";
        private const string Unknown = "???";

        /// <summary>One line per action, numbered in resolution order, under a heading for each layer.</summary>
        public static List<string> Lines(IPlayer viewer, IActionsQueue queue, IModifierManager modifiers) {
            var lines = new List<string>();
            if (queue == null) return lines;
            ResolutionLayer? layer = null;
            int number = 0;
            foreach (var action in queue.GetPendingActions()) {
                string text = Describe(action, viewer, queue, modifiers);
                if (text == null) continue;
                var actionLayer = queue.LayerOfPending(action);
                if (actionLayer != layer) {
                    lines.Add($"<b>{LayerName(actionLayer)}</b>");
                    layer = actionLayer;
                }
                lines.Add($"{++number}. {text}");
            }
            return lines;
        }

        public static string LayerName(ResolutionLayer layer) => layer switch {
            ResolutionLayer.Movement => "Moves",
            ResolutionLayer.Summon => "Summons",
            ResolutionLayer.Spell => "Spells",
            _ => "Abilities & Attacks"
        };

        /// <summary>What the action does, as the viewer may see it; null for bookkeeping the player doesn't need to see.</summary>
        public static string Describe(IGameAction action, IPlayer viewer, IActionsQueue queue, IModifierManager modifiers) {
            if (FogOfWar.Hides(viewer, action, queue, modifiers)) return $"{Who(Intel.PlannerOf(action), viewer)} plans {Unknown}";

            switch (action) {
                case PlayCardAction play when play.GetCard() is ICreature creature:
                    return Summons(play.GetOwner(), creature, play.GetTarget(), FogOfWar.HidesSummon(viewer, play, queue), viewer);
                case SummonCreatureAction summon:
                    return Summons(summon.GetOwner(), summon.GetCreature(), summon.GetTargetSlot(), FogOfWar.HidesSummon(viewer, summon, queue), viewer)
                        + (summon.IsFromDeck() ? " from the deck" : "");
                case PlayCardAction play when play.GetCard() is Spell spell:
                    return Casts(play.GetOwner(), spell, play.GetTarget(), viewer);
                case PlaySpellAction cast:
                    return Casts(cast.GetOwner(), cast.GetSpell(), cast.GetTarget(), viewer);
                case MoveCreatureAction move:
                    return $"{Who(move.GetCreature(), viewer)} {MoveVerb(move.Kind)} {SlotName(move.GetToSlot(), viewer)}";
                case BattlefieldCombatAction attack:
                    return attack.IsLastRites
                        ? $"{Who(attack.GetAttacker(), viewer)}'s Last Rites strike {Target(attack.GetTargetSlot(), viewer)}"
                        : $"{Who(attack.GetAttacker(), viewer)} attacks {Target(attack.GetTargetSlot(), viewer)}";
                case UseAbilityAction use:
                    return $"{Who(use.Creature, viewer)} uses {Abilities.NameOf(use.Creature.Ability)} on {Target(use.TargetSlot, viewer)}";
                case DamageCreatureAction damage:
                    return $"{By(damage.GetAttacker(), viewer)}{damage.GetDamage()} damage to {Who(damage.GetTarget(), viewer)}";
                case DamagePlayerAction damage:
                    return $"{By(damage.GetAttacker(), viewer)}{damage.GetDamage()} damage to {PlayerName(damage.GetTargetPlayer(), viewer)}";
                case HealCreatureAction heal:
                    return $"Heal {Who(heal.GetTarget(), viewer)} for {heal.GetAmount()}";
                case HealPlayerAction heal:
                    return $"Heal {PlayerName(heal.GetTargetPlayer(), viewer)} for {heal.GetAmount()}";
                case ModifyAction modify:
                    return $"{By(modify.Source, viewer)}{StatChange(modify)} to {Target(modify.GetTarget(), viewer)}";
                case ModifyArmorAction armor:
                    return $"{armor.GetAmount():+0;-0} Armor to {Who(armor.GetTargetCreature(), viewer)}";
                case ApplyStatusEffectAction status:
                    return $"{By(status.Source, viewer)}{StatusName(status.GetStatusType())} on {Who(status.GetTargetCreature(), viewer)}";
                case CleanseAction cleanse:
                    return $"{(cleanse.IsPurge ? "Purge" : "Cleanse")} {Who(cleanse.GetTargetCreature(), viewer)}";
                case DelayDeploymentAction delay:
                    return $"+{delay.Days} Deployment Time to {Who(delay.Creature, viewer)}";
                case DestroyCreatureAction destroy:
                    return $"Destroy {Who(destroy.GetCreature(), viewer)}";
                case DoomedSentenceAction sentence:
                    return $"{Who(sentence.GetCreature(), viewer)}'s Doomed sentence";
                case CheckLethalAction:
                    return null; // Bookkeeping: whether a creature that reached 0 Health dies
                case DrawCardsAction draw:
                    return $"{PlayerName(draw.GetPlayer(), viewer)} {(draw.GetPlayer() == viewer ? "draw" : "draws")} {draw.GetAmount()}";
                case DiscardHandAction discard:
                    return $"{PlayerName(discard.GetPlayer(), viewer)} {(discard.GetPlayer() == viewer ? "discard" : "discards")} the hand";
                case PlaceSlotEffectAction place:
                    var effect = new SlotEffect(place.GetEffectType(), place.GetController(), place.GetValue(), place.GetDays(), 0);
                    bool hiddenTrap = FogOfWar.Enabled && effect.IsHidden && place.GetController() != viewer;
                    return $"{PlayerName(place.GetController(), viewer)} {(place.GetController() == viewer ? "place" : "places")} " +
                           $"{(hiddenTrap ? Unknown : effect.Name)} on {SlotName(place.GetSlot(), viewer)}";
                case CleanseSlotAction clear:
                    return $"Clear {SlotName(clear.GetSlot(), viewer)}";
                case CreateWatchdogAction watchdog:
                    bool hiddenCondition = FogOfWar.Enabled && watchdog.Controller != viewer;
                    return $"{PlayerName(watchdog.Controller, viewer)} {(watchdog.Controller == viewer ? "set" : "sets")} a Watchdog" +
                           (hiddenCondition ? "" : $" ({watchdog.Spec.side} {watchdog.Spec.watches}: {watchdog.Spec.effect})");
                case RevealAction reveal:
                    return $"{PlayerName(reveal.Viewer, viewer)} {(reveal.Viewer == viewer ? "look" : "looks")} at " +
                           (reveal.Slot != null ? SlotName(reveal.Slot, viewer) : $"{PlayerName(reveal.Side, viewer)}'s Watchdogs");
                case ChangeWeatherAction weather:
                    return $"The weather turns {weather.GetTargetWeather()}";
                default:
                    string name = action.GetType().Name;
                    return name.EndsWith("Action") ? name.Substring(0, name.Length - "Action".Length) : name;
            }
        }

        private static string Summons(IPlayer owner, ICreature creature, ITarget slot, bool hidden, IPlayer viewer) {
            string name = Painted(owner, viewer, hidden ? Unknown : creature?.Name ?? Unknown); // Not in play yet: it has no side
            string into = slot is BoardSlot boardSlot ? $" into {SlotName(boardSlot, viewer)}" : "";
            return $"{PlayerName(owner, viewer)} {(owner == viewer ? "summon" : "summons")} {name}{into}";
        }

        // An enemy spell queued from the hand shows where it's aimed, not what it is, until it resolves (GDD 2.5)
        private static string Casts(IPlayer caster, Spell spell, ITarget target, IPlayer viewer) {
            bool hidden = FogOfWar.Enabled && caster != viewer;
            string name = Painted(caster, viewer, hidden ? $"a spell ({Unknown})" : spell.Name);
            string aim = target switch {
                null => "",
                IPlayer side => $" on {PlayerName(side, viewer)}'s side",
                _ when spell != null && !EffectResolver.IsAimed(spell) => "",
                _ => $" at {Target(target, viewer)}"
            };
            return $"{PlayerName(caster, viewer)} {(caster == viewer ? "cast" : "casts")} {name}{aim}";
        }

        private static string StatusName(StatusEffectType status) => status == StatusEffectType.HeatSignature ? "Heat Signal" : status.ToString();

        private static string MoveVerb(MoveKind kind) => kind switch {
            MoveKind.Push => "pushes into",
            MoveKind.Leapfrog => "leapfrogs to",
            MoveKind.Swap => "swaps with",
            _ => "moves to"
        };

        private static string StatChange(ModifyAction modify) {
            var stats = new List<string>();
            if (modify.ShouldModifyAttack()) stats.Add("Attack");
            if (modify.ShouldModifyHealth()) stats.Add("Health");
            if (modify.ShouldModifySpeed()) stats.Add("Speed");
            return $"{modify.GetValue():+0;-0} {string.Join("/", stats)}";
        }

        // "Mole: " before what a creature does to something else
        private static string By(ICreature source, IPlayer viewer) => source != null ? $"{Who(source, viewer)}: " : "";

        private static string Who(ICreature creature, IPlayer viewer) {
            if (creature == null) return Unknown;
            return Painted(creature.Owner, viewer, FogOfWar.HidesIdentity(viewer, creature) ? Unknown : creature.Name);
        }

        private static string PlayerName(IPlayer player, IPlayer viewer) =>
            player == null ? Unknown : Painted(player, viewer, player == viewer ? "You" : "Enemy");

        private static string SlotName(BoardSlot slot, IPlayer viewer) =>
            slot == null ? Unknown : Painted(slot.Owner, viewer, $"{(slot.Owner == viewer ? "your" : "enemy")} slot {slot.Index + 1}");

        // A creature, or the slot when nobody stands there
        private static string Target(ITarget target, IPlayer viewer) => target switch {
            HomingTarget homing when homing.CreatureInPlay != null => Who(homing.CreatureInPlay, viewer),
            HomingTarget homing => SlotName(homing.CurrentSlot ?? homing.AimedSlot, viewer),
            BoardSlot slot when slot.OccupyingCreature != null => $"{Who(slot.OccupyingCreature, viewer)} ({SlotName(slot, viewer)})",
            BoardSlot slot => SlotName(slot, viewer),
            ICreature creature => Who(creature, viewer),
            IPlayer player => PlayerName(player, viewer),
            _ => Unknown
        };

        private static string Painted(IPlayer side, IPlayer viewer, string text) =>
            $"<color={(side == viewer ? OursColor : TheirsColor)}>{text}</color>";
    }
}
