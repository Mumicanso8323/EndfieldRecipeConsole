namespace EndfieldRecipe.Tui;

public enum IntentKind {
    None,
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    PageUp,
    PageDown,
    JumpTop,
    JumpBottom,
    Confirm,
    Toggle,
    OpenResult,
    ToggleExpand,
    Add,
    Remove,
    Back,
    SearchStart,
    Help,
    TextInput,
    DeleteChar,
    Undo,
    Redo
}

public readonly record struct Intent(IntentKind Kind, char? Char = null);
