namespace EndfieldRecipe.Tui;

public enum IntentKind {
    None,
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    PageUp,
    PageDown,
    Confirm,
    Toggle,
    Back,
    SearchStart,
    Help,
    TextInput,
    DeleteChar,
    Undo,
    Redo
}

public readonly record struct Intent(IntentKind Kind, char? Char = null);
