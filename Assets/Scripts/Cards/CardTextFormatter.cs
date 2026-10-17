using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Builds the printed text of a card from its data, in the style of Magic: The Gathering and Slay the Spire:
    /// a line of keywords first (movement, ability, and keyword abilities like Counterattack or Decay 1 for effects that
    /// keep coming back), then the rest as short templated clauses led by a trigger keyword ("Dawn: draw a card"): what it
    /// does, then whom it targets ("target", "all enemy creatures", "a random creature", "itself"). A keyword's number is
    /// left out when it's 1 ("Summon", "Bless", "Doom"), except an amount of damage or healing, which always shows.
    /// Lines don't end with a full stop: each line is one rule.
    /// Every keyword is a link to its explanation (<see cref="Keywords"/>), shown when it's hovered on an enlarged card.
    /// </summary>
    public static class CardTextFormatter {

        public static string GetTypeLine(CardData data) {
            string type = data is SpellData ? EffectResolver.IsTrend(data.effects) ? "Trend" : "Spell" : "Creature";
            return data.faction == Faction.None ? type : $"{type} — {FactionName(data.faction)}";
        }

        public static string FactionName(Faction faction) {
            switch (faction) {
                case Faction.ShadowOps: return "Shadow Ops.";
                case Faction.DigitalSociety: return "Digital Society";
                default: return faction.ToString();
            }
        }

        /// <param name="ability">A creature's ability in play, when it differs from the printed one: written in the text, the
        /// printed one struck through.</param>
        /// <param name="abilityOff">Its ability is switched off for now: struck through too.</param>
        /// <param name="effects">The effects as they are now, when they differ from the printed ones (a Ripening spell ripened).</param>
        /// <param name="gear">A creature's Gear in play (a held item), written after its ability.</param>
        public static string GetRulesText(CardData data, CreatureAbility? ability = null, bool abilityOff = false, IEnumerable<CardEffect> effects = null,
            CreatureAbility gear = CreatureAbility.None) {
            var creature = data as CreatureData;
            bool isSpell = data is SpellData;
            var keywords = new List<string>();
            if (creature != null) {
                if (creature.movement.HasFlag(MovementKeyword.Leapfrog)) keywords.Add(Keywords.Link("Leapfrog"));
                if (creature.movement.HasFlag(MovementKeyword.Swap)) keywords.Add(Keywords.Link("Swap"));
                if (creature.gift) keywords.Add(Keywords.Link("Gift"));
                if (creature.rootedWhileDeploying) keywords.Add(Keywords.Link("Rooted") + " while deploying");
                var printed = creature.ability;
                var now = ability ?? printed;
                if (printed != CreatureAbility.None && now != printed) keywords.Add($"<s>{Abilities.NameOf(printed)}</s>"); // Replaced or lost
                if (now != CreatureAbility.None) {
                    string name = now != printed ? $"{AbilityName(now)} (gained)" : AbilityName(now);
                    keywords.Add(abilityOff ? $"<s>{Abilities.NameOf(now)}</s> (switched off)" : name);
                }
                if (gear != CreatureAbility.None) keywords.Add($"{Keywords.Link("Gear")}: {AbilityName(gear)}");
            }
            if (data.homing) keywords.Add(Keywords.Link("Homing"));
            if (data.potion) keywords.Add(Keywords.Link("Potion"));
            else if (data.singleUse) keywords.Add(Keywords.Link("Single Use"));
            if (data.policy) keywords.Add(Keywords.Link("Policy") + " (it happens again each Dawn, for the rest of the combat)");
            if (data.combo) keywords.Add(Keywords.Link("Combo") + " (+1 to its numbers for each other spell you play this Day)");
            if (data.ripening) keywords.Add(Keywords.Link("Ripening") + $" (+1 to its numbers for each Dawn it's kept in your hand, up to +{Ripening.Most})");
            if (data.prophecyLatest > data.prophecy) keywords.Add($"{Keywords.Link("Prophecy", $"Prophecy {data.prophecy}-{data.prophecyLatest}")} (comes true in {data.prophecy} to {data.prophecyLatest} Days, at random)");
            else if (data.prophecy > 0) keywords.Add($"{Keywords.Link("Prophecy", $"Prophecy {data.prophecy}")} (comes true in {data.prophecy} Day{(data.prophecy == 1 ? "" : "s")})");

            var sentences = new List<string>();
            if (data.gamble) sentences.Add($"{Keywords.Link("Gamble")}: one of these, at random:");
            foreach (var effect in effects ?? data.effects ?? new List<CardEffect>()) {
                string keyword = isSpell ? null : KeywordAbility(effect);
                if (keyword != null) keywords.Add(keyword);
                else {
                    string sentence = DescribeEffect(effect, isSpell);
                    if (sentence != null) sentences.Add(data.gamble ? $"• {sentence}" : sentence);
                }
            }

            string keywordLine = keywords.Count > 0 ? string.Join(", ", keywords) : "";
            // An additional cost comes first, like Magic's: "Discard 2: give up 2 random other cards from your hand to play this".
            // A sacrifice comes last, after what it buys (the user, 2026-10-15): "Destroy target creature / Sacrifice 1: ..."
            string sacrifice = data.sacrifice > 0
                ? $"{Keywords.Link("Sacrifice", $"Sacrifice {data.sacrifice}")}: your {(data.sacrifice == 1 ? $"{Keywords.Link("Weakest", "weakest")} creature" : $"{data.sacrifice} {Keywords.Link("Weakest", "weakest")} creatures")} will be sacrificed to play this"
                : "";
            string cost = data.discardCost > 0
                ? $"{Keywords.Link("Discard Cost", $"Discard {data.discardCost}")}: give up {data.discardCost} {(data.discardChosen ? "" : "random ")}other card{(data.discardCost == 1 ? "" : "s")}{(data.discardChosen ? " of your choice" : "")} from your hand to play this"
                : data.doomCost > 0
                        ? $"{Keywords.Link("Doom Cost", $"Doom {data.doomCost}")}: your {Keywords.Link("Weakest", "weakest")} creature not yet {Keywords.Link("Doomed")} is Doomed (dies in {data.doomCost} Day{(data.doomCost == 1 ? "" : "s")}) to play this"
                        : "";
            return string.Join("\n", new[] { cost, keywordLine }.Concat(sentences).Append(sacrifice).Where(line => !string.IsNullOrEmpty(line)));
        }

        /// <summary>The rules text of these effects alone (no keyword line for the card's movement or ability).</summary>
        public static string GetRulesText(IEnumerable<CardEffect> effects, bool isSpell) {
            if (effects == null) return "";
            var lines = effects.Select(e => (!isSpell ? KeywordAbility(e) : null) ?? DescribeEffect(e, isSpell)).Where(l => l != null);
            return string.Join("\n", lines);
        }

        /// <summary>A creature's ability as printed: its name, a link to what it does.</summary>
        public static string AbilityLine(CreatureAbility ability) => ability == CreatureAbility.None ? "" : AbilityName(ability);

        private static string AbilityName(CreatureAbility ability) => Keywords.Link(Abilities.NameOf(ability));

        #region Keyword abilities
        // Effects that keep coming back get a keyword, like Magic's "Lifelink" or Slay the Spire's "Retain".
        // Only an effect that is exactly the pattern becomes the keyword; anything more is written out.
        private static string KeywordAbility(CardEffect effect) {
            if (effect?.actions == null || effect.actions.Count != 1) return null;
            var action = effect.actions[0];
            bool plain = action.targetModifier == TargetModifier.None;
            if (!plain) return null;

            // Revive: its own Last Will raises it at the next Dawn (once per combat)
            if (effect.trigger == EffectTrigger.OnDeath && action.actionType == ActionType.Revive && action.targetType == TargetType.Self) {
                return Keywords.Link("Revive");
            }
            if (effect.oncePerCombat) {
                return effect.trigger == EffectTrigger.OnAboutToDie && action.actionType == ActionType.Heal && action.targetType == TargetType.Self
                    ? Keywords.Link("Rebirth") : null;
            }
            switch (effect.trigger) {
                // Hitting back for its Attack: the effect has no damage of its own (see EffectResolver)
                case EffectTrigger.OnDamage when action.actionType == ActionType.Damage && action.targetType == TargetType.Enemy && action.value <= 0:
                    return Keywords.Link("Counterattack");
                case EffectTrigger.EndOfTurn when action.targetType == TargetType.Self && action.actionType == ActionType.Damage:
                    return $"{Keywords.Link("Decay")} {action.value}";
                case EffectTrigger.EndOfTurn when action.targetType == TargetType.Self && action.actionType == ActionType.Heal:
                    return $"{Keywords.Link("Regenerate")} {action.value}";
                case EffectTrigger.StartOfTurn when action.targetType == TargetType.Self && action.actionType == ActionType.ModifyStat
                                                    && action.modifyAttack && action.modifyHealth && !action.modifySpeed
                                                    && action.value > 0 && action.statusDuration <= 0:
                    return Keywords.Link("Grow") + Count(action.value);
                default:
                    return null;
            }
        }
        #endregion

        #region Sentences
        // Short templated clauses: what it does first, then whom ("3 damage to target", "Doom 3 on all enemy creatures").
        // The same thing done to several groups reads once, for the group they make up ("exhaust all creatures",
        // "1 damage to everything"). A number after a status is its Days ("Doom 3"); a status with a strength shows the
        // Days with the sand watch ("Burn 2 [sand watch]3": 2 damage, 3 Days). The rest lives in the keywords' explanations.
        private static string DescribeEffect(CardEffect effect, bool isSpell) {
            bool atDawn = !isSpell && effect.trigger == EffectTrigger.StartOfTurn;
            var actions = effect.actions.Where(a => DescribeAction(a, isSpell, null, atDawn) != null).ToList();
            if (actions.Count == 0) return null;

            // Retaliation: an enemy-targeted Hurt effect lands on the creature whose attack hit this one (see EffectResolver)
            bool retaliates = !isSpell && effect.trigger == EffectTrigger.OnDamage
                              && actions.All(a => a.targetType == TargetType.Enemy && a.targetModifier == TargetModifier.None);
            (string icon, string text) trigger = isSpell ? default : retaliates ? (EffectIcons.Hurt, Keywords.Link("Retaliate")) : DescribeTrigger(effect);
            string body = JoinClauses(retaliates ? actions.Select(RetaliationClause).ToList() : Clauses(actions, isSpell, atDawn));
            // When Revealed against the enemy: it strikes whoever revealed it
            if (!isSpell && effect.trigger == EffectTrigger.OnRevealed) body = body.Replace("enemy player", "whoever revealed it");
            string once = effect.oncePerCombat ? "Once per combat, " : "";

            if (trigger.text == null) return CapitalizeRich(once + body);
            string lead = trigger.text.StartsWith("<") ? $"{trigger.text}:" : $"{trigger.text},";
            return $"{EffectIcons.Tag(trigger.icon)}{(once.Length > 0 ? once + LowerFirst(lead) : lead)} {body}";
        }

        private const string Whom = "\u0001"; // Where the target goes in a clause, while clauses are merged
        private const string Itself = "itself"; // The creature whose effect it is: "exhaust itself", "1 damage to itself"

        // What Retaliate does to the attacker, its numbers after it: "Retaliate: Burn 2 [sand watch]3", "Retaliate: Exhaust".
        // Only something other than a status names the attacker: "Retaliate: 1 damage to it"
        private static string RetaliationClause(EffectAction action) {
            string clause = DescribeAction(action, false, Whom);
            const string onWhom = " on " + Whom;
            if (clause.EndsWith(onWhom)) clause = clause.Substring(0, clause.Length - onWhom.Length);
            else if (clause.EndsWith(" " + Whom) && action.actionType == ActionType.Stun) clause = clause.Substring(0, clause.Length - Whom.Length - 1);
            return CapitalizeRich(clause.Replace(Whom, "it"));
        }

        // One clause per action, except that the same thing done to groups that together make a bigger one reads once
        // ("exhaust all creatures" for all enemy and all friendly creatures; "everything" for all creatures and both
        // players), and statuses landing on the same creatures share them ("Burn 1 [sand watch]2 and Heat Signature 1 on all creatures")
        private static List<string> Clauses(List<EffectAction> actions, bool isSpell, bool atDawn) {
            var parts = new List<(string what, string whom)>();
            var done = new HashSet<EffectAction>();
            foreach (var action in actions) {
                if (done.Contains(action)) continue;
                string what = DescribeAction(action, isSpell, Whom, atDawn);
                var same = actions.Where(a => !done.Contains(a) && ReachOf(a) != Reach.None && a.delayDays == action.delayDays
                                              && DescribeAction(a, isSpell, Whom, atDawn) == what).ToList();
                string group = what.Contains(Whom) && same.Count > 1 && same.Contains(action) ? GroupOf(same.Select(ReachOf)) : null;
                if (group != null) same.ForEach(a => done.Add(a));
                else done.Add(action);
                // A part that waits says when: "at the next Dawn, 5 damage to it"
                if (action.delayDays > 0) what = (action.delayDays == 1 ? $"at the next {Keywords.Link("Dawn")}, " : $"{action.delayDays} {Keywords.Link("Dawn")}s on, ") + what;
                if (action.onlyIfTargetDies) what = "if it dies, " + what;
                parts.Add((what, group ?? DescribeTarget(action, isSpell)));
            }

            const string onWhom = " on " + Whom;
            var clauses = new List<string>();
            string named = null; // Who the last clause named, so the next one can say "it" or "them"
            for (int i = 0; i < parts.Count; i++) {
                string what = parts[i].what, whom = parts[i].whom;
                while (what.EndsWith(onWhom) && i + 1 < parts.Count && parts[i + 1].whom == whom && parts[i + 1].what.EndsWith(onWhom)) {
                    what = what.Substring(0, what.Length - onWhom.Length) + " and " + parts[++i].what;
                }
                string pronoun = whom == named ? PronounFor(whom) : null;
                bool statFirst = what.StartsWith(Whom + " ");
                if (pronoun != null && statFirst && clauses.Count > 0 && parts[i - 1].what.StartsWith(Whom + " ")) {
                    // "Target +3 Attack and -2 Health": the same creature's stats, one after the other
                    clauses[clauses.Count - 1] += " and " + what.Substring(Whom.Length + 1);
                    continue;
                }
                if (pronoun != null) what = statFirst ? "give " + what : what; // "Cleanse target and give it -1 Attack"
                else if (statFirst && whom.StartsWith(Itself)) what = "give " + what; // "Give itself +1 Attack"
                clauses.Add(what.Replace(Whom, pronoun ?? whom));
                named = whom;
            }
            return clauses;
        }

        // A target named again: "it" for one creature, "them" for a group. Random picks and players are named each time
        private static string PronounFor(string whom) {
            if (whom == "target" || whom.StartsWith("target ") && !whom.Contains(" side")) return "it";
            if (whom == Itself) return Itself;
            return whom.StartsWith("all ") || whom.StartsWith("every ") || whom.Contains("everything") ? "them" : null;
        }

        [System.Flags] private enum Reach { None = 0, FriendlyCreatures = 1, EnemyCreatures = 2, You = 4, EnemyPlayer = 8 }

        // Which whole group an action lands on; None when it's one target (chosen, random, or a creature and its neighbors)
        private static Reach ReachOf(EffectAction action) {
            if (action.targetModifier.HasFlag(TargetModifier.Random) || action.targetModifier.HasFlag(TargetModifier.WithAdjacent)
                || action.targetModifier.HasFlag(TargetModifier.Beside) || action.targetModifier.HasFlag(TargetModifier.AlliesOfTarget) || action.targetModifier.HasFlag(TargetModifier.Strongest) || action.targetModifier.HasFlag(TargetModifier.Opposite)) return Reach.None;
            bool each = action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets);
            return action.targetType switch {
                TargetType.FriendlyCreatures when each => Reach.FriendlyCreatures,
                TargetType.EnemyCreatures when each => Reach.EnemyCreatures,
                TargetType.AllCreatures when each => Reach.FriendlyCreatures | Reach.EnemyCreatures,
                TargetType.Player => Reach.You,
                TargetType.Enemy => Reach.EnemyPlayer,
                _ => Reach.None
            };
        }

        private static string GroupOf(IEnumerable<Reach> reaches) {
            var all = reaches.Aggregate(Reach.None, (sum, reach) => sum | reach);
            const Reach creatures = Reach.FriendlyCreatures | Reach.EnemyCreatures, players = Reach.You | Reach.EnemyPlayer;
            if (all == creatures) return "all creatures";
            if (all == (creatures | players)) return Keywords.Link("Everything", "everything");
            if (all == players) return "both players";
            return null;
        }

        private static (string icon, string text) DescribeTrigger(CardEffect effect) {
            switch (effect.trigger) {
                case EffectTrigger.OnPlay: return (EffectIcons.Enter, Keywords.Link("Arrival"));
                case EffectTrigger.OnDeath: return (EffectIcons.Death, Keywords.Link("Last Will"));
                case EffectTrigger.OnAboutToDie: return (EffectIcons.Death, "When this would die");
                case EffectTrigger.OnDamage:
                    // Chain/spread effects fire when this creature's attack lands; the rest when it is damaged
                    return effect.actions.Any(a => a.targetModifier != TargetModifier.None)
                        ? (EffectIcons.Attack, Keywords.Link("On Hit"))
                        : (EffectIcons.Hurt, Keywords.Link("Hurt"));
                case EffectTrigger.StartOfTurn: return (EffectIcons.Dawn, Keywords.Link("Dawn"));
                case EffectTrigger.EndOfTurn: return (EffectIcons.Dusk, Keywords.Link("Dusk"));
                case EffectTrigger.ActionAttempted: return (EffectIcons.Attack, "Whenever this acts");
                case EffectTrigger.OnRevealed: return (EffectIcons.Reveal, Keywords.Link("When Revealed"));
                case EffectTrigger.OnReplaced: return (EffectIcons.Enter, Keywords.Link("Replaced"));
                case EffectTrigger.OnDiscarded: return (EffectIcons.Days, Keywords.Link("Laid Off"));
                default: return (null, null);
            }
        }

        // One clause; target overrides who it lands on ("it": whoever damaged this creature)
        // Drawn cards wait for the next Day's hand (a hand drawn now would be discarded at Dusk), except at Dawn
        private static string DescribeAction(EffectAction action, bool isSpell, string targetOverride = null, bool atDawn = false) {
            string target = targetOverride ?? DescribeTarget(action, isSpell);
            int value = action.value;

            switch (action.actionType) {
                case ActionType.Damage:
                    if (action.targetModifier.HasFlag(TargetModifier.Chained))
                        return $"{value} damage to its target, chaining to 2 more at half each jump";
                    return $"{value} damage to {target}{Requisite(action)}";
                case ActionType.Heal:
                    return $"heal {value} to {target}";
                case ActionType.Destroy:
                    return $"{Keywords.Link("Destroy", "destroy")} {target}{Requisite(action)}";
                case ActionType.TakeControl:
                    return $"{Keywords.Link("Take Control", "take control")} of {target}{Requisite(action)}";
                case ActionType.Attack:
                    return $"attack {target}";
                case ActionType.Draw:
                    string when = atDawn ? "" : $" next {Keywords.Link("Day")}";
                    string cards = value == 1 ? "a card" : value.ToString();
                    return action.targetType == TargetType.Enemy ? $"enemy draws {cards}{when}" : $"draw {cards}{when}";
                case ActionType.Summon:
                    return Keywords.Link("Summon", "summon" + Count(value));
                case ActionType.SummonDecoys: {
                    string decoy = action.decoy?.cardName ?? action.decoyData?.cardName ?? "decoy";
                    string shownAs = action.decoy?.disguise?.cardName ?? action.decoyData?.disguise?.cardName;
                    string plural = value == 1 ? decoy : decoy + "s";
                    return $"put {value} {Keywords.Link("Decoy", plural)} in your empty {Keywords.Link("Slot", "slots")}" +
                           (shownAs != null ? $": the enemy sees {(value == 1 ? "it" : "each")} as a {shownAs} until it's revealed" : "");
                }
                case ActionType.Armor:
                    return $"{target} +{(value == 1 ? "" : value.ToString())}{Keywords.Link("Armor", EffectIcons.Tag(EffectIcons.Armor))}";
                case ActionType.Stun:
                    return $"{Keywords.Link("Exhausted", "exhaust")} {target}";
                case ActionType.ModifyStat:
                    return $"{target}{Requisite(action)} {DescribeStatChange(action)}";
                case ActionType.ApplyStatus:
                    string status = DescribeStatus(action, target);
                    return status != null ? status + Requisite(action) : null;
                case ActionType.DelayDeployment:
                    return $"+{value} {Keywords.Link("Deployment Time")} to {target}";
                case ActionType.SwapStats: // Geomagnetic Reversal
                    return $"{target}{Requisite(action)}: its Attack and Health trade places";
                case ActionType.Defect: // Taqiyya
                    return "it goes over to the enemy, into the slot opposite (whatever stands there is discarded)";
                case ActionType.Rush: // Overtime
                    return $"{target}{Requisite(action)}: ready up to {value} Days sooner, then {Keywords.Link("Burnout")}: destroyed at the end of its first Day ready";
                // Between two creatures: this one and its target, or (on a spell, or aimed at itself) the target and the creature facing it
                case ActionType.CopyStats:
                    return isSpell || action.targetType == TargetType.Self
                        ? $"{target} copies the Attack, Health and Speed of the creature facing it"
                        : $"copy the Attack, Health and Speed of {target}";
                case ActionType.StealStatChanges:
                    return isSpell || action.targetType == TargetType.Self
                        ? $"{target} steals the stat raises of the creature facing it"
                        : $"steal the stat raises of {target}";
                case ActionType.ReturnToHand:
                    return $"{target} goes back to its owner's hand next {Keywords.Link("Day")}";
                case ActionType.FakeDeath:
                    return $"{target} fakes its death and goes back to your hand next {Keywords.Link("Day")}";
                case ActionType.Vanish:
                    return $"{target} {Keywords.Link("Gone Dark", "goes dark")} until the next {Keywords.Link("Dawn")}";
                case ActionType.SendFor: {
                    string kind = action.sendFor switch { SendForFilter.Creature => "a creature", SendForFilter.Spell => "a spell", _ => "a card" };
                    string where = action.fromDiscard ? "from your discard pile" : value > 0 ? $"among the top {value} of your deck" : "from your deck";
                    return $"{Keywords.Link("Send For", "send for")} {kind} {where}: it joins your next hand";
                }
                case ActionType.Revive:
                    return $"{target} {Keywords.Link("Revive", "rises again")} at the next {Keywords.Link("Dawn")}";
                case ActionType.Grace:
                    return $"{Keywords.Link("Grace")} until the {Keywords.Link("Dusk")} of the next {Keywords.Link("Day")}: at 0 Health you don't lose yet; still at 0 then, you do. If both players fall to 0, you win";
                case ActionType.Resurrect:
                    return $"sacrifice {target}: it {Keywords.Link("Revive", "rises again")} at the {Keywords.Link("Dawn")} {value} Day{(value == 1 ? "" : "s")} later, ready, as strong as it was";
                case ActionType.CutDraws:
                    return $"{target} draws {value} fewer card{(value == 1 ? "" : "s")} with the next hand";
                case ActionType.WipeWatchdogs:
                    // Every Watchdog of theirs, in what they do (the user: no "Watchdog" on cards)
                    return $"end every {(action.targetType == TargetType.Player ? "one of your" : "enemy")} effect{(action.targetType == TargetType.Player ? "s" : "")} still waiting to {Keywords.Link("Disable")} or {Keywords.Link("Redirect")} actions";
                case ActionType.Funnel:
                    return $"every other creature on its side hands its stat raises to {target}";
                case ActionType.Consolidate:
                    return $"{target} takes {value} Attack from each other creature{(action.targetType == TargetType.AllCreatures ? " on the board" : " on its side")}";
                case ActionType.PassHarmfulStatuses:
                    return $"{target} passes its statuses to the creature facing it";
                case ActionType.StealGoodStatuses:
                    return $"{target} takes the statuses of the creature facing it";
                case ActionType.CopyStatuses:
                    return $"{target} copies the statuses of the creature facing it";
                case ActionType.SwapPositions:
                    return $"{target} trades places with the creature to its right";
                case ActionType.GrantAbility:
                    return $"{target} gains {AbilityName(action.ability)}";
                case ActionType.Equip:
                    return $"{Keywords.Link("Gear", "equip")} {target} with {AbilityName(action.ability)}";
                case ActionType.EndDay:
                    return "the Day ends here: every other action still in the queue, both sides', is discarded";
                case ActionType.Earthquake:
                    return $"an {Keywords.Link("Earthquake", "earthquake")}: from a random slot, a zigzag across the board, one hit in every column, the sides taking turns, {value} damage each";
                case ActionType.SetTrend: // "Trend: Rush Hour [sand watch]3" (one word, Trend, everywhere: the user, 2026-10-15)
                    return action.trend == Trend.None
                        ? $"the {Keywords.Link("Trend")} ends"
                        : $"{Keywords.Link("Trend")}: {Keywords.Link(TrendSystem.NameOf(action.trend))}{DaysTag(action.statusDuration)}";
                case ActionType.PlaceSlotEffect:
                    return $"{DescribeSlotEffect(action)} on {SlotTarget(action, isSpell)}";
                case ActionType.CleanseSlot:
                    return action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets)
                        ? $"clear the {Keywords.Link("Slot Effect", "Slot Effects")} on {SlotTarget(action, isSpell)}"
                        : $"clear the {Keywords.Link("Slot Effect")} on {SlotTarget(action, isSpell)}";
                case ActionType.SwapSlotEffects: // Projection (Court Change): each side's Slot Effects go to the other's
                    return $"the {Keywords.Link("Slot Effect", "Slot Effects")} on each side trade places with the other side's";
                case ActionType.CreateWatchdog:
                    return DescribeWatchdog(action, target);
                case ActionType.Reveal: // Instant: it resolves as soon as it's played
                    if (action.targetType is TargetType.Player or TargetType.Enemy) return $"{Keywords.Link("Reveal", "reveal")} which actions {target}'s waiting effects will {Keywords.Link("Disable")} or {Keywords.Link("Redirect")}";
                    return action.targetModifier.HasFlag(TargetModifier.Random)
                        ? $"{Keywords.Link("Reveal", "reveal")} {target}"
                        : $"{Keywords.Link("Reveal", "reveal")} {SlotTarget(action, isSpell)}";
                case ActionType.Cleanse: // A Cleanse removes the bad; a Purge removes everything
                    string what = action.cleanse switch {
                        CleanseTarget.StatModifications => " stat changes on",
                        CleanseTarget.Both => " statuses and stat changes on",
                        _ => ""
                    };
                    return action.purge ? $"{Keywords.Link("Purge", "purge")}{what} {target}" : $"{Keywords.Link("Cleanse", "cleanse")}{what} {target}";
                default:
                    return null;
            }
        }

        // What the target must be for it to land: " if it's Revealed", " with 2 Attack or less"
        private static string Requisite(EffectAction action) => action.requires switch {
            TargetRequisite.Revealed => $" if it's {Keywords.Link("Revealed")}",
            TargetRequisite.NextToEmptySlot => $" if a {Keywords.Link("Slot", "slot")} beside it is empty",
            TargetRequisite.Outnumbered => " if you have more creatures than its side",
            TargetRequisite.AttackAtMost => $" with {action.value} Attack or less",
            TargetRequisite.Deploying => $" if it's still deploying ({Keywords.Link("Deployment Time")})",
            TargetRequisite.Online => " if it's Digital Society",
            TargetRequisite.OutsideHeadquarters => " outside Headquarters",
            _ => ""
        };

        // Plain words: "target" (what it's dropped on, either side), groups ("all enemy creatures"), players and the
        // creature itself. Only "random" and "strongest" are keywords.
        private static string DescribeTarget(EffectAction action, bool isSpell) {
            // "Other": a creature's effect that leaves itself out (Demagogue)
            string target = DescribeTargetGroup(action, isSpell);
            if (!action.targetModifier.HasFlag(TargetModifier.Others)) return target;
            return Regex.Replace(target, @"\b(friendly creature|enemy creature|creature)", "other $1");
        }

        private static string DescribeTargetGroup(EffectAction action, bool isSpell) {
            if (action.targetModifier.HasFlag(TargetModifier.DroppedSide)) return "every creature on target side"; // Either side: the one it's dropped on
            if (action.targetModifier.HasFlag(TargetModifier.Strongest)) {
                string strongest = "the " + Keywords.Link("Strongest", "strongest");
                switch (action.targetType) {
                    case TargetType.EnemyCreatures: return strongest + " enemy creature";
                    case TargetType.FriendlyCreatures: return strongest + " friendly creature";
                    case TargetType.AllCreatures: return strongest + " creature";
                }
            }
            if (action.targetModifier.HasFlag(TargetModifier.Opposite)) return "the creature facing it";
            if (action.targetModifier.HasFlag(TargetModifier.Random)) {
                switch (action.targetType) {
                    case TargetType.EnemyCreatures: return "a " + Keywords.Link("Random", "random") + " enemy creature";
                    case TargetType.FriendlyCreatures: return "a " + Keywords.Link("Random", "random") + " friendly creature";
                    case TargetType.AllCreatures: return "a " + Keywords.Link("Random", "random") + " creature";
                }
            }

            if (action.targetModifier.HasFlag(TargetModifier.Beside)) return "its neighbors";
            if (action.targetModifier.HasFlag(TargetModifier.AlliesOfTarget)) return "the other creatures on its side";
            string adjacent = action.targetModifier.HasFlag(TargetModifier.WithAdjacent) ? " and its neighbors" : "";
            // Spells resolve against whatever they are dropped on unless they hit a whole group
            bool hitsGroup = action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets);
            // What it's dropped on, by what it can land on (the user: say whether it's a creature, a slot or a player). Either
            // side's, unless it only works on your own
            if (isSpell && !hitsGroup && action.targetType is not (TargetType.Player or TargetType.Enemy)) {
                if (EffectResolver.OwnSideOnly(action.actionType)) return "target friendly creature" + adjacent;
                if (EffectResolver.IsSlotAction(action.actionType)) return "target slot" + adjacent;
                bool creatures = EffectResolver.CanAffectCreatures(action.actionType), players = EffectResolver.CanAffectPlayers(action.actionType);
                if (creatures && players) return "target creature or player" + adjacent; // An empty slot: that side's player
                return (creatures ? "target creature" : "target") + adjacent;
            }

            switch (action.targetType) {
                case TargetType.Self: return Itself + adjacent;
                case TargetType.Player: return "you";
                case TargetType.Enemy: return "enemy player";
                case TargetType.FriendlyCreatures: return "all friendly creatures";
                case TargetType.EnemyCreatures: return "all enemy creatures";
                case TargetType.AllCreatures: return "all creatures";
                default: return "target";
            }
        }

        // "+1 Attack", "+4 Attack [sand watch]1" for a Day; a Health decrease is Health Loss, so it's a link to that rule
        private static string DescribeStatChange(EffectAction action) {
            string sign = action.value >= 0 ? "+" : "";
            var stats = new List<string>();
            if (action.modifyAttack) stats.Add("Attack");
            if (action.modifyHealth) stats.Add("Health");
            if (action.modifySpeed) stats.Add("Speed");
            if (stats.Count == 0) return "no change";
            string change = $"{sign}{action.value} {string.Join("/", stats)}";
            if (action.value < 0 && action.modifyHealth) change = Keywords.Link("Health Loss", change);
            return change + DaysTag(action.statusDuration);
        }

        // A status by its keyword: the number is its Days ("Doom 3"), or its strength and then its Days ("Burn 2 [sand watch]3").
        // A 1 goes without saying ("Doom", "Bless"), except Burn's, which is damage
        private static string DescribeStatus(EffectAction action, string target) {
            int days = action.statusDuration;
            int potency = System.Math.Max(1, action.statusPotency);
            switch (action.statusEffectToApply) {
                case StatusEffectType.Exhausted: return $"{Keywords.Link("Exhausted", "exhaust")} {target}";
                case StatusEffectType.Burned: return $"{Keywords.Link("Burned", $"Burn {potency}")}{DaysTag(days)} on {target}";
                case StatusEffectType.Provoked: return $"{Keywords.Link("Provoked", "Provoke" + Count(days))} on {target}";
                case StatusEffectType.Doomed: return $"{Keywords.Link("Doomed", "Doom" + Count(days))} on {target}";
                case StatusEffectType.Blessed: return $"{Keywords.Link("Blessed", "Bless" + Count(potency))} on {target}";
                case StatusEffectType.HeatSignature: return $"{Keywords.Link("Heat Signature", "Heat Signature" + Count(potency))} on {target}";
                case StatusEffectType.Concealed: return $"{Keywords.Link("Concealed", "Conceal" + Count(days))} on {target}";
                case StatusEffectType.Revealed: return $"{Keywords.Link("Revealed", "Revealed" + Count(days))} on {target}";
                case StatusEffectType.Encrypted: return $"{Keywords.Link("Encrypted", "Encrypt" + Count(days))} on {target}";
                case StatusEffectType.Untouchable: return $"{Keywords.Link("Untouchable", "Untouchable" + Count(days))} on {target}";
                case StatusEffectType.Shielded: return $"{Keywords.Link("Shield", "Shield" + Count(days))} on {target}";
                case StatusEffectType.Cooldown: return $"{Keywords.Link("Cooldown", "Cooldown" + Count(days))} on {target}";
                case StatusEffectType.Brainwashed: return $"{Keywords.Link("Brainwashed", "Brainwashed" + Count(days))} on {target}";
                case StatusEffectType.KillOrder: return $"{Keywords.Link("Kill Order", "Kill Order" + Count(days))} on {target}";
                case StatusEffectType.Burnout: return $"{Keywords.Link("Burnout", "Burnout" + Count(days))} on {target}";
                case StatusEffectType.BorrowedTime: return $"{Keywords.Link("Borrowed Time", "Borrowed Time" + Count(days))} on {target}";
                case StatusEffectType.Licensed: return $"{Keywords.Link("Licensed", "license")} {target}";
                case StatusEffectType.Composed: return $"{Keywords.Link("Composed", "Composed" + Count(days))} on {target}";
                case StatusEffectType.Blackout: return $"{Keywords.Link("Blackout", "Blackout" + Count(days))} on {target}";
                case StatusEffectType.Survivor: return $"{Keywords.Link("Survivor")} on {target}";
                default: return null;
            }
        }

        // A keyword's count after it, unless it's 1: "Summon", "Summon 2"
        private static string Count(int number) => number == 1 ? "" : $" {number}";

        // " [sand watch]3": how many Days a timed change lasts. Nothing for a lasting one
        private static string DaysTag(int days) => days > 0 ? " " + Keywords.Link("Days", $"{EffectIcons.Tag(EffectIcons.Days)}{days}") : "";

        // A Watchdog in plain words, without naming it (the user, 2026-10-17: the player doesn't need to know how the game works
        // inside, and no "->"): "Disable the next 2 enemy attacks", "Redirect the first enemy attack this Day". It catches
        // actions as they come up; a Briefing one picks from the whole Day's plans, from the Day it's set
        private static string DescribeWatchdog(EffectAction action, string target) {
            var spec = action.watchdog;
            int charges = System.Math.Max(1, spec.charges);
            string whose = spec.side switch { WatchdogSide.Enemy => "enemy ", WatchdogSide.Friendly => "friendly ", _ => "" };
            (string one, string many) = spec.watches switch {
                WatchedAction.Attack => ("attack", "attacks"), WatchedAction.Spell => ("spell", "spells"), WatchedAction.Move => ("move", "moves"),
                WatchedAction.Ability => ("ability", "abilities"), WatchedAction.Summon => ("summon", "summons"),
                WatchedAction.Intelligence => (Keywords.Link("Reveal", "reveal"), Keywords.Link("Reveal", "reveals")), _ => ("status", "statuses")
            };
            bool watchesCreature = action.targetType is not (TargetType.Player or TargetType.Enemy);
            bool briefing = spec.timing == WatchdogTiming.Briefing;
            bool plural = !briefing && charges > 1 || briefing && spec.pick == BriefingPick.Every;
            string what = $"{whose}{(plural ? many : one)}";
            if (spec.atMost > 0) what += $" of {spec.atMost} or less";
            if (watchesCreature) what += $" by {target}";
            if (spec.damaging) what += plural ? " that deal damage" : " that deals damage";
            if (spec.effect == WatchdogEffect.Backfire) what += plural ? " to their attackers" : " to its attacker";
            // A Briefing one acts once a Day: "each Day" only when it can act more than once
            string eachDay = charges > 1 ? $" each {Keywords.Link("Day")}" : "";
            string thing = !briefing ? (charges > 1 ? $"the next {charges} {what}" : $"the next {what}")
                : spec.pick switch {
                    BriefingPick.Strongest => $"the strongest {what}{eachDay}",
                    BriefingPick.Every => $"every {what}{eachDay}",
                    _ => $"the first {what}{eachDay}"
                };
            string sentence = spec.effect switch {
                WatchdogEffect.Nullify => $"{Keywords.Link("Disable")} {thing}",
                WatchdogEffect.Redirect or WatchdogEffect.Backfire => $"{Keywords.Link("Redirect")} {thing}",
                WatchdogEffect.Reveal => $"{Keywords.Link("Reveal")} {thing}",
                _ => $"{Keywords.Link("Reorder", "move")} {thing} to the {(spec.moveEarlier ? "start" : "end")} of the {Keywords.Link("Day")}"
            };
            // How often a Briefing one acts, once a Day: "2 times"; Every: as many as it has left
            if (briefing && charges > 1) sentence += spec.pick == BriefingPick.Every ? $", {charges} in all" : $", {charges} times";
            return sentence + (action.statusDuration == 1 ? $" this {Keywords.Link("Day")}" : DaysTag(action.statusDuration));
        }

        // Slot Effects aim at the slot, whoever stands in it
        private static string SlotTarget(EffectAction action, bool isSpell) {
            if (action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets)) { // A side hazard: every slot of a side
                return action.targetType switch {
                    TargetType.EnemyCreatures or TargetType.Enemy => $"every enemy {Keywords.Link("Slot", "slot")}",
                    TargetType.AllCreatures => $"every {Keywords.Link("Slot", "slot")}",
                    _ => $"every one of your {Keywords.Link("Slot", "slots")}"
                };
            }
            return isSpell || action.targetType != TargetType.Self ? $"target {Keywords.Link("Slot", "slot")}" : $"its {Keywords.Link("Slot", "slot")}";
        }

        // "Landmine 3" (its damage, always shown), "Shield 2" (its Days; just "Shield" for 1)
        private static string DescribeSlotEffect(EffectAction action) {
            string days = action.statusDuration > 0 ? Count(action.statusDuration) : "";
            return action.slotEffect switch {
                SlotEffectType.Landmine => Keywords.Link("Landmine", $"Landmine {action.value}"),
                SlotEffectType.BarbedSnare => Keywords.Link("Barbed Snare", $"Barbed Snare {action.value}"),
                SlotEffectType.Shield => Keywords.Link("Shield", $"Shield{days}"),
                SlotEffectType.ScorchedEarth => Keywords.Link("Scorched Earth") + DaysTag(action.statusDuration),
                SlotEffectType.Checkpoint => Keywords.Link("Checkpoint", $"Checkpoint {action.value}") + DaysTag(action.statusDuration),
                _ => "nothing"
            };
        }

        public static string SlotEffectIcon(SlotEffectType type) => type switch {
            SlotEffectType.Landmine => EffectIcons.Damage,
            SlotEffectType.BarbedSnare => EffectIcons.Hurt,
            SlotEffectType.Shield => EffectIcons.Armor,
            SlotEffectType.ScorchedEarth => EffectIcons.Burn,
            SlotEffectType.Checkpoint => EffectIcons.Hurt,
            _ => null
        };
        #endregion

        private static string JoinClauses(IList<string> clauses) {
            if (clauses.Count == 1) return clauses[0];
            return string.Join(", ", clauses.Take(clauses.Count - 1)) + " and " + clauses[clauses.Count - 1];
        }


        // Upper-cases the first letter that is read, skipping rich-text tags and icons in front of it
        private static string CapitalizeRich(string text) {
            bool inTag = false;
            for (int i = 0; i < (text?.Length ?? 0); i++) {
                char c = text[i];
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag && !char.IsWhiteSpace(c)) {
                    // "3 damage to target" starts with its number: nothing to capitalize
                    return char.IsLetter(c) ? text.Substring(0, i) + char.ToUpper(c) + text.Substring(i + 1) : text;
                }
            }
            return text;
        }

        private static string LowerFirst(string text) => text.StartsWith("<") ? text : char.ToLower(text[0]) + text.Substring(1);
    }
}
