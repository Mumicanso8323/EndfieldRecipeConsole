namespace EndfieldRecipe.Core.Domain;

public sealed class AppSettings {
    public bool HideInternalKeys { get; set; } = true;
    public int HistoryDepth { get; set; } = 30;
    public string ItemSortMode { get; set; } = "Value";
    public Dictionary<string, List<string>> Keymap { get; set; } = CreateDefaultKeymap();

    public static AppSettings CreateDefault() {
        return new AppSettings {
            HideInternalKeys = true,
            HistoryDepth = 30,
            ItemSortMode = "Value",
            Keymap = CreateDefaultKeymap()
        };
    }

    public static Dictionary<string, List<string>> CreateDefaultKeymap() {
        return new Dictionary<string, List<string>>(StringComparer.Ordinal) {
            ["Confirm"] = new List<string> { "Enter", "Space" },
            ["Back"] = new List<string> { "Escape" },
            ["DeleteChar"] = new List<string> { "Backspace" },
            ["MoveUp"] = new List<string> { "UpArrow", "W" },
            ["MoveDown"] = new List<string> { "DownArrow", "S" },
            ["MoveLeft"] = new List<string> { "LeftArrow" },
            ["MoveRight"] = new List<string> { "RightArrow" },
            ["PageUp"] = new List<string> { "PageUp" },
            ["PageDown"] = new List<string> { "PageDown" },
            ["JumpTop"] = new List<string> { "Home" },
            ["JumpBottom"] = new List<string> { "End" },
            ["SearchStart"] = new List<string> { "/" },
            ["Help"] = new List<string> { "?" },
            ["OpenResult"] = new List<string> { "R" },
            ["ToggleExpand"] = new List<string> { "T" },
            ["Add"] = new List<string> { "A" },
            ["Remove"] = new List<string> { "D" },
            ["Undo"] = new List<string> { "Ctrl+Z" },
            ["Redo"] = new List<string> { "Ctrl+Y", "Ctrl+Shift+Z" }
        };
    }
}
