using System.Collections.Generic;
using static DebugLogger;

public interface ICard : IEntity {
    string CardId { get; }
    List<CardEffect> Effects { get; }
    string Description { get; set; }
    void Play(IPlayer owner, ActionsQueue context, ITarget target = null);
}

public class Card : Entity, ICard {
    public string CardId { get; protected set; }
    public List<CardEffect> Effects { get; protected set; }
    public string Description { get; set; }

    public Card(string name) : base(name) {
        Effects = new List<CardEffect>();
        Description = "";
        CardId = System.Guid.NewGuid().ToString(); // Generate a unique ID on creation
    }

    public Card(string name, string cardId) : base(name) {
        Effects = new List<CardEffect>();
        Description = "";
        CardId = !string.IsNullOrEmpty(cardId) ? cardId : System.Guid.NewGuid().ToString();
    }

    public virtual void Play(IPlayer owner, ActionsQueue context, ITarget target = null) {
        Log($"[Card] Playing {Name} with {Effects.Count} effects and target {target.TargetId}", LogTag.Cards | LogTag.Actions);
        // Base implementation for non-creature cards
        foreach (var effect in Effects) {
            Log($"[Card] Processing effect with trigger {effect.trigger}", LogTag.Cards | LogTag.Actions | LogTag.Effects);
        }
    }
}