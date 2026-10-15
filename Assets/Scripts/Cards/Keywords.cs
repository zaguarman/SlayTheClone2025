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
            Add("Deployment Time", "The [Days] a creature needs to get ready after it's summoned. Until then it can't act and its ability is off. Adding to it only slows a creature still deploying: one already ready isn't sent back.");

            // ---------------------------------------------------------------- Targets
            group = "Targets";
            Add("Slot", "One of the five places on each side of the table where a creature stands.");
            Add("Random", "Picked by chance when the effect happens, among the creatures it can affect at that moment. The one with the most [Heat Signature] is picked first, if any has it. No creature to pick: nothing happens.");
            Add("Everything", "Every creature on both sides, and both players.");
            Add("Strongest", "The creature with the highest Attack, picked when the effect happens. Ties: one of them at random. No creature: nothing happens.");
            Add("Weakest", "The creature with the lowest Attack; tied, the one with the lowest Health. Picked when the card resolves, among your creatures on the board.");

            // ---------------------------------------------------------------- Triggers
            group = "Triggers";
            Add("Arrival", "Happens when this creature lands: once its summon finishes and its [Deployment Time] is over.");
            Add("Last Will", "Happens when this creature dies.");
            Add("Hurt", "Happens each time this creature takes damage.");
            Add("On Hit", "Happens when this creature's attack lands, on top of the attack.");
            Add("When Revealed", "Happens when this creature becomes [Revealed], or a [Reveal] shows it to the other side. Against the enemy, it strikes whoever revealed it: the creature whose effect it was, or else its player.");

            // ---------------------------------------------------------------- Keyword abilities (recurring patterns)
            group = "Keyword Abilities";
            Add("Counterattack", "When a creature attacks this one, this one hits it back for its own Attack. Hitting back isn't an attack, so it's never answered.");
            Add("Retaliate", "Retaliate followed by a status, like [Exhausted|exhaust] or [Burned|Burn] 2: when a creature attacks this one, that status lands on the attacker, with the numbers shown.");
            Add("Decay", "Decay X: takes X damage at each [Dusk].");
            Add("Regenerate", "Regenerate X: heals X at each [Dusk].");
            Add("Grow", "Grow X (no number: 1): +X Attack and +X Health for good at each [Dawn].");
            Add("Rebirth", "Once per combat, when this would die, it's reborn instead: it survives with 1 Health. The wheel turns: last life a worm, this life a dog.");
            Add("Summon", "Summon X (no number: 1): X random creatures from your deck enter your empty slots.");
            Add("Destroy", "Kills it outright, whatever its Health or [Armor]. [Untouchable] still saves it.");
            Add("Gone Dark", "Off the board until the next [Dawn]: nothing reaches it, and its [Slot|slot] is empty meanwhile (an attack there hits its player). It comes back to its slot, or the first free one, with a new identity: [Concealed] for 2 Days, so the other side doesn't know what it is, and its old statuses are gone. No free slot: it never comes back.");
            Add("Gamble", "Only one of the card's effects happens, picked at random when it resolves. The house always has an edge.");
            Add("Friendly Fire", "Right-drag one of your creatures onto another of yours: it attacks it, a real attack. Its Last Will happens if it dies, which is the point (a Dead Man's Switch, a Martyr, a Cultist).");
            Add("Send For", "Pick a card from your deck (some only look at its top cards, some only take creatures or spells): it joins your next hand, so your plan has what it needs on the Day it needs it. You pick it when you play the card; if you don't, the best one is sent for.");
            Add("Revive", "Once per combat, when this dies, it rises again at the next [Dawn]: its card comes back from your discard pile to the slot where it fell (or the first free one), with full Health. It's ready at once (no [Deployment Time]) and keeps the stat changes it had, but not its statuses. Its Arrival happens again, and its [Last Will] still happens when it dies. No free slot: it stays dead.");
            Add("Sacrifice", "Sacrifice X: an additional cost. When this card resolves, your X [Weakest|weakest] creatures die first; their [Last Will] happens. You can't play it with fewer creatures on the board, and if they're gone by the time it resolves, it fizzles. Myth bends the rules: one offering pays every Sacrifice of its Day, so a creature you sacrificed this Day, or one a Phoenix Feather is taking, pays instead of your weakest.");
            Add("Prophecy", "Prophecy X: the spell is foretold now and comes true at the [Dawn] X [Days] later, aimed where it was played. Everyone hears it: the queue lists what's been foretold.");
            Add("Single Use", "Once played, the card is gone for the rest of the combat instead of going to your discard pile.");
            Add("Doom Cost", "Doom X, as a cost: when this card resolves, your [Weakest|weakest] creature that isn't [Doomed] yet is Doomed X first: it fights on harder, then dies in X [Days]. You can't play it without one, and if it's gone by the time it resolves, it fizzles. A [Blessed] creature stops the Doom with a stack.");
            Add("Discard Cost", "Discard X: a payment. To play this card, X other cards from your hand (not the ones you're playing) are given up: set aside now, discarded when it resolves, back in your hand if you take the play back. It's paid in full or not at all: with fewer cards, it isn't played and stays in your hand. A calculated card lets you pick them; a chaotic one takes them at random. A card whose effect is to discard (not to pay) discards as many as you have, up to X.");
            Add("Decoy", "A token that isn't what it looks like: until a [Reveal] or [Revealed] shows it, the other side sees the card it pretends to be, with that card's numbers. A token isn't a card in a deck: when it leaves play, it's gone.");
            Add("Take Control", "It changes sides for good: it goes to the [Slot|slot] facing the one it stood in, under your control, and whatever stands there is discarded. It keeps its Health, statuses and stat changes, and loses what it planned. It's still its owner's card: when it leaves play, it goes back to them.");
            Add("Gift", "You play it into an empty enemy slot: it joins their side, and they control it. It's still your card: when it leaves play, it comes back to your discard pile. They can get rid of it by summoning over it.");

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
            Add("Doomed", "Doom X (no number: 1): dies in X [Days], with a last attack ([Last Rites]). Meanwhile it gets +1 Attack each [Dawn] and no other status can land on it, except [Borrowed Time]: on Borrowed Time when the sentence is carried out, it survives that too.");
            Add("Last Rites", "A [Doomed] creature's final attack on the slot opposite it, even if it can't act.");
            Add("Blessed", "Bless X (no number: 1): the next X statuses that would land on it are stopped, good or bad. Each stack also saves it once from being [Destroy|destroyed] by a spell or an ability (a Sacrifice isn't stopped).");
            Add("Heat Signature", "Heat Signature X (no number: 1): [Random|random] effects and redirected attacks go to the creature with the most Heat Signature. It stays until cleansed.");
            Add("Concealed", "Conceal X (no number: 1): for X [Days], its plans show as ??? to the enemy and their [Watchdog|Watchdogs] can't see them.");
            Add("Revealed", "Revealed X (no number: 1): for X [Days], it can't be [Concealed]: its plans can be seen.");
            Add("Encrypted", "Encrypt X (no number: 1): for X [Days], its actions slip past every [Watchdog], the enemy's and yours: none reacts to them, and none spends a charge on them. Unlike [Concealed], everyone still sees what it plans.");
            Add("Untouchable", "Untouchable X (no number: 1): takes no damage at all for X [Days], counting the Day it lands: 1 is this Day only.");
            Add("Cooldown", "Cooldown X (no number: 1): its Active ability rests and can't be used for the next X [Days].");
            Add("Brainwashed", "Brainwashed X (no number: 1): when its X [Days] run out, at [Dusk], it changes sides for good ([Take Control|taken] by the other side) and loses its ability until its card is reshuffled. A [Cleanse] can't remove it: only taking it off the board in time (back to a hand, or [Gone Dark|gone dark]) saves it.");
            Add("Kill Order", "Kill Order X (no number: 1): secret, only the side that gave it can see it, and the spell that gives it is played unseen. When its X [Days] run out, at [Dusk], the creature is [Destroy|destroyed]; [Untouchable] still saves it. A [Cleanse] removes it, knowingly or not.");
            Add("Borrowed Time", "Borrowed Time X (no number: 1): it survives [Lethal] damage for X [Days]. Still at 0 Health when they run out, it dies.");
            Add("Lethal", "What would kill a creature: damage that takes it to 0 Health, or a [Destroy].");
            Add("Licensed", "Its next attack on a creature [Destroy|destroys] it, whatever its Health or [Armor], and the licence is spent ([Untouchable] or a [Shield] stops the hit, and spends it all the same). It lasts until used. With [Double Tap] both hits carry it: two [Blessed] stacks, or one and the creature.");
            Add("Survivor", "From full Health, a hit that would kill it leaves it at 1 Health instead, and the status is spent. It lasts until used: hurt first, and it doesn't help.");
            Add("Composed", "Composed X (no number: 1): it can't be [Exhausted] for X [Days], counting the Day it lands (1 is this Day only), and it shakes off a stun it has.");
            Add("Blackout", "Blackout X (no number: 1): its ability and its effects (arrival, Dawn and Dusk, retaliation, [Last Will]) are switched off for X [Days].");
            Add("Cleanse", "Removes every status, whichever side it helps.");
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
            Add("Backfire", "The attack doesn't happen: the attacker takes its own Attack as damage instead.");

            // ---------------------------------------------------------------- Slot Effects
            group = "Slot Effects";
            Add("Slot Effect", "A condition on a [Slot|slot], not on the creature: it stays with the slot. One per slot.");
            Add("Landmine", "Landmine X: hidden. The next creature to enter the slot takes X damage.");
            Add("Barbed Snare", "Barbed Snare X: hidden. The next creature to leave the slot takes X damage.");
            Add("Shield", "Shield X (no number: 1): for X [Days], the creature in the slot takes no combat damage.");
            Add("Scorched Earth", "The creature in the slot can't use its ability or trigger its effects. Its [Arrival] effects wait until it's off the slot. It can still move and attack.");

            // ---------------------------------------------------------------- Trends
            group = "Trends";
            Add("Grace", "A player in Grace (God's Grace) can fall to 0 Health and not lose, until the [Dusk] its Grace ends: still at 0 then, they lose; healed above 0 in time, they live. If both players fall to 0, the one in Grace wins. It doesn't stack: another Grace doesn't put that Dusk off.");
            Add("Earthquake", "It starts at a random slot on either side and zigzags out across the whole board, crossing to the other side at each column: every column is hit once, the sides taking turns, so the creatures facing each other never both take it. Nobody's attack: nothing answers it or redirects it.");
            Add("Trend", "What the world is into right now: a rule for the whole board, for the [Days] shown (no number: until another Trend replaces it). One at a time: a new Trend replaces the old. A Trend card plays on either side of the table. Like the weather, it's the same for both sides: it helps whoever is built for it, and can turn against whoever set it.");
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
            CreatureAbility.ExtremePrejudice => "Its attack [Destroy|destroys] the creature it hits, whatever its Health or [Armor]; then it's gone too. [Untouchable] or a [Shield] stops the hit.",
            CreatureAbility.Ambush => "It enters as ??? to the other side. While they don't know what it is, or it's [Concealed], everything it deals is doubled. A [Reveal] or [Revealed] shows it for good.",
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
            CreatureAbility.AllergicToLight => $"A drawback: each time it's [Revealed], or a [Reveal] shows it to the other side, it takes {Abilities.LightDamage} damage. Spies coming into the light; one day, vampires.",
            CreatureAbility.Brink => "At 2 Health or less (0 on [Borrowed Time] included), everything it deals is doubled. Utopia's accelerationism: however bad it gets, the breakthrough pays off.",
            CreatureAbility.CollateralDamage => "When its attack deals more than a creature's Health (after [Armor]), the rest goes on to that creature's player: whoever stands behind it pays for the overflow.",
            CreatureAbility.FallGuy => "When an enemy effect would make another friendly creature [Revealed] or [Destroy|destroy] it, it lands on this one instead: it takes the fall.",
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
