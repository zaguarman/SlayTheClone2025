using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The keyword glossary, like Magic's keyword abilities or Slay the Spire's keyword tooltips: short words on the cards
    /// (Retaliate 1, Dusk, Exhausted, Watchdog...) whose full meaning lives here. Card text marks each keyword as a link
    /// (<see cref="Link"/>); hovering it on an enlarged card opens its explanation, whose own keywords are links too, so
    /// the player can keep following them (see <see cref="Tooltip"/>).
    /// Descriptions write other keywords as [Key] or [Key|shown text]; <see cref="Describe"/> turns them into links.
    /// </summary>
    public static class Keywords {
        public const string LinkColor = "#8a4b00";

        public class Entry {
            public string Key;
            public string Title;
            public string Text; // With [Key] references
        }

        private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        private static void Add(string key, string text, string title = null) =>
            entries[key] = new Entry { Key = key, Title = title ?? key, Text = text };

        static Keywords() {
            // ---------------------------------------------------------------- Time
            Add("Day", "One turn. Both sides plan their actions, then everything resolves in order: Moves, Summons, Spells, then Abilities & Attacks. It starts at [Dawn] and ends at [Dusk].");
            Add("Dawn", "The start of a [Day], before anyone plans. Dawn effects of creatures in play happen then.");
            Add("Dusk", "The end of a [Day], after everything resolved. Dusk effects happen; then [Deployment Time] counts down, [Armor] fades by 1 and timed statuses tick.");

            // ---------------------------------------------------------------- Triggers
            Add("Arrival", "Happens when this creature enters play: once its summon finishes and its [Deployment Time] is over. A creature on [Scorched Earth] loses it.");
            Add("Last Will", "Happens when this creature dies (killed, not discarded or returned to the hand).");
            Add("When Hurt", "Happens each time this creature takes damage, even if [Armor] absorbs it or the hit kills it. [Health Loss] isn't damage.");
            Add("On Hit", "Happens when this creature's attack lands, on top of the attack itself.");

            // ---------------------------------------------------------------- Keyword abilities (recurring patterns)
            Add("Retaliate", "When a creature damages this one, it strikes back at that creature: Retaliate X deals X damage to it; Retaliate: [Exhausted] exhausts it. If a spell damaged it, damage goes to the enemy player instead. Retaliation still happens if the hit kills it. A [When Hurt] effect.");
            Add("Decay", "Decay X: at each [Dusk] this creature takes X damage. Its upkeep, side effects or bad habits.");
            Add("Regenerate", "Regenerate X: at each [Dusk] this creature heals X, up to its max Health.");
            Add("Grow", "Grow X: at each [Dawn] this creature gets +X Attack and +X Health for good.");
            Add("Second Wind", "Once per combat, when this creature would die, it heals and survives instead. The use is gone until the combat ends, even if the card is reshuffled.");
            Add("Summon", "A random creature from your deck enters an empty slot of yours, with its own [Deployment Time].");

            // ---------------------------------------------------------------- Movement
            Add("Leapfrog", "Moves by jumping over creatures to an empty slot. If its landing slot is taken, it stops at the closest empty slot on the way.");
            Add("Swap", "Trades places with a friendly creature at any distance, over the creatures in between, even one that can't act.");

            // ---------------------------------------------------------------- Creature stats and states
            Add("Armor", "Absorbs damage before Health. Capped at twice the creature's max Health, it fades by 1 each [Dusk] and is lost when the creature leaves play. [Health Loss] ignores it.");
            Add("Health Loss", "Lowers Health directly: it ignores [Armor] and isn't damage, so [When Hurt] effects don't happen. At 0 Health the creature dies.");
            Add("Deployment Time", "The Days a summoned creature needs to get ready. Until then it can't act, its ability is off and its [Arrival] effect waits. It counts down at [Dusk], not at the Dusk of the Day it arrived.");
            Add("Exposed", "Takes +1 damage from each hit.");

            // ---------------------------------------------------------------- Statuses
            Add("Exhausted", "Can't act, its ability is switched off and it's [Exposed]. Its planned action is cancelled. Lasts the rest of the [Day] and all of the next; exhausting it again refreshes it.");
            Add("Burned", "Takes damage at each [Dusk] while it lasts. It does nothing at the Dusk of the Day it's applied.");
            Add("Doomed", "A death sentence. It wipes and blocks other statuses and gives +1 Attack for good each [Dawn]. When it runs out, its [Last Rites] strike and the creature dies.");
            Add("Last Rites", "A [Doomed] creature's final attack on the slot opposite it, even if it can't act. Then it dies for real.");
            Add("Blessed", "Each stack negates the next status that would land on this creature, good or bad, [Doomed] included. Up to 3 stacks; it lasts until used.");
            Add("Heat", "Random and redirected attacks go to the creature with the most Heat first. Heat stays until cleansed.");
            Add("Concealed", "Its planned actions are hidden from the other side (they show as ???), and [Watchdog|Watchdogs] can't match them. [Revealed] cancels it.");
            Add("Revealed", "Cancels [Concealed] and [Undercover]: its planned actions can be seen and [Watchdog|Watchdogs] can match them.");
            Add("Encrypted", "[Watchdog|Watchdogs] ignore its actions and spend no charges on them.");
            Add("Cleanse", "Removes harmful statuses and stat decreases. A [Purge] removes the good ones too.");
            Add("Purge", "Removes every status and temporary stat change, good and bad. Changes made for good stay.");

            // ---------------------------------------------------------------- Intelligence
            Add("Reveal", "Instantly shows its caster a slot's hidden [Slot Effect] and what the creature there plans this [Day]; aimed at a player, their [Watchdog] conditions. Information is instant: it resolves as it's played.");
            Add("Watchdog", "A trap in the action queue. When an action it watches comes up it can [Nullify] it, [Redirect] it, [Reorder] it or reveal it, one charge per action. It looks [On Watch] or at the [Briefing]. It can't see [Concealed] actions and ignores [Encrypted] creatures.");
            Add("On Watch", "The [Watchdog] looks at each action right before it resolves, even ones planned before it existed.");
            Add("Briefing", "From the [Day] after it's set, the [Watchdog] looks at the whole queue once, before anything resolves.");
            Add("Nullify", "The action is cancelled.");
            Add("Redirect", "The action is aimed at another creature at random, never the one acting or the one it was aimed at. [Heat] draws it first.");
            Add("Reorder", "The action moves to the start or the end of its layer.");

            // ---------------------------------------------------------------- Slot Effects
            Add("Slot Effect", "A condition on a battlefield slot, not on the creature: it stays with the slot. One per slot; a new one replaces the old. Hidden ones are known only to their controller until they trigger or are [Reveal|revealed].");
            Add("Landmine", "A hidden [Slot Effect]: the first creature to enter the slot, summoned or moved in, takes its damage. It goes off before anything else, then it's gone.");
            Add("Barbed Snare", "A hidden [Slot Effect]: the first creature to leave the slot, moving, pushed or swapped, takes its damage. Dying or being replaced isn't leaving.");
            Add("Temporal Shield", "A [Slot Effect]: the creature in the slot takes no combat damage. Spells and abilities still hurt it.");
            Add("Scorched Earth", "A [Slot Effect]: the creature in the slot can't trigger its effects, [Arrival] included (that one is lost). Its ability still works.");

            // ---------------------------------------------------------------- Creature abilities (Abilities Compendium)
            foreach (CreatureAbility ability in System.Enum.GetValues(typeof(CreatureAbility))) {
                if (ability == CreatureAbility.None) continue;
                Add(Abilities.NameOf(ability), AbilityText(ability));
            }
        }

        // An ability's reminder text, with the keywords it mentions as links
        private static string AbilityText(CreatureAbility ability) => ability switch {
            CreatureAbility.Unpatchable => "Enemy effects can't lower its stats, and [Zero-Day] can't get around it.",
            CreatureAbility.Underground => "In combat it ignores the other creature's stat changes, both ways: when it's attacked, the attacker's buffs and debuffs; when it attacks, the defender's temporary Health changes.",
            CreatureAbility.ZeroDay => "Its attacks and effects ignore the target's defensive abilities ([Unbothered], [No U], [Return to Sender]...), except [Unpatchable].",
            CreatureAbility.Copypasta => "When it's ready, and each [Dawn] after that, it copies the ability of the creature opposite it (and loses Copypasta).",
            CreatureAbility.Undercover => "Everything it plans is hidden from the other side, like [Concealed], and it stays ??? to them until [Revealed].",
            CreatureAbility.HiveMind => "A creature that attacks it gains Hive Mind, replacing its own ability.",
            CreatureAbility.IdentityTheft => "Active: instead of attacking or moving, swap abilities with target creature (hold Shift as you drag it).",
            CreatureAbility.InsiderJob => "Its damage ignores [Armor].",
            CreatureAbility.SignalJammer => "The enemy creature directly opposite has its ability switched off. Its triggered effects still work.",
            _ => Capitalize(Abilities.ReminderOf(ability)) + "."
        };

        public static bool Exists(string key) => key != null && entries.ContainsKey(key);
        public static IEnumerable<Entry> All => entries.Values;
        public static Entry Get(string key) => Exists(key) ? entries[key] : null;

        /// <summary>A keyword as printed on a card: bold and colored, and a link its tooltip can find.</summary>
        public static string Link(string key, string shown = null) =>
            Exists(key) ? $"<link=\"{key}\"><b><color={LinkColor}>{shown ?? key}</color></b></link>" : shown ?? key;

        /// <summary>The keyword's explanation, with the keywords it mentions as links.</summary>
        public static string Describe(string key) {
            var entry = Get(key);
            if (entry == null) return "";
            return Regex.Replace(entry.Text, @"\[([^\]|]+)(?:\|([^\]]+))?\]", m => Link(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null));
        }

        /// <summary>The keywords a text links to, in order.</summary>
        public static List<string> LinksIn(string richText) =>
            Regex.Matches(richText ?? "", "<link=\"([^\"]+)\">").Cast<Match>().Select(m => m.Groups[1].Value).ToList();

        /// <summary>The text without its rich-text tags, as it reads.</summary>
        public static string Plain(string richText) => Regex.Replace(richText ?? "", "<[^>]+>", "");

        private static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);
    }
}
