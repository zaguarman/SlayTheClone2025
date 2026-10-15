using System.Collections.Generic;
using UnityEngine;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The adventure under way: the faction and deck you chose, the map, the rooms you've walked through and the one you're
    /// in. It lives across scenes: a fight loads the game scene, and winning it brings you back to the map, a room further.
    /// Losing one ends the run. For now there's one act and no Ascension: the boss at the top is the end.
    /// Each fight won offers three cards of the deck beaten: take one into your deck, or skip (Slay the Spire's reward).
    /// </summary>
    public static class AdventureRun {
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
        /// <summary>The cards offered after the fight just won, to take one of or skip; empty when there's no choice to make.</summary>
        public static List<CardDataScriptableObject> RewardOffer { get; } = new List<CardDataScriptableObject>();

        public static void Start(Faction faction, DeckScriptableObject deck, int seed) {
            Clear();
            Active = true;
            Faction = faction;
            Deck = deck;
            Map = AdventureMap.Generate(seed);
        }

        /// <summary>The rooms you can walk into next: the bottom row at first, then the rooms your room's paths lead to.</summary>
        public static IReadOnlyList<MapNode> Reachable() {
            if (!Active || Won || Lost || Fighting != null || RewardOffer.Count > 0) return new List<MapNode>();
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
            RewardOffer.Clear();
            RewardOffer.AddRange(AdventureDecks.Reward(EnemyDeck, new System.Random(Map.Seed * 17 + room.Row * 5 + room.Column)));
        }

        /// <summary>Takes one of the cards offered into your deck (unless it has 40 already); the offer is over.</summary>
        public static bool TakeReward(CardDataScriptableObject card) {
            if (card == null || !RewardOffer.Contains(card)) return false;
            if (Deck.Cards.Count < PlayerDecks.MaxDeckSize) Deck = DeckScriptableObject.Make(Deck.GetDeckName(), System.Linq.Enumerable.Append(Deck.Cards, card));
            RewardOffer.Clear();
            return true;
        }

        /// <summary>Takes none of the cards offered.</summary>
        public static void SkipReward() => RewardOffer.Clear();

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
            RewardOffer.Clear();
            Map = null;
            Current = Fighting = null;
            Path.Clear();
        }
    }
}
