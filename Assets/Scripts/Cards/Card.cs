using System.Collections.Generic;

public interface ICard : IEntity {
    string CardId { get; }
    List<CardEffect> Effects { get; }
    string Description { get; set; }
    void Play(IPlayer owner, IActionsQueue context, ITarget target = null);
}

public abstract class Card : Entity, ICard {
    public string CardId { get; protected set; }
    public List<CardEffect> Effects { get; protected set; }
    public string Description { get; set; }

    public Card(string name) : base(name) {
        Effects = new List<CardEffect>();
        Description = "";
        CardId = System.Guid.NewGuid().ToString();
    }

    public Card(string name, string cardId) : base(name) {
        Effects = new List<CardEffect>();
        Description = "";
        CardId = !string.IsNullOrEmpty(cardId) ? cardId : System.Guid.NewGuid().ToString();
    }

    public abstract void Play(IPlayer owner, IActionsQueue context, ITarget target = null);
}