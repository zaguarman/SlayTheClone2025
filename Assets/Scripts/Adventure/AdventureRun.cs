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

        public static bool Active { get; private set; }
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

        public static void Start(Faction faction, DeckScriptableObject deck, int seed) {
            Clear();
            Active = true;
            Faction = faction;
            Deck = deck;
            Map = AdventureMap.Generate(seed);
        }

        /// <summary>The rooms you can walk into next: the bottom row at first, then the rooms your room's paths lead to.</summary>
        public static IReadOnlyList<MapNode> Reachable() {
            if (!Active || Won || Lost || Fighting != null || InRewards) return new List<MapNode>();
            return Current == null ? new List<MapNode>(Map.Row(0)) : Current.Next;
        }

        public static bool CanEnter(MapNode node) => node != null && System.Linq.Enumerable.Contains(Reachable(), node);

        /// <summary>
        /// Walks into the room. A fight (an elite, the boss) starts: true, and the caller starts the game; it counts once won
        /// (<see cref="WinFight"/>). Any other room is walked through at once (nothing happens in them yet): false.
        /// </summary>
        public static bool Enter(MapNode node) {
            if (!CanEnter(node)) return false;
            if (node.IsCombat) {
                Fighting = node;
                return true;
            }
            Arrive(node);
            return false;
        }

        /// <summary>The deck you're about to fight.</summary>
        public static void SetEnemy(DeckScriptableObject deck) => EnemyDeck = deck;

        public static void WinFight() {
            if (Fighting == null) return;
            var room = Fighting;
            Fighting = null;
            Arrive(room);
            if (room.Type == RoomType.Boss) {
                Won = true;
                return;
            }
            Rewards.Clear();
            var random = new System.Random(Map.Seed * 17 + room.Row * 5 + room.Column);
            int packs = room.Type == RoomType.Elite ? 2 : 1; // An elite drops a second pack
            var offered = new HashSet<CardDataScriptableObject>();
            for (int i = 0; i < packs; i++) {
                var pack = new RewardPack();
                // A second pack shows other cards of the deck, when there are enough
                var fresh = AdventureDecks.Reward(EnemyDeck, random).Where(c => !offered.Contains(c)).ToList();
                pack.Cards.AddRange(fresh.Count > 0 ? fresh : AdventureDecks.Reward(EnemyDeck, random));
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
        public static void Proceed() => Rewards.Clear();

        public static void LoseFight() {
            if (Fighting == null) return;
            Fighting = null;
            Lost = true;
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
            Active = Won = Lost = false;
            Faction = Faction.None;
            Deck = EnemyDeck = null;
            Rewards.Clear();
            Map = null;
            Current = Fighting = null;
            Path.Clear();
        }
    }
}
