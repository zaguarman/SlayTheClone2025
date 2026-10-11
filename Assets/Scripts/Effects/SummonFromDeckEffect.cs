using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public static class SummonFromDeckEffect {
        /// <summary>
        /// Takes up to <paramref name="count"/> random creatures out of the player's draw pile
        /// and queues summoning each one into a random empty slot of that player's battlefield.
        /// </summary>
        public static void QueueRandomSummons(IPlayer player, int count, IActionsQueue actionsQueue) {
            if (player?.Deck == null || actionsQueue == null) return;

            var creaturesInDeck = player.Deck.GetDeckPreview().OfType<ICreature>().ToList();
            var emptySlots = player.Battlefield.Where(s => !s.IsOccupied()).ToList();
            int summonCount = System.Math.Min(count, System.Math.Min(creaturesInDeck.Count, emptySlots.Count));

            if (summonCount == 0) {
                Log($"Summon from deck: nothing to summon for {player.Name} (creatures in deck: {creaturesInDeck.Count}, empty slots: {emptySlots.Count})", LogTag.Creatures | LogTag.Effects);
                return;
            }

            for (int i = 0; i < summonCount; i++) {
                var creature = TakeRandom(creaturesInDeck);
                var slot = TakeRandom(emptySlots);

                player.Deck.RemoveCard(creature);
                actionsQueue.AddAction(new SummonCreatureAction(creature, player, slot, fromDeck: true));
                Log($"Summon from deck: queued {creature.Name} for {player.Name}", LogTag.Creatures | LogTag.Effects | LogTag.Actions);
            }
        }

        private static T TakeRandom<T>(System.Collections.Generic.List<T> items) {
            int index = GameRandom.Next(items.Count);
            var item = items[index];
            items.RemoveAt(index);
            return item;
        }
    }
}
