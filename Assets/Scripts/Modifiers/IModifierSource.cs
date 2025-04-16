// Optional interface to identify what applied a modifier
public interface IModifierSource {
    string GetSourceName(); // e.g., Card name, Creature name
    string GetSourceId();   // e.g., Card ID, Creature Instance ID
}
