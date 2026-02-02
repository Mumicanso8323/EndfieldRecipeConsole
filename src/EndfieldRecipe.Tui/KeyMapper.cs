using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public static class KeyMapper {
    public static Intent Map(ConsoleKeyInfo key, AppSettings settings, bool preferTextInput = false) {
        if (preferTextInput && ShouldUseTextInput(key)) {
            if (!char.IsControl(key.KeyChar)) {
                return new Intent(IntentKind.TextInput, key.KeyChar);
            }
        }

        var map = BuildMap(settings);
        var token = KeyToken.FromConsoleKeyInfo(key);
        if (token != null && map.TryGetValue(token, out var kind)) {
            return new Intent(kind);
        }

        if (!char.IsControl(key.KeyChar)) {
            return new Intent(IntentKind.TextInput, key.KeyChar);
        }

        return new Intent(IntentKind.None);
    }

    private static Dictionary<string, IntentKind> BuildMap(AppSettings settings) {
        var map = new Dictionary<string, IntentKind>(StringComparer.OrdinalIgnoreCase);
        foreach (var intentName in IntentOrder) {
            if (!settings.Keymap.TryGetValue(intentName, out var tokens) || tokens == null) continue;
            if (!Enum.TryParse(intentName, ignoreCase: true, out IntentKind kind)) continue;
            foreach (var token in tokens) {
                if (string.IsNullOrWhiteSpace(token)) continue;
                map[token] = kind;
            }
        }

        return map;
    }

    private static bool ShouldUseTextInput(ConsoleKeyInfo key) {
        if ((key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0) {
            return false;
        }

        return key.Key switch {
            ConsoleKey.Enter => false,
            ConsoleKey.Escape => false,
            ConsoleKey.Backspace => false,
            ConsoleKey.UpArrow => false,
            ConsoleKey.DownArrow => false,
            ConsoleKey.LeftArrow => false,
            ConsoleKey.RightArrow => false,
            ConsoleKey.PageUp => false,
            ConsoleKey.PageDown => false,
            ConsoleKey.Home => false,
            ConsoleKey.End => false,
            _ => true
        };
    }

    private static readonly string[] IntentOrder = {
        nameof(IntentKind.MoveUp),
        nameof(IntentKind.MoveDown),
        nameof(IntentKind.MoveLeft),
        nameof(IntentKind.MoveRight),
        nameof(IntentKind.PageUp),
        nameof(IntentKind.PageDown),
        nameof(IntentKind.JumpTop),
        nameof(IntentKind.JumpBottom),
        nameof(IntentKind.Confirm),
        nameof(IntentKind.Back),
        nameof(IntentKind.DeleteChar),
        nameof(IntentKind.SearchStart),
        nameof(IntentKind.Help),
        nameof(IntentKind.OpenResult),
        nameof(IntentKind.ToggleExpand),
        nameof(IntentKind.Undo),
        nameof(IntentKind.Redo),
        nameof(IntentKind.Add),
        nameof(IntentKind.Remove)
    };
}
