using System.Collections.Generic;

namespace SlayTheClone {
    public interface ICard : IEntity {
        string CardId { get; }
        List<CardEffect> Effects { get; }
        string Description { get; set; }
        CardData SourceData { get; } // Definition this instance was created from; used to reset it when discarded
        void Play(IPlayer owner, IActionsQueue context, ITarget target = null);
    }

    public abstract class Card : Entity, ICard {
        public string CardId { get; protected set; }
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
