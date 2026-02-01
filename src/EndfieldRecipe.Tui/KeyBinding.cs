namespace EndfieldRecipe.Tui;

public static class KeyBinding {
    public static Intent FromConsoleKey(ConsoleKeyInfo key) {
        if ((key.Modifiers & ConsoleModifiers.Control) != 0) {
            if (key.Key == ConsoleKey.Z && (key.Modifiers & ConsoleModifiers.Shift) != 0) {
                return new Intent(IntentKind.Redo);
            }

            return key.Key switch {
                ConsoleKey.Z => new Intent(IntentKind.Undo),
                ConsoleKey.Y => new Intent(IntentKind.Redo),
                _ => new Intent(IntentKind.None)
            };
        }

        return key.Key switch {
            ConsoleKey.UpArrow => new Intent(IntentKind.MoveUp),
            ConsoleKey.DownArrow => new Intent(IntentKind.MoveDown),
            ConsoleKey.LeftArrow => new Intent(IntentKind.MoveLeft),
            ConsoleKey.RightArrow => new Intent(IntentKind.MoveRight),
            ConsoleKey.PageUp => new Intent(IntentKind.PageUp),
            ConsoleKey.PageDown => new Intent(IntentKind.PageDown),
            ConsoleKey.Enter => new Intent(IntentKind.Confirm),
            ConsoleKey.Spacebar => new Intent(IntentKind.Toggle),
            ConsoleKey.Escape => new Intent(IntentKind.Back),
            ConsoleKey.Backspace => new Intent(IntentKind.DeleteChar),
            _ => FromChar(key.KeyChar)
        };
    }

    private static Intent FromChar(char c) {
        if (c == '/') return new Intent(IntentKind.SearchStart);
        if (c == '?') return new Intent(IntentKind.Help);
        if (char.IsControl(c)) return new Intent(IntentKind.None);
        return new Intent(IntentKind.TextInput, c);
    }
}
