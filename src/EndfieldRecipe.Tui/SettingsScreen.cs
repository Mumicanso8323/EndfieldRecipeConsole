namespace EndfieldRecipe.Tui;

public sealed class SettingsScreen : IScreen, ITextEntryModeProvider {
    private int _selected;
    private bool _editingDepth;
    private string _input = string.Empty;

    public string Title => UiText.SettingsTitle;
    public bool PreferTextInput => _editingDepth;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        if (_editingDepth) {
            view.Status = $"{UiText.LabelHistoryDepthInput}: {_input}";
        }

        var items = BuildItems(screenContext);
        for (var i = 0; i < items.Length && i < context.BodyHeight; i++) {
            var prefix = i == _selected ? "> " : "  ";
            context.WriteBodyLine(i, prefix + items[i]);
        }
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        if (_editingDepth) {
            return HandleDepthEdit(intent, context);
        }

        var items = BuildItems(context);
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(items.Length - 1, _selected + 1);
                break;
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, items.Length - 1);
                break;
            case IntentKind.Confirm:
                return HandleConfirm(context);
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleConfirm(ScreenContext context) {
        switch (_selected) {
            case 0:
                context.Settings.HideInternalKeys = !context.Settings.HideInternalKeys;
                context.SaveSettings();
                break;
            case 1:
                _editingDepth = true;
                _input = context.Settings.HistoryDepth.ToString();
                break;
            case 2:
                context.Settings.ItemSortMode = string.Equals(context.Settings.ItemSortMode, "Value", StringComparison.OrdinalIgnoreCase)
                    ? "Name"
                    : "Value";
                context.SaveSettings();
                break;
            case 3:
                return ScreenResult.Push(new KeymapEditorScreen());
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleDepthEdit(Intent intent, ScreenContext context) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char is >= '0' and <= '9') {
                    _input += intent.Char.Value;
                }
                break;
            case IntentKind.DeleteChar:
                if (_input.Length > 0) {
                    _input = _input[..^1];
                }
                break;
            case IntentKind.Confirm:
                if (int.TryParse(_input, out var value)) {
                    value = Math.Clamp(value, 1, 200);
                    context.Settings.HistoryDepth = value;
                    context.History.ResetDepth(value);
                    context.SaveSettings();
                }
                _editingDepth = false;
                _input = string.Empty;
                break;
            case IntentKind.Back:
                _editingDepth = false;
                _input = string.Empty;
                break;
        }

        return ScreenResult.None();
    }

    private static string[] BuildItems(ScreenContext context) {
        var hideLabel = context.Settings.HideInternalKeys ? UiText.LabelOn : UiText.LabelOff;
        var sortLabel = string.Equals(context.Settings.ItemSortMode, "Value", StringComparison.OrdinalIgnoreCase)
            ? UiText.LabelSortValue
            : UiText.LabelSortName;

        return new[] {
            $"{UiText.LabelHideInternalKeys}: {hideLabel}",
            $"{UiText.LabelHistoryDepth}: {context.Settings.HistoryDepth}",
            $"{UiText.LabelItemSort}: {sortLabel}",
            UiText.KeymapTitle
        };
    }
}
