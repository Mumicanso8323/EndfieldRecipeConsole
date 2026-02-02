using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class MachinePickerScreen : IScreen, ITextEntryModeProvider {
    private readonly Action<string> _onSelect;
    private int _selected;
    private int _offset;
    private bool _searchMode;
    private string _searchQuery = string.Empty;

    public MachinePickerScreen(Action<string> onSelect) {
        _onSelect = onSelect;
    }

    public string Title => UiText.MachinesTitle;
    public bool PreferTextInput => _searchMode;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        if (_searchMode) {
            view.Status = $"{UiText.LabelSearch}: {_searchQuery}";
        }

        var machines = GetMachines(screenContext, _searchQuery);
        if (_selected >= machines.Count) _selected = Math.Max(0, machines.Count - 1);

        if (machines.Count == 0) {
            context.WriteBodyLine(0, UiText.StatusEmpty);
            return;
        }

        ClampOffset(context, machines.Count);
        var max = Math.Min(machines.Count, _offset + context.BodyHeight);
        var line = 0;
        for (var i = _offset; i < max; i++) {
            var machine = machines[i];
            var prefix = i == _selected ? "> " : "  ";
            var baseName = string.IsNullOrWhiteSpace(machine.DisplayName) ? machine.Key : machine.DisplayName;
            var name = screenContext.Settings.HideInternalKeys
                ? baseName
                : $"{baseName} ({machine.Key})";
            context.WriteBodyLine(line, $"{prefix}{name}");
            line++;
        }
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        if (_searchMode) {
            return HandleSearch(intent, context);
        }

        var machines = GetMachines(context, _searchQuery);
        var page = Math.Max(1, context.BodyHeight);
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(Math.Max(0, machines.Count - 1), _selected + 1);
                break;
            case IntentKind.PageUp:
                _selected = Math.Max(0, _selected - page);
                break;
            case IntentKind.PageDown:
                _selected = Math.Min(Math.Max(0, machines.Count - 1), _selected + page);
                break;
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, machines.Count - 1);
                break;
            case IntentKind.SearchStart:
                _searchMode = true;
                break;
            case IntentKind.Confirm:
                if (machines.Count > 0) {
                    _onSelect(machines[_selected].Key);
                    return ScreenResult.Pop();
                }
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context, machines.Count);
        return ScreenResult.None();
    }

    private ScreenResult HandleSearch(Intent intent, ScreenContext context) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char.HasValue) {
                    _searchQuery += intent.Char.Value;
                }
                break;
            case IntentKind.DeleteChar:
                if (_searchQuery.Length > 0) {
                    _searchQuery = _searchQuery[..^1];
                }
                break;
            case IntentKind.Back:
                _searchQuery = string.Empty;
                _searchMode = false;
                break;
            case IntentKind.Confirm:
                _searchMode = false;
                break;
        }

        var machines = GetMachines(context, _searchQuery);
        if (_selected >= machines.Count) _selected = Math.Max(0, machines.Count - 1);
        ClampOffset(context, machines.Count);
        return ScreenResult.None();
    }

    private static List<Machine> GetMachines(ScreenContext context, string query) {
        var machines = context.Data.Machines.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query)) {
            machines = machines.Where(m =>
                m.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || m.Key.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return machines
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ClampOffset(IRenderContext context, int count) {
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + context.BodyHeight) {
            _offset = Math.Min(_selected, Math.Max(0, count - context.BodyHeight));
        }
    }
}
