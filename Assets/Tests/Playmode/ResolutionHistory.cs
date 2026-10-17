using System.Collections.Generic;
using System.Linq;

namespace Zeitgeist.Tests {
    /// <summary>
    /// What happened while actions resolved, in order: every action the queue ran and everything the game announced.
    /// Lets tests check that something happened once, in the right order, or not at all.
    /// </summary>
    public sealed class ResolutionHistory {
        public List<IGameAction> Executed { get; } = new List<IGameAction>();
        public List<(ICreature Creature, int Amount)> CreatureDamage { get; } = new List<(ICreature, int)>();
        public List<(IPlayer Player, int Amount)> PlayerDamage { get; } = new List<(IPlayer, int)>();
        public List<ICreature> Deaths { get; } = new List<ICreature>();
        public List<IPlayer> Winners { get; } = new List<IPlayer>();

        public ResolutionHistory(IActionsQueue queue, IGameMediator mediator) {
            queue.OnActionExecuting.AddListener(Executed.Add);
            mediator.CreatureDamaged += (creature, amount) => CreatureDamage.Add((creature, amount));
            mediator.PlayerDamaged += (player, amount) => PlayerDamage.Add((player, amount));
            mediator.CreatureDied += Deaths.Add;
            mediator.GameOver += Winners.Add;
        }

        /// <summary>Each hit the creature took to its health, in order.</summary>
        public List<int> DamageTo(ICreature creature) => CreatureDamage.Where(e => e.Creature == creature).Select(e => e.Amount).ToList();

        /// <summary>Each hit the player took, in order.</summary>
        public List<int> DamageTo(IPlayer player) => PlayerDamage.Where(e => e.Player == player).Select(e => e.Amount).ToList();

        /// <summary>The kinds of the actions that ran, in order: "PlayCard", "DamageCreature"...</summary>
        public List<string> ExecutedKinds() => Executed.Select(Kind).ToList();

        public static string Kind(IGameAction action) => action.GetType().Name.Replace("Action", "");

        public void Clear() {
            Executed.Clear();
            CreatureDamage.Clear();
            PlayerDamage.Clear();
            Deaths.Clear();
            Winners.Clear();
        }
    }
}
