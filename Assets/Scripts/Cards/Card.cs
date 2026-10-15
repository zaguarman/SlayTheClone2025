using System.Collections.Generic;

namespace SlayTheClone {
    public interface ICard : IEntity {
        string CardId { get; } // Its definition's: the same for every copy
        /// <summary>This copy of the card, the same through reshuffles (a reshuffled card is made again): what's once per combat is per copy.</summary>
        string CopyId { get; set; }
        List<CardEffect> Effects { get; }
        string Description { get; set; }
        CardData SourceData { get; } // Definition this instance was created from: what its card shows, and how it is reset when discarded
        void Play(IPlayer owner, IActionsQueue context, ITarget target = null);
    }

    public abstract class Card : Entity, ICard {
        public string CardId { get; protected set; }
        private string copyId;
        public string CopyId { get => copyId ?? TargetId; set => copyId = value; }
        public List<CardEffect> Effects { get; protected set; }
        public string Description { get; set; }
        public CardData SourceData { get; internal set; }

        public Card(string name, string cardId = null) : base(name) {
            Effects = new List<CardEffect>();
            Description = "";
            CardId = !string.IsNullOrEmpty(cardId) ? cardId : System.Guid.NewGuid().ToString();
        }

        public abstract void Play(IPlayer owner, IActionsQueue context, ITarget target = null);
    }
}
