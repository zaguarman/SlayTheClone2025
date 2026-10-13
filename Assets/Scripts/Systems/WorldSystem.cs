using System.Linq;
using UnityEngine.Events;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The World in play (GDD 4.2 Worlds): one rule for the whole board, set by a card for some Days, until another World
    /// replaces it. Zeitgeist's signature: the mood of the times changes the rules for everyone.
    /// </summary>
    public interface IWorldSystem {
        World Current { get; }
        /// <summary>The last Day it's in play; null until another World replaces it.</summary>
        int? LastDay { get; }
        UnityEvent<World> OnWorldChanged { get; }
        /// <summary>Sets the World from this Day for this many Days, counting this one (0: until replaced). None clears it.</summary>
        void SetWorld(World world, int days, int currentTurn);
        /// <summary>At Dusk: the World ends after its last Day.</summary>
        void ProcessDusk(int endedTurn);
        /// <summary>Rush Hour: the slowest act first. Speed orders everything the other way round.</summary>
        bool SlowestFirst { get; }
        /// <summary>The cards the player draws on top of the Day's hand (Zeitgeist: whoever has fewer creatures draws 1 more).</summary>
        int ExtraDraws(IPlayer player);
    }

    public class WorldSystem : IWorldSystem {
        public World Current { get; private set; } = World.None;
        public int? LastDay { get; private set; }
        public UnityEvent<World> OnWorldChanged { get; } = new UnityEvent<World>();

        public void SetWorld(World world, int days, int currentTurn) {
            LastDay = world != World.None && days > 0 ? currentTurn + days - 1 : null;
            if (Current == world) return; // The same World again: only its Days start over
            Current = world;
            Log($"The World is now {NameOf(world)}" + (LastDay != null ? $" until the end of Day {LastDay}" : ""), LogTag.Effects);
            OnWorldChanged.Invoke(world);
        }

        public void ProcessDusk(int endedTurn) {
            if (Current != World.None && LastDay != null && endedTurn >= LastDay) SetWorld(World.None, 0, endedTurn);
        }

        public bool SlowestFirst => Current == World.RushHour;

        public int ExtraDraws(IPlayer player) {
            if (Current != World.Zeitgeist || player?.Opponent == null) return 0;
            return CreaturesOf(player) < CreaturesOf(player.Opponent) ? 1 : 0;
        }

        private static int CreaturesOf(IPlayer player) => player.Battlefield.Count(slot => slot.OccupyingCreature != null && !slot.OccupyingCreature.IsDead);

        /// <summary>The World as cards print it, a keyword whose tooltip gives its rule.</summary>
        public static string NameOf(World world) => world switch {
            World.RushHour => "Rush Hour",
            World.None => "No World",
            _ => world.ToString()
        };

        /// <summary>What the World does, in a few words.</summary>
        public static string RuleOf(World world) => world switch {
            World.Zeitgeist => "each Day, whoever has fewer creatures draws 1 more card",
            World.RushHour => "the slowest act first",
            _ => ""
        };
    }
}
