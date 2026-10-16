using System;
using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The action queue as one player sees it (the Queue panel, see <see cref="QueuePanel"/>): what will happen, in the order it
    /// resolves, grouped by layer (GDD 3.7), with who does what to whom. Fog of War applies (GDD 2.5): the other side's Concealed
    /// plans, unknown creatures, the spells it plays from its hand and its hidden traps and Watchdogs show as "???". What the
    /// viewer has seen since shows for what it is: an action a Reveal (or a Watchdog) showed them, and on a past Day's queue,
    /// whatever played out in front of them; only secrets stay secret (a Kill Order, a hidden trap, a Watchdog's condition).
    /// With the fog switched off for debugging (F9) everything shows.
    /// </summary>
    public static class QueueView {
        private const string OursColor = "#8FE3A8";
        private const string TheirsColor = "#F19A8B";
        private const string Unknown = "???";

        /// <summary>A row of the queue: a layer's heading, or an action with what it does and its details for hovering.</summary>
        public class Item {
            public string Heading;  // A layer's heading row; the rest is empty
            public int Number;      // Its place in the resolution order
            public string Text;     // What it does, in a line
            public string Details;  // Shown when hovered: who, with what, on what, and when
            public string Icon;     // An EffectIcons sprite for the kind of action
            public bool Ours;       // Started by the viewer's side
            public IGameAction Action; // Hovering it shows this action's arrows alone; none for what's foretold
            public bool DaySeparator;  // A Day's title in the list that runs through every Day ("Day 2"); Heading holds it
            public bool Note;          // A line of plain text ("Nothing planned yet."), not an action
        }

        // What the viewer knows of one action: the fog as it is now, and whether it played out in front of them (Played) or a
        // Reveal or a Watchdog showed it to them (Revealed)
        private readonly struct Sight {
            public readonly IPlayer Viewer;
            public readonly IActionsQueue Queue;
            public readonly IModifierManager Modifiers;
            public readonly bool Played, Revealed;

            public Sight(IPlayer viewer, IActionsQueue queue, IModifierManager modifiers, IGameAction action, bool played, bool revealed) {
                Viewer = viewer;
                Queue = queue;
                Modifiers = modifiers;
                Played = played;
                Revealed = revealed || (queue?.IsRevealedTo(viewer, action) ?? false);
            }

            public bool Seen => Played || Revealed;

            // "plans ???": a plan the fog hides, unless it was seen, and not a secret that stays one after it lands
            public bool HidesPlan(IGameAction action) =>
                FogOfWar.Hides(Viewer, action, Queue, Modifiers) && (!Seen || FogOfWar.KeepsSecret(Viewer, action, Queue, Revealed));
        }

        /// <summary>The queue in resolution order, numbered, under a heading for each layer, then what's been foretold.</summary>
        public static List<Item> Items(IPlayer viewer, IActionsQueue queue, IModifierManager modifiers) {
            if (queue == null) return new List<Item>();
            var items = Actions(viewer, queue, modifiers, queue.GetPendingActions().Select(a => (a, queue.LayerOfPending(a))).ToList());
            items.AddRange(Foretold(viewer));
            items.AddRange(InForce(viewer));
            items.AddRange(RelicsOf(viewer));
            return items;
        }

        /// <summary>
        /// These actions, numbered, under a heading for each layer, as the viewer knows them now: one that played out in front
        /// of them (<paramref name="played"/>) or that a Reveal showed them (<paramref name="revealed"/>) shows for what it was.
        /// A past Day's queue, or the queue now.
        /// </summary>
        public static List<Item> Actions(IPlayer viewer, IActionsQueue queue, IModifierManager modifiers,
            IEnumerable<(IGameAction Action, ResolutionLayer Layer)> actions, Func<IGameAction, bool> played = null, Func<IPlayer, IGameAction, bool> revealed = null) {
            var items = new List<Item>();
            ResolutionLayer? layer = null;
            int number = 0;
            foreach (var (action, actionLayer) in actions) {
                var sight = new Sight(viewer, queue, modifiers, action, played?.Invoke(action) ?? false, revealed?.Invoke(viewer, action) ?? false);
                string text = Describe(action, sight);
                if (text == null) continue;
                if (actionLayer != layer) {
                    items.Add(new Item { Heading = LayerName(actionLayer) });
                    layer = actionLayer;
                }
                bool hidden = sight.HidesPlan(action);
                items.Add(new Item {
                    Number = ++number, Text = text, Icon = hidden ? EffectIcons.Conceal : IconOf(action), Ours = SideOf(action) == viewer, Action = action,
                    Details = Details(action, text, number, actionLayer, hidden, sight)
                });
            }
            return items;
        }

        /// <summary>
        /// What watches over a Day, as the viewer knows it: the Watchdogs set (the other side's condition ??? until a Reveal
        /// shows it) and the traps in the slots (the other side's only once seen), under an "On Watch" heading; none: nothing.
        /// </summary>
        public static List<Item> OnWatch(IPlayer viewer, IEnumerable<Watchdog> watchdogs, IEnumerable<(BoardSlot Slot, SlotEffect Effect)> traps) {
            var items = new List<Item>();
            foreach (var watchdog in watchdogs ?? Enumerable.Empty<Watchdog>()) {
                bool sees = watchdog.Controller == viewer || !FogOfWar.HidesCondition(viewer, watchdog);
                string text = $"{Whose(watchdog.Controller, viewer, capital: true)} Watchdog: {watchdog.Describe(sees)}";
                items.Add(new Item { Text = text, Details = text, Icon = EffectIcons.Reveal, Ours = watchdog.Controller == viewer });
            }
            foreach (var (slot, effect) in traps ?? Enumerable.Empty<(BoardSlot, SlotEffect)>()) {
                if (effect == null || FogOfWar.Hides(viewer, effect)) continue;
                string text = $"{Whose(effect.Controller, viewer, capital: true)} {effect.Name} on {SlotName(slot, viewer)}";
                items.Add(new Item { Text = text, Details = text, Icon = CardTextFormatter.SlotEffectIcon(effect.Type), Ours = effect.Controller == viewer });
            }
            if (items.Count > 0) items.Insert(0, new Item { Heading = "On Watch" });
            return items;
        }

        /// <summary>What's been foretold (Prophecy): not in the queue yet, but everyone heard it.</summary>
        public static List<Item> Foretold(IPlayer viewer) {
            var items = new List<Item>();
            var foretold = new[] { viewer, viewer?.Opponent }.Where(p => p != null).SelectMany(p => p.Prophecies).OrderBy(p => p.DueOnTurn).ToList();
            if (foretold.Count > 0) items.Add(new Item { Heading = "Foretold" });
            foreach (var prophecy in foretold) {
                string when = prophecy.EarliestTurn == prophecy.LatestTurn ? $"at the Dawn of Day {prophecy.DueOnTurn}"
                    : $"at a Dawn from Day {prophecy.EarliestTurn} to Day {prophecy.LatestTurn}, nobody knows which"; // The roll stays secret
                string text = prophecy.Part != null // A part of a spell that waits
                    ? $"{Whose(prophecy.Caster, viewer, capital: true)} {Painted(prophecy.Caster, viewer, prophecy.Spell.Name)}: the rest of it lands {when}"
                    : $"{PlayerName(prophecy.Caster, viewer)} foretold {Painted(prophecy.Caster, viewer, prophecy.Spell.Name)}: it comes true {when}";
                items.Add(new Item { Text = text, Icon = EffectIcons.Days, Ours = prophecy.Caster == viewer, Details = text });
            }
            return items;
        }

        /// <summary>The relics each side carries (an adventure's: yours, an elite's or a boss's): always on.</summary>
        public static List<Item> RelicsOf(IPlayer viewer) {
            var items = new List<Item>();
            foreach (var side in new[] { viewer, viewer?.Opponent }.Where(p => p != null && p.Relics.Count > 0)) {
                if (items.Count == 0) items.Add(new Item { Heading = "Relics" });
                foreach (var relic in side.Relics) {
                    string text = $"{Whose(side, viewer, capital: true)} {Relics.NameOf(relic)}";
                    items.Add(new Item { Text = text, Icon = EffectIcons.Bless, Ours = side == viewer, Details = $"<b>{Relics.NameOf(relic)}</b>: {Relics.Describe(relic)}" });
                }
            }
            return items;
        }

        /// <summary>The Policies in force, both sides': they happen again at each Dawn.</summary>
        public static List<Item> InForce(IPlayer viewer) {
            var items = new List<Item>();
            var sides = new[] { viewer, viewer?.Opponent }.Where(p => p != null).ToList();
            foreach (var side in sides) {
                foreach (var policy in side.Policies) {
                    if (items.Count == 0) items.Add(new Item { Heading = "In Force" });
                    string rules = policy.SourceData != null ? CardTextFormatter.GetRulesText(policy.SourceData).Replace("\n", " ") : "";
                    string text = $"{Whose(side, viewer, capital: true)} {Painted(side, viewer, policy.Name)} (Policy): again each Dawn";
                    items.Add(new Item { Text = text, Icon = EffectIcons.Dawn, Ours = side == viewer, Details = $"<b>{policy.Name}</b>: {rules}" });
                }
            }
            return items;
        }

        /// <summary>One line per action, numbered in resolution order, under a heading for each layer.</summary>
        public static List<string> Lines(IPlayer viewer, IActionsQueue queue, IModifierManager modifiers) =>
            Items(viewer, queue, modifiers).Select(i => i.Heading != null ? $"<b>{i.Heading}</b>" : i.Number > 0 ? $"{i.Number}. {i.Text}" : i.Text).ToList();

        // What hovering an action tells: its place in the order, who does it (and their Speed, which orders a layer), the card
        // it comes from, and what's at the other end. Nothing the viewer can't see.
        private static string Details(IGameAction action, string text, int number, ResolutionLayer layer, bool hidden, Sight sight) {
            var viewer = sight.Viewer;
            var lines = new List<string> { $"<b>{number}. {text}</b>", $"<size=85%><color=#C9C0AE>{LayerName(layer)}: {LayerRule(layer)}</color></size>" };
            if (hidden) {
                lines.Add("Concealed: you can't see what it plans until it resolves, or a Reveal shows it.");
                return string.Join("\n", lines);
            }
            var actor = ActorOf(action);
            if (actor != null && !FogOfWar.HidesIdentity(viewer, actor)) {
                lines.Add($"{Who(actor, viewer)}: {actor.Attack} Attack, {actor.Health} Health, Speed {actor.Speed}");
            }
            var card = CardOf(action);
            // The other side's card: seen once it played out or a Reveal showed it, unless it's a creature still unknown (stealthy)
            bool cardHidden = card != null && FogOfWar.Enabled && OwnerOf(action) != viewer
                && !(sight.Seen && !(sight.Played && card is ICreature creature && FogOfWar.HidesIdentity(viewer, creature)));
            if (card?.SourceData != null && !cardHidden) {
                string rules = CardTextFormatter.GetRulesText(card.SourceData);
                lines.Add($"<b>{card.Name}</b>" + (string.IsNullOrEmpty(rules) ? "" : $": {rules.Replace("\n", " ")}"));
            }
            var target = TargetOf(action);
            if (card is Spell spell && !EffectResolver.IsAimed(spell)) {
                var side = target switch { IPlayer player => player, BoardSlot slot => slot.Owner, HomingTarget homing => homing.AimedSlot?.Owner, _ => null };
                if (side != null) lines.Add($"On: {Whose(side, viewer)} side, as a whole");
            }
            else if (target != null) lines.Add($"On: {Target(target, viewer)}");
            return string.Join("\n", lines);
        }

        private static string LayerRule(ResolutionLayer layer) => layer switch {
            ResolutionLayer.Movement => "first, before anything else.",
            ResolutionLayer.Summon => "after the moves.",
            ResolutionLayer.Spell => "after the summons.",
            ResolutionLayer.LateArrivals => "last, after the attacks: the creature it replaces takes the Day's hits first (Sorry I'm Late, He Protec).",
            _ => "last; the faster creature acts first."
        };

        // The creature doing it, if a creature does it
        internal static ICreature ActorOf(IGameAction action) => Intel.PlannerOf(action) ?? action switch {
            BattlefieldCombatAction attack => attack.GetAttacker(),
            DamageCreatureAction damage => damage.GetAttacker(),
            DamagePlayerAction damage => damage.GetAttacker(),
            ModifyAction modify => modify.Source,
            ApplyStatusEffectAction status => status.Source,
            _ => null
        };

        private static ICard CardOf(IGameAction action) => action switch {
            PlayCardAction play => play.GetCard(),
            PlaySpellAction cast => cast.GetSpell(),
            SummonCreatureAction summon => summon.GetCreature(),
            _ => null
        };

        private static IPlayer OwnerOf(IGameAction action) => action switch {
            PlayCardAction play => play.GetOwner(),
            PlaySpellAction cast => cast.GetOwner(),
            SummonCreatureAction summon => summon.GetOwner(),
            _ => null
        };

        // Whose side started it: the creature doing it, the player playing it, or the one it's for
        internal static IPlayer SideOf(IGameAction action) => ActorOf(action)?.Owner ?? OwnerOf(action) ?? action switch {
            DrawCardsAction draw => draw.GetPlayer(),
            DiscardHandAction discard => discard.GetPlayer(),
            PlaceSlotEffectAction place => place.GetController(),
            CreateWatchdogAction watchdog => watchdog.Controller,
            RevealAction reveal => reveal.Viewer,
            _ => null
        };

        private static ITarget TargetOf(IGameAction action) => action switch {
            PlayCardAction play => play.GetTarget(),
            PlaySpellAction cast => cast.GetTarget(),
            BattlefieldCombatAction attack => attack.GetTargetSlot(),
            UseAbilityAction use => use.TargetSlot,
            MoveCreatureAction move => move.GetToSlot(),
            _ => null
        };

        private static string IconOf(IGameAction action) => action switch {
            PlayCardAction play when play.GetCard() is ICreature => EffectIcons.Summon,
            SummonCreatureAction => EffectIcons.Summon,
            PlayCardAction or PlaySpellAction => EffectIcons.Enter,
            MoveCreatureAction => EffectIcons.Speed,
            BattlefieldCombatAction attack => attack.IsLastRites ? EffectIcons.Doom : EffectIcons.Attack,
            UseAbilityAction => EffectIcons.Chain,
            CopyStatsAction or StealStatChangesAction or GrantAbilityAction => EffectIcons.Buff,
            SwapPositionsAction => EffectIcons.Speed,
            DamageCreatureAction or DamagePlayerAction => EffectIcons.Damage,
            HealCreatureAction or HealPlayerAction => EffectIcons.Heal,
            ModifyAction modify => modify.GetValue() >= 0 ? EffectIcons.Buff : EffectIcons.Debuff,
            ModifyArmorAction => EffectIcons.Armor,
            ApplyStatusEffectAction status => StatusIcon(status.GetStatusType()),
            CleanseAction or CleanseSlotAction => EffectIcons.Cleanse,
            DelayDeploymentAction or RushAction => EffectIcons.Days,
            DestroyCreatureAction or DoomedSentenceAction => EffectIcons.Death,
            DrawCardsAction or DiscardHandAction => EffectIcons.Draw,
            RevealAction or CreateWatchdogAction => EffectIcons.Reveal,
            _ => null
        };

        private static string StatusIcon(StatusEffectType status) => status switch {
            StatusEffectType.Exhausted => EffectIcons.Paralyze,
            StatusEffectType.Burned => EffectIcons.Burn,
            StatusEffectType.Toxic => EffectIcons.Burn,
            StatusEffectType.Provoked => EffectIcons.Attack,
            StatusEffectType.Doomed => EffectIcons.Doom,
            StatusEffectType.Blessed => EffectIcons.Bless,
            StatusEffectType.HeatSignature => EffectIcons.Heat,
            StatusEffectType.Concealed => EffectIcons.Conceal,
            StatusEffectType.Revealed => EffectIcons.Reveal,
            StatusEffectType.Encrypted => EffectIcons.Encrypt,
            StatusEffectType.Untouchable => EffectIcons.Armor,
            StatusEffectType.Cooldown => EffectIcons.Days,
            _ => EffectIcons.Debuff
        };

        public static string LayerName(ResolutionLayer layer) => layer switch {
            ResolutionLayer.Movement => "Moves",
            ResolutionLayer.Summon => "Summons",
            ResolutionLayer.Spell => "Spells",
            ResolutionLayer.LateArrivals => "Late Arrivals",
            _ => "Abilities & Attacks"
        };

        /// <summary>What the action does, as the viewer may see it now; null for bookkeeping the player doesn't need to see.</summary>
        public static string Describe(IGameAction action, IPlayer viewer, IActionsQueue queue, IModifierManager modifiers) =>
            Describe(action, new Sight(viewer, queue, modifiers, action, false, false));

        private static string Describe(IGameAction action, Sight sight) {
            var viewer = sight.Viewer;
            if (sight.HidesPlan(action)) return $"{Who(Intel.PlannerOf(action), viewer)} plans {Unknown}";

            switch (action) {
                case PlayCardAction play when play.GetCard() is ICreature creature:
                    return Summons(play.GetOwner(), creature, play.GetTarget(), SummonedName(play, creature, sight), viewer);
                case SummonCreatureAction summon:
                    return Summons(summon.GetOwner(), summon.GetCreature(), summon.GetTargetSlot(), SummonedName(summon, summon.GetCreature(), sight), viewer)
                        + (summon.IsFromDeck() ? " from the deck" : "");
                // An enemy spell queued from the hand shows where it's aimed, not what it is, until it resolves or a Reveal shows it (GDD 2.5)
                case PlayCardAction play when play.GetCard() is Spell spell:
                    return Casts(play.GetOwner(), spell, play.GetTarget(), FogOfWar.Enabled && play.GetOwner() != viewer && !sight.Seen, viewer);
                case PlaySpellAction cast:
                    return Casts(cast.GetOwner(), cast.GetSpell(), cast.GetTarget(), FogOfWar.Enabled && cast.GetOwner() != viewer && !sight.Seen, viewer);
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
                case RushAction rush:
                    return $"Up to -{rush.Days} Deployment Time to {Who(rush.Creature, viewer)}, then Burnout";
                case DestroyCreatureAction destroy:
                    return $"Destroy {Who(destroy.GetCreature(), viewer)}";
                case DoomedSentenceAction sentence:
                    return $"{Who(sentence.GetCreature(), viewer)}'s Doomed sentence";
                case CheckLethalAction:
                    return null; // Bookkeeping: whether a creature that reached 0 Health dies
                case SwapStatsAction swapStats:
                    return $"{Who(swapStats.Creature, viewer)}'s Attack and Health trade places";
                case BorrowedTimeEndsAction:
                    return null; // Bookkeeping: the Dusk it ends at
                case TrapSprungAction:
                    return null; // Only the screen's: the trap's card shown while its damage lands
                case DrawCardsAction draw:
                    return $"{PlayerName(draw.GetPlayer(), viewer)} {(draw.GetPlayer() == viewer ? "draw" : "draws")} {draw.GetAmount()}";
                case DiscardHandAction discard:
                    return $"{PlayerName(discard.GetPlayer(), viewer)} {(discard.GetPlayer() == viewer ? "discard" : "discards")} the hand";
                case PlaceSlotEffectAction place:
                    var effect = new SlotEffect(place.GetEffectType(), place.GetController(), place.GetValue(), place.GetDays(), 0);
                    // A hidden trap stays ??? once it's down, unless a Reveal shows it there
                    var laid = place.GetSlot()?.Effect;
                    bool trapSeen = sight.Revealed || (sight.Played && laid != null && laid.Type == effect.Type && laid.Controller == effect.Controller
                                                       && Intel.CanSee(viewer, laid));
                    bool hiddenTrap = FogOfWar.Enabled && effect.IsHidden && place.GetController() != viewer && !trapSeen;
                    return $"{PlayerName(place.GetController(), viewer)} {(place.GetController() == viewer ? "place" : "places")} " +
                           $"{(hiddenTrap ? Unknown : effect.Name)} on {SlotName(place.GetSlot(), viewer)}";
                case CleanseSlotAction clear:
                    return $"Clear {SlotName(clear.GetSlot(), viewer)}";
                case SwapSlotEffectsAction swap:
                    return $"{PlayerName(swap.Controller, viewer)} {(swap.Controller == viewer ? "swap" : "swaps")} the Slot Effects of both sides";
                case CreateWatchdogAction watchdog:
                    // Its condition stays unknown to the other side until a Reveal shows it
                    bool hiddenCondition = FogOfWar.Enabled && watchdog.Controller != viewer && !sight.Revealed
                                           && !(watchdog.Created != null && Intel.CanSeeCondition(viewer, watchdog.Created));
                    return $"{PlayerName(watchdog.Controller, viewer)} {(watchdog.Controller == viewer ? "set" : "sets")} a Watchdog" +
                           (hiddenCondition ? "" : $" ({watchdog.Spec.side} {watchdog.Spec.watches}: {WatchdogRules.NameOf(watchdog.Spec.effect)})");
                case RevealAction reveal:
                    return $"{PlayerName(reveal.Viewer, viewer)} {(reveal.Viewer == viewer ? "look" : "looks")} at " +
                           (reveal.Slot != null ? SlotName(reveal.Slot, viewer) : $"{Whose(reveal.Side, viewer)} Watchdogs");
                case SetTrendAction trend:
                    return $"Now trending: {TrendSystem.NameOf(trend.Trend)}";
                case EndDayAction amen:
                    return $"{Painted(amen.Controller, viewer, amen.Source?.Name ?? "Amen")}: the Day ends here, the rest is discarded";
                case EarthquakeAction quake:
                    return $"Earthquake: {quake.Damage} damage in a zigzag across the board";
                default:
                    string name = action.GetType().Name;
                    return name.EndsWith("Action") ? name.Substring(0, name.Length - "Action".Length) : name;
            }
        }

        // Who's summoned, as the viewer knows: on its way, ??? for the other side's unless a Reveal showed it; once it's in, what
        // the creature shows (??? while it's stealthy, a decoy's fake name)
        private static string SummonedName(IGameAction summon, ICreature creature, Sight sight) {
            var owner = summon is PlayCardAction play ? play.GetOwner() : (summon as SummonCreatureAction)?.GetOwner();
            if (sight.Played && creature?.Owner != null) return Who(creature, sight.Viewer);
            bool hidden = FogOfWar.HidesSummon(sight.Viewer, summon, sight.Queue) && !sight.Revealed;
            return Painted(owner, sight.Viewer, hidden ? Unknown : creature?.Name ?? Unknown); // Not in play yet: it has no side
        }

        private static string Summons(IPlayer owner, ICreature creature, ITarget slot, string name, IPlayer viewer) {
            string into = slot is BoardSlot boardSlot ? $" into {SlotName(boardSlot, viewer)}" : "";
            return $"{PlayerName(owner, viewer)} {(owner == viewer ? "summon" : "summons")} {name}{into}";
        }

        private static string Casts(IPlayer caster, Spell spell, ITarget target, bool hidden, IPlayer viewer) {
            string name = Painted(caster, viewer, hidden ? $"a spell ({Unknown})" : spell.Name);
            string aim = target switch {
                null => "",
                IPlayer side => $" on {Whose(side, viewer)} side",
                _ when spell != null && !EffectResolver.IsAimed(spell) => "",
                _ => $" at {Target(target, viewer)}"
            };
            return $"{PlayerName(caster, viewer)} {(caster == viewer ? "cast" : "casts")} {name}{aim}";
        }

        private static string StatusName(StatusEffectType status) => status == StatusEffectType.HeatSignature ? "Heat Signature" : status.ToString();

        private static string MoveVerb(MoveKind kind) => kind switch {
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
            var disguise = FogOfWar.DisguiseFor(viewer, creature);
            return Painted(creature.Owner, viewer, disguise != null ? disguise.cardName : FogOfWar.HidesIdentity(viewer, creature) ? Unknown : creature.Name);
        }

        private static string PlayerName(IPlayer player, IPlayer viewer) =>
            player == null ? Unknown : Painted(player, viewer, player == viewer ? "You" : "Enemy");

        private static string SlotName(BoardSlot slot, IPlayer viewer) =>
            slot == null ? Unknown : Painted(slot.Owner, viewer, $"{(slot.Owner == viewer ? "your" : "enemy")} {Formation.Describe(slot.Index)}");

        // "your", or "the enemy's", painted in the side's color
        private static string Whose(IPlayer player, IPlayer viewer, bool capital = false) {
            if (player == null) return Unknown;
            string whose = player == viewer ? "your" : "the enemy's";
            return Painted(player, viewer, capital ? char.ToUpper(whose[0]) + whose.Substring(1) : whose);
        }

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
