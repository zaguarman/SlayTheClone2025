public class StyleGuide {
    /*
    --- C# Naming Conventions & Code Style Guide (IA Prompt Optimized) ---

    **Purpose:** To ensure consistency, readability, and maintainability across the project codebase. This guide is based on common C# practices and patterns observed in the project.

    **Key Principles:** Clarity, Consistency, Readability.

    **Sections:**
    1. Naming Conventions
    2. Code Style & Formatting
    3. Unity Specifics
    4. Tooling

    ---

    ## 1. Naming Conventions

    - **Classes, Structs, Enums, Delegates, Interfaces:**
        - Use `PascalCase`.
        - Examples: `ActionsQueue`, `Player`, `CardData`, `LogTag`, `IPlayer`, `InitializableComponent`.

    - **Interfaces:**
        - Prefix with `I`.
        - Use `PascalCase`.
        - Examples: `ICard`, `IModifiable`, `IPlayer`.

    - **Methods:**
        - Use `PascalCase`.
        - Use descriptive verb phrases (e.g., `ResolveActions`, `GetCardData`, `InitializeBattlefield`).
        - Examples: `AddAction`, `Execute`, `Initialize`, `UpdateUI`, `TakeDamage`.

    - **Properties & Public Fields:**
        - Use `PascalCase`.
        - Examples: `Health`, `Name`, `TargetId`, `IsInitialized`, `Instance`, `OccupyingCard`.

    - **Events:**
        - Use `PascalCase`. Often prefixed with "On" but not strictly required.
        - Use `UnityEvent` or standard C# events (`event Action<...>`).
        - Examples: `OnActionsQueued`, `OnDamaged`, `GameStateChanged`, `OnCardDropped`.

    - **Private & Protected Fields:**
        - Use `camelCase` (without underscore prefix).
        - Examples: `actionsList`, `processedEffects`, `gameMediator`, `cardData`, `linkedCreature`.

    - **Local Variables & Method Parameters:**
        - Use `camelCase`.
        - Examples: `action`, `modifierData`, `targetSlot`, `eventData`, `damage`, `owner`.

    - **Constants (`const`, `static readonly`):**
        - Use `ALL_CAPS_SNAKE_CASE` for primitive types or well-known constant values.
        - Use `PascalCase` for `static readonly` objects (like `DefaultColors` dictionary).
        - Examples: `MAX_HAND_SIZE`, `MAX_SLOTS`.

    - **Enums Values:**
        - Use `PascalCase`.
        - Example: `EffectTrigger.OnPlay`, `TargetType.EnemyCreatures`, `LogTag.Initialization`.

    - **File Names:**
        - Match the name of the primary public type (class/interface/enum) defined within.
        - Use `PascalCase`.
        - Example: `ActionsQueue.cs`, `IPlayer.cs`, `LogTag.cs`.

    ---

    ## 2. Code Style & Formatting

    - **Braces `{}`:**
        - Use the **K&R (Kernighan & Ritchie) style variant common in C#**: Place the opening brace `{` on the *same line* as the declaration (namespace, class, method, property, control structure like `if`, `for`, `while`, `switch`, etc.). Place the closing brace `}` on its own new line, aligned with the start of the declaration line.
        - ```csharp
          public class ExampleClass {
              public void ExampleMethod(int value) {
                  if (value > 0) {
                      // Do something
                  } else {
                      // Do something else
                  }
              } // Closing brace for method
          } // Closing brace for class
          ```

    - **Spacing:**
        - Use a single space *before* flow control keywords (`if`, `for`, `while`, `switch`, etc.).
        - Use a single space *around* binary operators (`=`, `+`, `-`, `*`, `/`, `==`, `!=`, `=>`, etc.).
        - Use a single space *after* commas in argument lists and parameter lists.
        - Do *not* use spaces *after* opening or *before* closing parentheses `()`, brackets `[]`.
        - Do *not* use spaces *before* commas or semicolons.

    - **Indentation:**
        - Use 4 spaces for indentation (tabs configured to represent 4 spaces).

    - **Line Breaks:**
        - Keep lines reasonably short (aim for < 120 characters if possible, prioritize readability).
        - Break long lines *after* operators or commas, indenting the continuation logically.

    - **`using` Directives:**
        - Place all `using` directives at the top of the file, *outside* any namespace declaration.
        - Keep them sorted alphabetically (IDE can usually automate this).

    - **Access Modifiers:**
        - Always specify access modifiers explicitly (`public`, `private`, `protected`, `internal`). Default to `private` if unsure.
        - Order: `public` > `internal` > `protected` > `private`.

    - **`var` Keyword:**
        - Use `var` for local variables *only* when the assigned type is *obvious* from the right-hand side (e.g., `var player = new Player();`, `var dict = new Dictionary<string, int>();`).
        - Explicitly state the type if it enhances clarity or if the type is not immediately obvious (e.g., `int count = GetCountFromDatabase();`).

    - **`this` Keyword:**
        - Use `this` *only* when necessary to disambiguate between a local variable/parameter and a class field/property (typically in constructors or setters). Avoid unnecessary use.
        - Example: `this.gameMediator = gameMediator;`

    - **Comments:**
        - Use `///` XML documentation comments for all *public* types and members.
        - Use `//` for implementation comments within methods *only* where the logic is complex or non-obvious. Avoid commenting trivial code.

    - **Regions `#region`:**
        - Use sparingly. Prefer smaller, more focused classes.
        - If used, group logically related members (e.g., Fields, Properties, Constructors, Public Methods, Private Methods, Unity Lifecycle). Avoid deep nesting.

    ---

    ## 3. Unity Specifics

    - **`[SerializeField]`:**
        - Use `[SerializeField]` attribute to expose *private* fields to the Unity Inspector.
        - Name these private fields using `camelCase`.
        - Example: `[SerializeField] private Button resolveActionsButton;`

    - **Component Access:**
        - Cache component references in `Awake` or `Start` using `GetComponent<T>()`. Avoid repeated calls in `Update` or other frequent methods.
        - Use `TryGetComponent<T>(out var component)` when the component might be optional.

    - **Unity Event Handlers:**
        - Prefer creating private methods and assigning them in the Inspector or via code (`button.onClick.AddListener(OnMyButtonClick);`) rather than using lambda expressions directly in `AddListener` if the logic is more than one line or reusable.

    - **Coroutines:**
        - Name Coroutine methods using `PascalCase` and often ending with `Coroutine` (e.g., `InitializeAsyncCoroutine`).
        - Cache `WaitForSeconds` objects if used frequently in loops to avoid garbage collection: `private readonly WaitForSeconds shortDelay = new WaitForSeconds(0.1f);`

    ---

    ## 4. Tooling

    - **`.editorconfig`:**
        - Create and maintain an `.editorconfig` file in the project root.
        - Configure IDEs (Visual Studio, Rider, VS Code) to respect this file.
        - This helps automate formatting and enforce many style rules consistently.
    - **Analyzers:**
        - Consider using Roslyn analyzers (built into modern VS/Rider, available for VS Code) to enforce coding standards and identify potential issues during development.

    ---
    */
}
