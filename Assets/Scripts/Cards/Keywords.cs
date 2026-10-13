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
            Add("Day", "One turn: both sides plan, then everything resolves in order (moves, summons, spells, then abilities and attacks). It runs from [Dawn] to [Dusk].");
            Add("Dawn", "The start of a [Day], before anyone plans.");
            Add("Dusk", "The end of a [Day], after everything has resolved.");
            Add("Days", "How long it lasts, counting the [Day] it lands: 1 is this Day only, 2 is this Day and the next.");

            // ---------------------------------------------------------------- Targets
            Add("Target", "What you drop the card on: a creature or a slot, on either side.");
            Add("All", "Every creature on the board, both sides.");
            Add("All friendly", "Every creature on your side.");
            Add("All enemy", "Every creature on the enemy's side.");
            Add("Random", "One creature picked at random, either side.");
            Add("Random friendly", "One of your creatures, picked at random.");
            Add("Random enemy", "One enemy creature, picked at random.");

            // ---------------------------------------------------------------- Triggers
            Add("Arrival", "Happens when this creature lands: once its summon finishes and its [Deployment Time] is over.");
            Add("Last Will", "Happens when this creature dies.");
            Add("Hurt", "Happens each time this creature takes damage.");
            Add("On Hit", "Happens when this creature's attack lands, on top of the attack.");

            // ---------------------------------------------------------------- Keyword abilities (recurring patterns)
            Add("Retaliate", "When a creature damages this one, it strikes back: Retaliate X deals X damage to that creature. Retaliate: [Exhausted|exhaust] exhausts it instead.");
            Add("Decay", "Decay X: takes X damage at each [Dusk].");
            Add("Regenerate", "Regenerate X: heals X at each [Dusk].");
            Add("Grow", "Grow X: +X Attack and +X Health for good at each [Dawn].");
            Add("Second Wind", "Once per combat, when this would die, it survives instead.");
            Add("Summon", "Summon X: X random creatures from your deck enter your empty slots.");

            // ---------------------------------------------------------------- Movement
            Add("Leapfrog", "Moves by jumping over creatures to an empty slot.");
            Add("Swap", "Trades places with a friendly creature, however far.");

            // ---------------------------------------------------------------- Creature stats and states
            Add("Armor", "Absorbs damage before Health. Fades by 1 each [Dusk].");
            Add("Health Loss", "Lowers Health directly. It isn't damage, so [Armor] doesn't stop it.");
            Add("Deployment Time", "The [Days] a creature needs to get ready after it's summoned. Until then it can't act and its ability is off.");
            Add("Exposed", "Takes +1 damage from each hit.");

            // ---------------------------------------------------------------- Statuses
            Add("Exhausted", "Can't act, its ability is off and it's [Exposed]. Its planned action is cancelled. Lasts the rest of the [Day] and all of the next.");
            Add("Burned", "Burn X: takes X damage at each [Dusk] for the [Days] shown, starting the Dusk after it lands.");
            Add("Doomed", "Doom X: dies in X [Days], with a last attack ([Last Rites]). Meanwhile it gets +1 Attack each [Dawn] and no other status can land on it.");
            Add("Last Rites", "A [Doomed] creature's final attack on the slot opposite it, even if it can't act.");
            Add("Blessed", "Bless X: the next X statuses that would land on it are stopped, good or bad.");
            Add("Heat", "Heat X: random and redirected attacks go to the creature with the most Heat first.");
            Add("Concealed", "Conceal X: for X [Days], its plans show as ??? to the enemy and their [Watchdog|Watchdogs] can't see them.");
            Add("Revealed", "Revealed X: for X [Days], it can't be [Concealed]: its plans can be seen.");
            Add("Encrypted", "Encrypt X: for X [Days], [Watchdog|Watchdogs] ignore its actions.");
            Add("Cleanse", "Removes harmful statuses.");
            Add("Purge", "Removes every status, good and bad.");

            // ---------------------------------------------------------------- Intelligence
            Add("Reveal", "Shows you everything about a slot: its hidden [Slot Effect], the creature there and what it plans. Instant.");
            Add("Watchdog", "A trap in the action queue: when an action it watches comes up, it acts on it. One charge per action.");
            Add("Monitored", "The [Watchdog] checks every action right before it resolves.");
            Add("Briefing", "The [Watchdog] checks the whole queue once, at the start of each [Day] after the one it was set.");
            Add("Nullify", "The action is cancelled.");
            Add("Redirect", "The action hits another creature, picked at random.");
            Add("Reorder", "The action moves to the start or the end of its turn in the queue.");

            // ---------------------------------------------------------------- Slot Effects
            Add("Slot Effect", "A condition on a slot, not on the creature: it stays with the slot. One per slot.");
            Add("Landmine", "Landmine X: hidden. The next creature to enter the slot takes X damage.");
            Add("Barbed Snare", "Barbed Snare X: hidden. The next creature to leave the slot takes X damage.");
            Add("Temporal Shield", "Temporal Shield X: for X [Days], the creature in the slot takes no combat damage.");
            Add("Scorched Earth", "The creature in the slot can't use its ability or trigger its effects. It can still move and attack.");

            // ---------------------------------------------------------------- Creature abilities (Abilities Compendium)
            foreach (CreatureAbility ability in System.Enum.GetValues(typeof(CreatureAbility))) {
                if (ability == CreatureAbility.None) continue;
                Add(Abilities.NameOf(ability), AbilityText(ability));
            }
        }

        // An ability's reminder text, with the keywords it mentions as links
        private static string AbilityText(CreatureAbility ability) => ability switch {
            CreatureAbility.Unpatchable => "Enemy effects can't lower its stats, and [Zero-Day] can't get around it.",
            CreatureAbility.Underground => "In combat it ignores the other creature's stat changes.",
            CreatureAbility.ZeroDay => "Its attacks and effects ignore defensive abilities, except [Cold Storage].",
            CreatureAbility.Copypasta => "When it's ready and each [Dawn] after, it copies the ability of the creature opposite (once).",
            CreatureAbility.Undercover => "It's always [Concealed] until [Revealed].",
            CreatureAbility.HiveMind => "A creature that attacks it gets Hive Mind instead of its own ability.",
            CreatureAbility.IdentityTheft => "Instead of attacking or moving: swap abilities with target creature (hold Shift as you drag it).",
            CreatureAbility.InsiderJob => "Its damage ignores [Armor].",
            CreatureAbility.SignalJammer => "The enemy creature opposite has its ability switched off.",
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
