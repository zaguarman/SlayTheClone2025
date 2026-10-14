using System;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The game's event hub: rules code announces what happened through the Notify methods, and views listen to the
    /// matching events (subscribe with +=, unsubscribe with -=). A card that only cares about its own creature listens
    /// to <see cref="ICreature.Changed"/> instead, which the creature notifications also raise.
    /// </summary>
    public interface IGameMediator {
        #region Players
        event Action<IPlayer, int> PlayerDamaged;
        event Action<IPlayer, int> PlayerHealed;
        event Action<IPlayer> GameOver; // With the winner
        event Action<ICard, string> PlayRefused; // A card that couldn't be played, and why (its cost can't be paid)
        #endregion

        #region Creatures
        event Action<ICreature, IPlayer> CreatureSummoned;
        event Action<ICreature, int> CreatureDamaged;
        event Action<ICreature, int> CreatureHealed;
        event Action<ICreature, int> CreatureArmorChanged; // With the new armor
        event Action<ICreature> CreatureStatsChanged;
        event Action<ICreature, StatusEffectType, bool> CreatureStatusChanged; // True when applied, false when removed
        event Action<ICreature> CreatureDied;
        #endregion

        #region Zones and turn flow
        event Action<IPlayer> HandStateChanged;
        event Action<IPlayer> BattlefieldStateChanged;
        event Action ActionsQueueChanged;
        event Action<int> TurnEnded;
        #endregion

        #region Notifications
        /// <summary>Also ends the game when the player's health reaches 0.</summary>
        void NotifyPlayerDamaged(IPlayer player, int damage);
        void NotifyPlayerHealed(IPlayer player, int amount);
        /// <summary>The game ends once; later calls are ignored.</summary>
        void NotifyGameOver(IPlayer winner);
        void NotifyPlayRefused(ICard card, string reason);
        /// <summary>A player fell to 0 Health: the combat is over, and no more turns are played.</summary>
        bool IsGameOver { get; }

        void NotifyCreatureSummoned(ICreature creature, IPlayer owner);
        void NotifyCreatureDamaged(ICreature creature, int damage);
        void NotifyCreatureHealed(ICreature creature, int amount);
        void NotifyCreatureArmorChanged(ICreature creature, int newArmor);
        void NotifyCreatureStatsChanged(ICreature creature);
        void NotifyCreatureStatusChanged(ICreature creature, StatusEffectType status, bool applied);
        void NotifyCreatureDied(ICreature creature);

        void NotifyHandStateChanged(IPlayer player);
        void NotifyBattlefieldStateChanged(IPlayer player);
        void NotifyActionsQueueChanged();
        void NotifyTurnEnded(int turnNumber);
        #endregion
    }
}
