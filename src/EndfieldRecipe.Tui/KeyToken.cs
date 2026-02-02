namespace EndfieldRecipe.Tui;

public static class KeyToken {
    public static string? FromConsoleKeyInfo(ConsoleKeyInfo key) {
        if ((key.Modifiers & ConsoleModifiers.Alt) != 0) return null;

        var hasCtrl = (key.Modifiers & ConsoleModifiers.Control) != 0;
        var hasShift = (key.Modifiers & ConsoleModifiers.Shift) != 0;

        if (hasCtrl) {
            if (!IsLetterKey(key.Key)) return null;
            return hasShift ? $"Ctrl+Shift+{key.Key}" : $"Ctrl+{key.Key}";
        }

        if (key.Key == ConsoleKey.Enter) return "Enter";
        if (key.Key == ConsoleKey.Spacebar) return "Space";

        if (IsLetterKey(key.Key)) return key.Key.ToString();

        if (key.KeyChar is '/' or '?') return key.KeyChar.ToString();

        return key.Key switch {
            ConsoleKey.UpArrow => nameof(ConsoleKey.UpArrow),
            ConsoleKey.DownArrow => nameof(ConsoleKey.DownArrow),
            ConsoleKey.LeftArrow => nameof(ConsoleKey.LeftArrow),
            ConsoleKey.RightArrow => nameof(ConsoleKey.RightArrow),
            ConsoleKey.PageUp => nameof(ConsoleKey.PageUp),
            ConsoleKey.PageDown => nameof(ConsoleKey.PageDown),
            ConsoleKey.Home => nameof(ConsoleKey.Home),
            ConsoleKey.End => nameof(ConsoleKey.End),
            ConsoleKey.Escape => nameof(ConsoleKey.Escape),
            ConsoleKey.Backspace => nameof(ConsoleKey.Backspace),
            _ => null
        };
    }

    private static bool IsLetterKey(ConsoleKey key) {
        return key >= ConsoleKey.A && key <= ConsoleKey.Z;
    }
}
