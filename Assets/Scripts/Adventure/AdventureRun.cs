using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The adventure under way: the faction and deck you chose, the map, the rooms you've walked through and the one you're
    /// in. It lives across scenes: a fight loads the game scene, and winning it brings you back to the map, a room further.
    /// Losing one ends the run. For now there's one act and no Ascension: the boss at the top is the end.
    /// Each fight won drops card packs (an elite two), each three cards of the deck beaten: open one, take a card into your
    /// deck or skip it, as often as you like, then proceed (Slay the Spire's rewards).
    /// Each fight won also pays EuroYuans (the user, 2026-10-16), spent at the shops on the way: cards, or a card taken
    /// out of your deck. A roguelite's money: the run ends, it's gone.
    /// </summary>
    public static class AdventureRun {
        /// <summary>A card pack dropped by a fight: three cards to take one of, or none.</summary>
        public class RewardPack {
            public List<CardDataScriptableObject> Cards { get; } = new List<CardDataScriptableObject>();
            /// <summary>The card taken from it; null while undecided or skipped.</summary>
            public CardDataScriptableObject Taken { get; internal set; }
            /// <summary>A card was taken, or it was skipped: the pack is used up.</summary>
            public bool Done { get; internal set; }
        }

        /// <summary>A card for sale at a shop.</summary>
        public class ShopOffer {
            public CardDataScriptableObject Card { get; internal set; }
            public int Price { get; internal set; }
            public bool Sold { get; internal set; }
        }

        /// <summary>The currency's symbol: the yen's Y fused to the euro's C, its bars shared (TextMeshPro rich text).</summary>
        public const string Symbol = "<b>\u00A5<space=-0.42em>\u20AC</b>";
        public const int FightPurse = 10, ElitePurse = 15; // What a fight pays (the user: about 10)
        public const int RemovalPrice = 20, RemovalRise = 10; // Taking a card out of your deck; dearer each time, as in Slay the Spire
        public const int ShopCards = 5; // Cards for sale, all of your faction (the user, 2026-10-16)
        public const int MinDeck = 6; // The fewest cards a removal leaves you
        /// <summary>The price of a card at a shop, by its tier.</summary>
        public static int PriceOf(AdventureDecks.Tier tier) => tier switch {
            AdventureDecks.Tier.D => 8,
            AdventureDecks.Tier.C => 12,
            AdventureDecks.Tier.B => 16,
            AdventureDecks.Tier.A => 22,
            _ => 30
        };
        /// <summary>An amount with the symbol: "¥€ 25".</summary>
        public static string Money(int amount) => $"{Symbol}\u00A0{amount}";

        public const int StartingHealth = 60; // The user: about 60 at the start, no maximum
        public static bool Active { get; private set; }
        /// <summary>Your Health, carried from fight to fight: a fight starts at it and ends with what's left. 0: the run is lost.</summary>
        public static int Health { get; private set; }
        /// <summary>The run's money (EY): fights pay it, shops take it. Gone when the run ends.</summary>
        public static int EuroYuans { get; private set; }
        /// <summary>What the fight just won paid; 0 outside the rewards.</summary>
        public static int Earned { get; private set; }
        /// <summary>The shop you're in: its cards, sold or not; empty outside one.</summary>
        public static List<ShopOffer> Shop { get; } = new List<ShopOffer>();
        /// <summary>You're at a shop: the map waits until you leave.</summary>
        public static bool InShop { get; private set; }
        /// <summary>A card was taken out of your deck at this shop (once a shop).</summary>
        public static bool RemovedHere { get; private set; }
        /// <summary>The event of the ? room you're in, until you leave it; null outside one.</summary>
        public static AdventureEvents.Event Event { get; private set; }
        /// <summary>What the choice made at the event did; null until one is made.</summary>
        public static string EventOutcome { get; private set; }
        /// <summary>You're at an event: the map waits until you go on.</summary>
        public static bool InEvent => Event != null;
        private static readonly HashSet<string> seenEvents = new HashSet<string>();
        private static MapNode eventRoom;

        /// <summary>What taking a card out costs now.</summary>
        public static int RemovalCost => RemovalPrice + RemovalRise * removals;
        private static int removals;
        public static Faction Faction { get; private set; }
        public static DeckScriptableObject Deck { get; private set; }
        public static AdventureMap Map { get; private set; }
        /// <summary>The room you're in; null before the first.</summary>
        public static MapNode Current { get; private set; }
        /// <summary>The rooms walked through, in order.</summary>
        public static List<MapNode> Path { get; } = new List<MapNode>();
        /// <summary>The fight you've gone into and not finished yet; null outside one.</summary>
        public static MapNode Fighting { get; private set; }
        /// <summary>The boss is beaten: the run is won.</summary>
        public static bool Won { get; private set; }
        /// <summary>The last fight was lost: the run is over (the map still shows how far you got).</summary>
        public static bool Lost { get; private set; }
        /// <summary>The deck of the fight under way, or of the last one fought.</summary>
        public static DeckScriptableObject EnemyDeck { get; private set; }
        /// <summary>The packs dropped by the fight just won, until you proceed to the map; empty outside the rewards.</summary>
        public static List<RewardPack> Rewards { get; } = new List<RewardPack>();
        /// <summary>The fight is won and its rewards are up: the map waits until you proceed.</summary>
        public static bool InRewards => Rewards.Count > 0;

        /// <summary>The cards on offer when the run started: rewards, shops and events take theirs from it.</summary>
        public static IReadOnlyList<CardDataScriptableObject> Pool { get; private set; } = new List<CardDataScriptableObject>();
        public const int BossHealth = 60, HealthStep = 5; // Enemy Health (the user, 2026-10-16): 5, 10, 15... as you go; the boss 60

        /// <summary>The fights won so far this run.</summary>
        public static int FightsWon => Path.Count(n => n.IsCombat);

        /// <summary>The Health the enemy of this fight room starts at: 5 for the first fight, 5 more for each one won since, the boss 60.</summary>
        public static int EnemyHealthFor(MapNode room) => room.Type == RoomType.Boss ? BossHealth
            : Mathf.Min(BossHealth - HealthStep, HealthStep * (FightsWon + 1) + (room.Type == RoomType.Elite ? HealthStep : 0));

        public static void Start(Faction faction, DeckScriptableObject deck, int seed, IEnumerable<CardDataScriptableObject> pool = null) {
            Clear();
            Pool = (pool ?? Enumerable.Empty<CardDataScriptableObject>()).Where(c => c != null).Distinct().ToList();
            Active = true;
            Faction = faction;
            Deck = deck;
            Health = StartingHealth;
            Map = AdventureMap.Generate(seed);
        }

        /// <summary>The rooms you can walk into next: the bottom row at first, then the rooms your room's paths lead to.</summary>
        public static IReadOnlyList<MapNode> Reachable() {
            if (!Active || Won || Lost || Fighting != null || InRewards || InShop || InEvent) return new List<MapNode>();
            return Current == null ? new List<MapNode>(Map.Row(0)) : Current.Next;
        }

        public static bool CanEnter(MapNode node) => node != null && System.Linq.Enumerable.Contains(Reachable(), node);

        /// <summary>
        /// Walks into the room. A fight (an elite, the boss) starts: true, and the caller starts the game; it counts once won
        /// (<see cref="WinFight"/>). Any other room is walked through at once: false. A shop opens, stocked from the pool.
        /// </summary>
        public static bool Enter(MapNode node, IEnumerable<CardDataScriptableObject> pool = null) {
            if (!CanEnter(node)) return false;
            if (node.IsCombat) {
                Fighting = node;
                return true;
            }
            Arrive(node);
            if (node.Type == RoomType.Shop && pool != null) OpenShop(node, pool);
            if (node.Type == RoomType.Unknown && pool != null) {
                Event = AdventureEvents.For(node, Map.Seed, seenEvents);
                seenEvents.Add(Event.Id);
                EventOutcome = null;
                eventRoom = node;
            }
            return false;
        }

        /// <summary>Makes the event's choice, if it can be made and none was yet: what it says happens.</summary>
        public static bool ChooseAtEvent(AdventureEvents.Choice choice, IEnumerable<CardDataScriptableObject> pool) {
            if (!InEvent || EventOutcome != null || choice == null || !Event.Choices.Contains(choice) || !choice.Available()) return false;
            var context = new AdventureEvents.Context {
                Random = new System.Random(Map.Seed * 59 + eventRoom.Row * 17 + eventRoom.Column),
                Pool = (pool ?? Enumerable.Empty<CardDataScriptableObject>()).ToList()
            };
            EventOutcome = AdventureEvents.Run(choice, context);
            return true;
        }

        /// <summary>On from the event to the map.</summary>
        public static void LeaveEvent() {
            Event = null;
            EventOutcome = null;
            eventRoom = null;
        }

        /// <summary>Health won or lost outside a fight (an event): never below 1, and no maximum.</summary>
        public static void ChangeHealth(int amount) => Health = Mathf.Max(1, Health + amount);

        /// <summary>EuroYuans won or spent outside a fight and a shop (an event); never below 0.</summary>
        public static void ChangeMoney(int amount) => EuroYuans = Mathf.Max(0, EuroYuans + amount);

        /// <summary>A card into your deck, unless it's full.</summary>
        public static bool AddCard(CardDataScriptableObject card) {
            if (card == null || Deck.Cards.Count >= PlayerDecks.MaxDeckSize) return false;
            Deck = DeckScriptableObject.Make(Deck.GetDeckName(), Deck.Cards.Append(card));
            return true;
        }

        /// <summary>One copy of a card out of your deck (not below <see cref="MinDeck"/>).</summary>
        public static bool TakeCard(CardDataScriptableObject card) {
            if (card == null || Deck.Cards.Count <= MinDeck || !Deck.Cards.Contains(card)) return false;
            var cards = Deck.Cards.ToList();
            cards.Remove(card);
            Deck = DeckScriptableObject.Make(Deck.GetDeckName(), cards);
            return true;
        }

        // A shop's cards: five of your faction, different ones, none you can't put in a deck; priced by tier, the better ones
        // rarer (Slay the Spire's shops: mostly commons, now and then a rare)
        private static void OpenShop(MapNode room, IEnumerable<CardDataScriptableObject> pool) {
            Shop.Clear();
            RemovedHere = false;
            InShop = true;
            var cards = pool.Where(c => c != null && !(c is CreatureCardScriptableObject { token: true })).Distinct().ToList();
            var tiers = AdventureDecks.Tiers(cards);
            var random = new System.Random(Map.Seed * 23 + room.Row * 11 + room.Column);
            double Draw(CardDataScriptableObject c) => random.NextDouble() + (int)tiers[c] * 0.1;
            var own = cards.Where(c => c.faction == Faction).OrderBy(Draw).Take(ShopCards).ToList();
            foreach (var card in own.OrderBy(c => c is CreatureCardScriptableObject ? 0 : 1).ThenBy(c => tiers[c]))
                Shop.Add(new ShopOffer { Card = card, Price = PriceOf(tiers[card]) });
        }

        /// <summary>Buys the card, if you can pay it and your deck has room: into your deck, the money gone.</summary>
        public static bool Buy(ShopOffer offer) {
            if (!InShop || offer == null || offer.Sold || !Shop.Contains(offer) || EuroYuans < offer.Price) return false;
            if (Deck.Cards.Count >= PlayerDecks.MaxDeckSize) return false;
            EuroYuans -= offer.Price;
            Deck = DeckScriptableObject.Make(Deck.GetDeckName(), Deck.Cards.Append(offer.Card));
            offer.Sold = true;
            return true;
        }

        /// <summary>You can have a card taken out of your deck here: once a shop, if you can pay, down to <see cref="MinDeck"/> cards.</summary>
        public static bool CanRemove => InShop && !RemovedHere && EuroYuans >= RemovalCost && Deck.Cards.Count > MinDeck;

        /// <summary>Takes one copy of the card out of your deck, for <see cref="RemovalCost"/>.</summary>
        public static bool Remove(CardDataScriptableObject card) {
            if (!CanRemove || card == null || !Deck.Cards.Contains(card)) return false;
            EuroYuans -= RemovalCost;
            removals++;
            RemovedHere = true;
            var cards = Deck.Cards.ToList();
            cards.Remove(card);
            Deck = DeckScriptableObject.Make(Deck.GetDeckName(), cards);
            return true;
        }

        /// <summary>Out of the shop, back to the map; what you didn't buy stays behind.</summary>
        public static void LeaveShop() {
            InShop = false;
            Shop.Clear();
        }

        /// <summary>The deck you're about to fight.</summary>
        public static void SetEnemy(DeckScriptableObject deck) => EnemyDeck = deck;

        /// <summary>The fight is won, with this much Health left (null: as it went in).</summary>
        public static void WinFight(int? healthLeft = null) {
            if (Fighting == null) return;
            if (healthLeft.HasValue) Health = Mathf.Max(1, healthLeft.Value);
            var room = Fighting;
            Fighting = null;
            Arrive(room);
            Earned = room.Type == RoomType.Elite ? ElitePurse : room.Type == RoomType.Boss ? 0 : FightPurse;
            EuroYuans += Earned;
            if (room.Type == RoomType.Boss) {
                Won = true;
                return;
            }
            Rewards.Clear();
            var random = new System.Random(Map.Seed * 17 + room.Row * 5 + room.Column);
            int packs = room.Type == RoomType.Elite ? 2 : 1; // An elite drops a second pack
            var offered = new HashSet<CardDataScriptableObject>();
            float strength = AdventureDecks.StrengthOf(room);
            for (int i = 0; i < packs; i++) {
                var pack = new RewardPack();
                // Cards of your own faction (the user); a second pack shows others, when there are enough
                var fresh = AdventureDecks.Reward(Faction, strength, Pool, random, offered);
                pack.Cards.AddRange(fresh.Count > 0 ? fresh : AdventureDecks.Reward(Faction, strength, Pool, random));
                foreach (var card in pack.Cards) offered.Add(card);
                if (pack.Cards.Count > 0) Rewards.Add(pack);
            }
            if (Rewards.Count == 0) Rewards.Add(new RewardPack { Done = true }); // Nothing to offer: still the rewards screen, to proceed
        }

        /// <summary>Takes one of the pack's cards into your deck (unless it has 40 already); the pack is used up.</summary>
        public static bool TakeReward(RewardPack pack, CardDataScriptableObject card) {
            if (pack == null || pack.Done || card == null || !pack.Cards.Contains(card)) return false;
            if (Deck.Cards.Count < PlayerDecks.MaxDeckSize) Deck = DeckScriptableObject.Make(Deck.GetDeckName(), Deck.Cards.Append(card));
            pack.Taken = card;
            pack.Done = true;
            return true;
        }

        /// <summary>Takes none of the pack's cards: it's used up.</summary>
        public static void SkipReward(RewardPack pack) {
            if (pack != null) pack.Done = true;
        }

        /// <summary>Leaves the rewards for the map; packs not opened are left behind.</summary>
        public static void Proceed() {
            Rewards.Clear();
            Earned = 0;
        }

        public static void LoseFight() {
            if (Fighting == null) return;
            Fighting = null;
            Lost = true;
            Health = 0;
        }

        /// <summary>Back out of a fight that hasn't been played to the end (the pause menu's Main Menu): you can go in again.</summary>
        public static void LeaveFight() => Fighting = null;

        private static void Arrive(MapNode node) {
            Current = node;
            Path.Add(node);
        }

        // Also runs when play mode starts, so a run from an earlier session never leaks into this one
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() {
            Active = Won = Lost = InShop = RemovedHere = false;
            EuroYuans = Earned = removals = Health = 0; // A roguelite: the money goes with the run
            Pool = new List<CardDataScriptableObject>();
            Event = null;
            EventOutcome = null;
            eventRoom = null;
            seenEvents.Clear();
            Shop.Clear();
            Faction = Faction.None;
            Deck = EnemyDeck = null;
            Rewards.Clear();
            Map = null;
            Current = Fighting = null;
            Path.Clear();
        }
    }
}
