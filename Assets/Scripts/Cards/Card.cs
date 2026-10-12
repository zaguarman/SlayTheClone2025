using System.Collections.Generic;

namespace SlayTheClone {
    public interface ICard : IEntity {
        string CardId { get; }
        List<CardEffect> Effects { get; }
        string Description { get; set; }
        CardData SourceData { get; } // Definition this instance was created from: what its card shows, and how it is reset when discarded
        int Priority { get; } // Printed Priority (Action): a spell's Spell Action, a creature's attacks (GDD 3.7)
        void Play(IPlayer owner, IActionsQueue context, ITarget target = null);
    }

    public abstract class Card : Entity, ICard {
        public string CardId { get; protected set; }
        public List<CardEffect> Effects { get; protected set; }
        public string Description { get; set; }
        public CardData SourceData { get; internal set; }
        public int Priority { get; internal set; }

        public Card(string name, string cardId = null) : base(name) {
            Effects = new List<CardEffect>();
            Description = "";
            CardId = !string.IsNullOrEmpty(cardId) ? cardId : System.Guid.NewGuid().ToString();
        }

        public abstract void Play(IPlayer owner, IActionsQueue context, ITarget target = null);
    }
}
