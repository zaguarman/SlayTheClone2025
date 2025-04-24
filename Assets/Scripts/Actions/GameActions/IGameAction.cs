using UnityEngine;
using System;

public interface IGameAction {
    // Execute() method removed - all actions are now executed through their specific IActionExecutor
    string ToString(); // Keep ToString() for logging and debugging
}