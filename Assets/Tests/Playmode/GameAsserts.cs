using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SlayTheClone.Tests {
    public static class TestCategories {
        /// <summary>Tests of bugs that are known and not fixed yet: they fail until the bug is fixed.</summary>
        public const string KnownBug = "KnownBug";
    }

    /// <summary>Checks that hold in every game, whatever was played.</summary>
    public static class GameAsserts {
        /// <summary>All of a player's cards: deck, hand, board and discard pile.</summary>
        public static int CountCards(Player player) =>
            player.Deck.CardsRemaining + player.Hand.Count + player.Battlefield.Count(s => s.IsOccupied())
            + player.Deck.GetDiscardPilePreview().Count;

        /// <summary>No card is in two places at once (in the deck and on the board, say).</summary>
        public static void AssertEachCardInOneZone(Player player) {
            var zones = new (string Name, IEnumerable<ICard> Cards)[] {
                ("deck", player.Deck.GetDeckPreview()),
                ("hand", player.Hand),
                ("board", player.Battlefield.Where(s => s.IsOccupied()).Select(s => (ICard)s.OccupyingCreature)),
                ("discard pile", player.Deck.GetDiscardPilePreview())
            };

            var zoneOf = new Dictionary<ICard, string>();
            foreach (var zone in zones) {
                foreach (var card in zone.Cards) {
                    if (zoneOf.TryGetValue(card, out var otherZone)) {
                        Assert.Fail($"{card.Name} is in {player.Name}'s {otherZone} and {zone.Name} at the same time");
                    }
                    zoneOf[card] = zone.Name;
                }
            }
        }

        /// <summary>Every slot and its creature agree on where the creature is, and no creature is in two slots.</summary>
        public static void AssertBoardsConsistent(params Player[] players) {
            var placed = new HashSet<ICreature>();
            foreach (var slot in players.SelectMany(p => p.Battlefield)) {
                var creature = slot.OccupyingCreature;
                if (creature == null) continue;
                Assert.AreSame(slot, creature.Slot, $"{creature.Name} is in {slot} but thinks it is in {creature.Slot?.ToString() ?? "no slot"}");
                Assert.IsTrue(placed.Add(creature), $"{creature.Name} is in two slots");
                Assert.IsFalse(creature.IsDead, $"{creature.Name} is dead but still on the board");
            }
        }
    }

    public static class Wait {
        /// <summary>Waits a frame at a time until the condition holds; fails the test if it takes longer than the timeout (in real time).</summary>
        public static IEnumerator For(Func<bool> condition, string what, float timeoutSeconds = 30f) {
            float giveUpAt = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition()) {
                if (Time.realtimeSinceStartup > giveUpAt) Assert.Fail($"Gave up waiting for {what} after {timeoutSeconds} seconds");
                yield return null;
            }
        }
    }
}
