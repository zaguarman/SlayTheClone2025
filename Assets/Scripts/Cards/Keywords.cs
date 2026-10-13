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
            public string Group; // The glossary section it's listed under
        }

        private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private static string group; // The section the entries being added belong to

        private static void Add(string key, string text, string title = null) =>
            entries[key] = new Entry { Key = key, Title = title ?? key, Text = text, Group = group };

        static Keywords() {
            // ---------------------------------------------------------------- Time
            group = "Time";
            Add("Day", "One turn: both sides plan, then everything resolves in order (moves, summons, spells, then abilities and attacks). It runs from [Dawn] to [Dusk].");
            Add("Dawn", "The start of a [Day], before anyone plans.");
            Add("Dusk", "The end of a [Day], after everything has resolved.");
            Add("Days", "How long it lasts, counting the [Day] it lands: 1 is this Day only, 2 is this Day and the next.");
            Add("Deployment Time", "The [Days] a creature needs to get ready after it's summoned. Until then it can't act and its ability is off.");

            // ---------------------------------------------------------------- Targets
            group = "Targets";
            Add("Slot", "One of the five places on each side of the table where a creature stands.");
            Add("Random", "Picked by chance when the effect happens, among the creatures it can affect at that moment. The one with the most [Heat Signature] is picked first, if any has it. No creature to pick: nothing happens.");
            Add("Everything", "Every creature on both sides, and both players.");

            // ---------------------------------------------------------------- Triggers
            group = "Triggers";
            Add("Arrival", "Happens when this creature lands: once its summon finishes and its [Deployment Time] is over.");
            Add("Last Will", "Happens when this creature dies.");
            Add("Hurt", "Happens each time this creature takes damage.");
            Add("On Hit", "Happens when this creature's attack lands, on top of the attack.");

            // ---------------------------------------------------------------- Keyword abilities (recurring patterns)
            group = "Keyword Abilities";
            Add("Counterattack", "When a creature attacks this one, this one hits it back for its own Attack. Hitting back isn't an attack, so it's never answered.");
            Add("Retaliate", "Retaliate followed by a status, like [Exhausted|exhaust] or [Burned|Burn] 2: when a creature attacks this one, that status lands on the attacker, with the numbers shown.");
            Add("Decay", "Decay X: takes X damage at each [Dusk].");
            Add("Regenerate", "Regenerate X: heals X at each [Dusk].");
            Add("Grow", "Grow X (no number: 1): +X Attack and +X Health for good at each [Dawn].");
            Add("Rebirth", "Once per combat, when this would die, it's reborn instead: it survives with 1 Health. The wheel turns: last life a worm, this life a dog.");
            Add("Summon", "Summon X (no number: 1): X random creatures from your deck enter your empty slots.");

            // ---------------------------------------------------------------- Movement
            group = "Movement";
            Add("Leapfrog", "Moves by jumping over creatures to an empty slot.");
            Add("Swap", "Trades places with a friendly creature, however far.");

            // ---------------------------------------------------------------- Creature stats and states
            group = "Stats";
            Add("Armor", "Armor X (no number: 1): absorbs X damage before Health. Fades by 1 each [Dusk].");
            Add("Health Loss", "Lowers Health directly. It isn't damage, so [Armor] doesn't stop it.");
            Add("Exposed", "Takes +1 damage from each hit.");

            // ---------------------------------------------------------------- Statuses
            group = "Statuses";
            Add("Exhausted", "Can't act, its ability is off and it's [Exposed]. Its planned action is cancelled. Lasts the rest of the [Day] and all of the next.");
            Add("Burned", "Burn X: takes X damage at each [Dusk] for the [Days] shown, starting the Dusk after it lands.");
            Add("Doomed", "Doom X (no number: 1): dies in X [Days], with a last attack ([Last Rites]). Meanwhile it gets +1 Attack each [Dawn] and no other status can land on it.");
            Add("Last Rites", "A [Doomed] creature's final attack on the slot opposite it, even if it can't act.");
            Add("Blessed", "Bless X (no number: 1): the next X statuses that would land on it are stopped, good or bad.");
            Add("Heat Signature", "Heat Signature X (no number: 1): [Random|random] effects and redirected attacks go to the creature with the most Heat Signature. It stays until cleansed.");
            Add("Concealed", "Conceal X (no number: 1): for X [Days], its plans show as ??? to the enemy and their [Watchdog|Watchdogs] can't see them.");
            Add("Revealed", "Revealed X (no number: 1): for X [Days], it can't be [Concealed]: its plans can be seen.");
            Add("Encrypted", "Encrypt X (no number: 1): for X [Days], its actions slip past every [Watchdog], the enemy's and yours: none reacts to them, and none spends a charge on them. Unlike [Concealed], everyone still sees what it plans.");
            Add("Untouchable", "Untouchable X (no number: 1): takes no damage at all for X [Days], counting the Day it lands: 1 is this Day only.");
            Add("Cooldown", "Cooldown X (no number: 1): its Active ability rests and can't be used for the next X [Days].");
            Add("Cleanse", "Removes harmful statuses.");
            Add("Purge", "Removes every status, good and bad.");

            // ---------------------------------------------------------------- Intelligence
            group = "Intelligence";
            Add("Reveal", "Shows you everything about a slot: its hidden [Slot Effect], the creature there and what it plans. Instant.");
            Add("Watchdog", "A trap in the action queue: when an action it watches comes up, it acts on it. One charge per action.");
            Add("Monitored", "The [Watchdog] checks every action right before it resolves.");
            Add("Briefing", "Each [Day], once everyone has planned and before anything resolves, the [Watchdog] reads the whole queue and acts on what it watches. It starts the Day after it's set.");
            Add("Nullify", "The action is cancelled.");
            Add("Redirect", "The action hits another creature, picked at [Random|random].");
            Add("Reorder", "The action moves to the start or the end of its turn in the queue.");

            // ---------------------------------------------------------------- Slot Effects
            group = "Slot Effects";
            Add("Slot Effect", "A condition on a [Slot|slot], not on the creature: it stays with the slot. One per slot.");
            Add("Landmine", "Landmine X: hidden. The next creature to enter the slot takes X damage.");
            Add("Barbed Snare", "Barbed Snare X: hidden. The next creature to leave the slot takes X damage.");
            Add("Temporal Shield", "Temporal Shield X (no number: 1): for X [Days], the creature in the slot takes no combat damage.");
            Add("Scorched Earth", "The creature in the slot can't use its ability or trigger its effects. Its [Arrival] effects wait until it's off the slot. It can still move and attack.");

            // ---------------------------------------------------------------- Trends
            group = "Trends";
            Add("Trend", "What the world is into right now: a rule for the whole board, for the [Days] shown (no number: until another Trend replaces it). One at a time: a new Trend replaces the old. A Trend card plays on either side of the table.");
            foreach (Trend trend in System.Enum.GetValues(typeof(Trend))) {
                if (trend == Trend.None) continue;
                Add(TrendSystem.NameOf(trend), $"A [Trend]: {TrendSystem.RuleOf(trend)}." + (trend == Trend.RushHour ? " Lower Speed goes first, wherever Speed decides the order." : ""));
            }

            // ---------------------------------------------------------------- Creature abilities (Abilities Compendium)
            group = "Creature Abilities";
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
            CreatureAbility.HiveMind => "A creature that attacks it loses its ability and gains Hive Mind instead.",
            CreatureAbility.IdentityTheft => "Instead of attacking or moving: swap abilities with target creature (hold Shift as you drag it).",
            CreatureAbility.InsiderJob => "Its damage ignores [Armor].",
            CreatureAbility.SignalJammer => "The enemy creature opposite has its ability switched off.",
            CreatureAbility.Bodyguard => "Can't attack. Instead, drag it onto another friendly creature: that one is [Untouchable] this Day (it lands before the attacks, with the spells). Then a Day of [Cooldown].",
            CreatureAbility.Wannabe => "Instead of attacking or moving: its Attack, Health and Speed become those of target creature, for as long as it stays in play (hold Shift as you drag it).",
            CreatureAbility.CreditGrab => "Once per combat, instead of attacking or moving: steals target creature's stat raises, timed, lasting and for good. Its lowered stats stay (hold Shift as you drag it).",
            CreatureAbility.SprayAndPray => "Its attacks also hit the creatures on both sides of its target, each for its Attack: three slots in a cone.",
            CreatureAbility.ChainMail => "Its attacks jump on to the next 2 creatures in line, toward the side with more of them, at half the damage each jump.",
            CreatureAbility.DoubleTap => "It attacks twice: a second hit on the same target right after the first.",
            CreatureAbility.ShellGame => "Instead of attacking or moving: target creature trades places with the one to its right, or to its left at the end of the row (hold Shift as you drag it).",
            CreatureAbility.SafeSpace => "Each [Dawn], it and the friendly creatures beside it gain [Blessed|Bless]: a ward that stops the next status.",
            _ => Capitalize(Abilities.ReminderOf(ability)) + "."
        };

        public static bool Exists(string key) => key != null && entries.ContainsKey(key);
        public static IEnumerable<Entry> All => entries.Values;
        /// <summary>The glossary sections, in the order they were written.</summary>
        public static List<string> Groups => entries.Values.Select(e => e.Group).Distinct().ToList();
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
