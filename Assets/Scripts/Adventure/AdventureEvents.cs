using System;
using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// What happens in an adventure's ? rooms (the user, 2026-10-16): an event, Slay the Spire style. A short scene and two
    /// or three choices; each one changes your Health, your EuroYuans or your deck, sometimes on a coin toss, and says what
    /// happened. A choice you can't pay for can't be picked. Each event happens once a run, while there are new ones.
    /// </summary>
    public static class AdventureEvents {
        /// <summary>One of an event's choices.</summary>
        public class Choice {
            public string Label { get; internal set; }
            /// <summary>Whether it can be picked now (it costs EuroYuans you have, or needs a card to take).</summary>
            public Func<bool> Available { get; internal set; } = () => true;
            /// <summary>Does it, and says what happened.</summary>
            internal Func<Context, string> Outcome { get; set; }
        }

        /// <summary>An event: what you find, and what you can do about it.</summary>
        public class Event {
            public string Id { get; internal set; }
            public string Title { get; internal set; }
            public string Text { get; internal set; }
            public List<Choice> Choices { get; } = new List<Choice>();
        }

        /// <summary>What a choice can use: the run's random (the same for the same map and room) and the cards on offer.</summary>
        internal class Context {
            public Random Random;
            public IReadOnlyList<CardDataScriptableObject> Pool;
        }

        public static IReadOnlyList<Event> All => all ??= Build();
        private static List<Event> all;

        /// <summary>The event in this room: one not seen this run, if any is left; the same one for the same map and room.</summary>
        public static Event For(MapNode room, int seed, ICollection<string> seen) {
            var random = new Random(seed * 41 + room.Row * 13 + room.Column);
            var fresh = All.Where(e => !seen.Contains(e.Id)).ToList();
            var from = fresh.Count > 0 ? fresh : All.ToList();
            return from[random.Next(from.Count)];
        }

        internal static string Run(Choice choice, Context context) => choice.Outcome(context);

        // A plain card of the pool, not one you can't put in a deck
        private static CardDataScriptableObject AnyCard(Context context, Func<CardDataScriptableObject, bool> which = null) {
            var cards = context.Pool.Where(c => c != null && !(c is CreatureCardScriptableObject { token: true }) && (which?.Invoke(c) ?? true)).ToList();
            return cards.Count > 0 ? cards[context.Random.Next(cards.Count)] : null;
        }

        private static string Pay(int amount) => AdventureRun.Money(amount);

        private static List<Event> Build() => new List<Event> {
            new Event {
                Id = "Opportunity",
                Title = "A Once-in-a-Lifetime Opportunity",
                Text = "A man in a fleece vest corners you outside a co-working space. His coin, he says, is going to the moon. This week, probably. He has a whitepaper. It is one page long.",
                Choices = {
                    new Choice {
                        Label = $"Invest {Pay(15)}  <color=#C9C0AE>(a coin toss: {Pay(40)} back, or nothing)</color>",
                        Available = () => AdventureRun.EuroYuans >= 15,
                        Outcome = c => {
                            AdventureRun.ChangeMoney(-15);
                            if (c.Random.Next(2) == 0) {
                                AdventureRun.ChangeMoney(40);
                                return $"It went to the moon. You sold at the top, by accident. <b>+{Pay(40)}</b>.";
                            }
                            return $"It went to zero by lunch. He's already pitching the next one. <b>{Pay(15)} gone</b>.";
                        }
                    },
                    new Choice {
                        Label = "Ask for a free sample  <color=#C9C0AE>(a random card into your deck)</color>",
                        Available = () => AdventureRun.Deck.Cards.Count < PlayerDecks.MaxDeckSize,
                        Outcome = c => {
                            var card = AnyCard(c);
                            if (card == null || !AdventureRun.AddCard(card)) return "He hands you a business card. It's blank on both sides.";
                            return $"He gives you something \"for the exposure\": <b>{card.cardName}</b> joins your deck.";
                        }
                    },
                    new Choice {
                        Label = "Walk away",
                        Outcome = c => "You keep walking. Your wallet sends its thanks."
                    }
                }
            },
            new Event {
                Id = "Retreat",
                Title = "Wellness Retreat",
                Text = "Scented candles, a gong, and a man called River who used to work in private equity. For a modest fee they will realign your chakras. The first session is free, with conditions.",
                Choices = {
                    new Choice {
                        Label = $"The full package, {Pay(12)}  <color=#C9C0AE>(+20 Health)</color>",
                        Available = () => AdventureRun.EuroYuans >= 12,
                        Outcome = c => {
                            AdventureRun.ChangeMoney(-12);
                            AdventureRun.ChangeHealth(20);
                            return "Three hours of whale songs. You feel incredible, and slightly poorer. <b>+20 Health</b>.";
                        }
                    },
                    new Choice {
                        Label = "The free session  <color=#C9C0AE>(+8 Health, and they keep a random card of yours)</color>",
                        Available = () => AdventureRun.Deck.Cards.Count > AdventureRun.MinDeck,
                        Outcome = c => {
                            AdventureRun.ChangeHealth(8);
                            var cards = AdventureRun.Deck.Cards;
                            var kept = cards[c.Random.Next(cards.Count)];
                            AdventureRun.TakeCard(kept);
                            return $"<b>+8 Health</b>. On the way out River keeps <b>{kept.cardName}</b>, \"as an energy exchange\".";
                        }
                    },
                    new Choice {
                        Label = "Leave before the gong",
                        Outcome = c => "You slip out during the breathing exercise. Nobody notices; they all have their eyes closed."
                    }
                }
            },
            new Event {
                Id = "Overtime",
                Title = "Quick Favor?",
                Text = "11 p.m. A message from your manager: \"Quick favor?\" It is never quick. It is never a favor.",
                Choices = {
                    new Choice {
                        Label = $"Say yes  <color=#C9C0AE>(-10 Health, +{Pay(25)})</color>",
                        Available = () => AdventureRun.Health > 10,
                        Outcome = c => {
                            AdventureRun.ChangeHealth(-10);
                            AdventureRun.ChangeMoney(25);
                            return $"You finish at 4 a.m. The deck was due Monday; it's Friday. <b>-10 Health</b>, <b>+{Pay(25)}</b>.";
                        }
                    },
                    new Choice {
                        Label = $"Ask for a raise first  <color=#C9C0AE>(a coin toss: +{Pay(15)}, or -5 Health)</color>",
                        Available = () => AdventureRun.Health > 5,
                        Outcome = c => {
                            if (c.Random.Next(2) == 0) {
                                AdventureRun.ChangeMoney(15);
                                return $"Stunned, they say yes. HR is investigating how. <b>+{Pay(15)}</b>.";
                            }
                            AdventureRun.ChangeHealth(-5);
                            return "They schedule a meeting about your attitude. It lasts two hours. <b>-5 Health</b>.";
                        }
                    },
                    new Choice {
                        Label = "Pretend you're asleep",
                        Outcome = c => "You leave it on read. Tomorrow's problem. Tomorrow's you sends their regards."
                    }
                }
            }
        };
    }
}
