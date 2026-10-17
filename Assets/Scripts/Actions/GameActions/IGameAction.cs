namespace Zeitgeist {
    // Actions are plain data; each type is run by its IActionExecutor
    public interface IGameAction {
        string ToString(); // Used for logging and debugging
    }
}
