using System;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The game's event hub (see <see cref="IGameMediator"/>). A plain object created by the bootstrap, so it lives
    /// exactly as long as the game that holds it.
    /// </summary>
    public class GameMediator : IGameMediator {
        public event Action<IPlayer, int> PlayerDamaged;
        public event Action<IPlayer, int> PlayerHealed;
        public event Action<IPlayer> GameOver;
        public event Action<ICard, string> PlayRefused;

        public event Action<ICreature, IPlayer> CreatureSummoned;
        public event Action<ICreature, int> CreatureDamaged;
        public event Action<ICreature, int> CreatureHealed;
        public event Action<ICreature, int> CreatureArmorChanged;
        public event Action<ICreature> CreatureStatsChanged;
        public event Action<ICreature, StatusEffectType, bool> CreatureStatusChanged;
        public event Action<ICreature> CreatureDied;

        public event Action<IPlayer> HandStateChanged;
        public event Action<IPlayer> BattlefieldStateChanged;
        public event Action ActionsQueueChanged;
        public event Action<int> TurnEnded;

        private bool gameOver;
        public bool IsGameOver => gameOver;

        #region Players
        public void NotifyPlayerDamaged(IPlayer player, int damage) {
            if (player == null) throw new ArgumentNullException(nameof(player));

            Log($"{damage} damage to {player.Name}", LogTag.Players | LogTag.Combat);
            PlayerDamaged?.Invoke(player, damage);

            if (player.Health <= 0) {
                if (player.GraceUntilDay != null) Log($"{player.Name} is at 0 Health, held up by Grace", LogTag.Players); // Grace.ProcessDusk decides
                else NotifyGameOver(player.Opponent); // Both at 0: whoever is in Grace wins, since only the other one loses here
            }
        }

        public void NotifyPlayerHealed(IPlayer player, int amount) {
            if (player == null) throw new ArgumentNullException(nameof(player));

            Log($"{player.Name} healed for {amount}, health now: {player.Health}", LogTag.Players | LogTag.Effects);
            PlayerHealed?.Invoke(player, amount);
        }

        public void NotifyPlayRefused(ICard card, string reason) {
            Log($"{card?.Name} isn't played: {reason}", LogTag.Cards | LogTag.Actions);
            PlayRefused?.Invoke(card, reason);
        }

        public void NotifyGameOver(IPlayer winner) {
            if (winner == null) throw new ArgumentNullException(nameof(winner));
            if (gameOver) return;
            gameOver = true;

            Log($"Game over: {winner.Name} wins", LogTag.Players);
            GameOver?.Invoke(winner);
        }
        #endregion

        #region Creatures
        // Each change is announced game-wide, then to the creature's own listeners (its card)
        public void NotifyCreatureSummoned(ICreature creature, IPlayer owner) {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            Log($"Creature summoned: {creature.Name} by {owner.Name}", LogTag.Creatures);
            CreatureSummoned?.Invoke(creature, owner);
        }

        public void NotifyCreatureDamaged(ICreature creature, int damage) {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            Log($"{damage} damage to {creature.Name}", LogTag.Creatures | LogTag.Combat);
            CreatureDamaged?.Invoke(creature, damage);
            creature.NotifyChanged();
            // Death is announced separately by DamageCreatureActionExecutor via NotifyCreatureDied
        }

        public void NotifyCreatureHealed(ICreature creature, int amount) {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            Log($"{creature.Name} healed for {amount}, health now: {creature.Health}", LogTag.Creatures | LogTag.Effects);
            CreatureHealed?.Invoke(creature, amount);
            creature.NotifyChanged();
        }

        public void NotifyCreatureArmorChanged(ICreature creature, int newArmor) {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            CreatureArmorChanged?.Invoke(creature, newArmor);
            creature.NotifyChanged();
        }

        public void NotifyCreatureStatsChanged(ICreature creature) {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            Log($"Stats changed for {creature.Name} (TargetID: {creature.TargetId.ToUpper()})", LogTag.Creatures | LogTag.Effects);
            CreatureStatsChanged?.Invoke(creature);
            creature.NotifyChanged();
        }

        public void NotifyCreatureStatusChanged(ICreature creature, StatusEffectType status, bool applied) {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            Log($"{creature.Name} {(applied ? "gained" : "lost")} status {status}", LogTag.Creatures | LogTag.Effects);
            CreatureStatusChanged?.Invoke(creature, status, applied);
            creature.NotifyChanged();
        }

        public void NotifyCreatureDied(ICreature creature) {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            Log($"Creature died: {creature.Name}", LogTag.Creatures);
            CreatureDied?.Invoke(creature);
            creature.NotifyChanged();
        }
        #endregion

        #region Zones and turn flow
        public void NotifyHandStateChanged(IPlayer player) {
            Log($"Hand state changed for {player.Name}", LogTag.Players | LogTag.Cards);
            HandStateChanged?.Invoke(player);
        }

        public void NotifyBattlefieldStateChanged(IPlayer player) {
            Log($"Battlefield state changed for {player.Name}", LogTag.Players | LogTag.Cards);
            BattlefieldStateChanged?.Invoke(player);
        }

        public void NotifyActionsQueueChanged() {
            ActionsQueueChanged?.Invoke();
        }

        public void NotifyTurnEnded(int turnNumber) {
            Log($"Turn {turnNumber} ended", LogTag.Turns);
            TurnEnded?.Invoke(turnNumber);
        }
        #endregion
    }
}
