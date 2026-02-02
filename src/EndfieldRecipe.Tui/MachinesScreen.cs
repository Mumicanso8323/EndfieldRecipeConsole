using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class MachinesScreen : IScreen, ITextEntryModeProvider {
    private int _selected;
    private int _offset;
    private bool _searchMode;
    private string _searchQuery = string.Empty;
    private string _status = string.Empty;

    private EditMode _mode = EditMode.None;
    private bool _isNew;
    private string? _editingKey;
    private string _input = string.Empty;
    private string _draftName = string.Empty;
    private int _draftSeconds = 1;
    private int _draftInputTypes;
    private int _draftOutputTypes;

    public string Title => UiText.MachinesTitle;
    public bool PreferTextInput => _searchMode || _mode != EditMode.None;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        if (_searchMode) {
            view.Status = $"{UiText.LabelSearch}: {_searchQuery}";
        } else if (_mode != EditMode.None) {
            if (!string.IsNullOrEmpty(_status)) {
                view.Status = _status;
                _status = string.Empty;
            } else {
                view.Status = BuildEditStatus();
            }
        } else if (!string.IsNullOrEmpty(_status)) {
            view.Status = _status;
            _status = string.Empty;
        }

        var machines = GetMachines(screenContext, _searchQuery);
        if (_selected >= machines.Count) _selected = Math.Max(0, machines.Count - 1);
        RenderList(context, screenContext, machines);
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        if (_searchMode) {
            return HandleSearch(intent, context);
        }

        if (_mode != EditMode.None) {
            return HandleEdit(intent, context);
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
                    StartEdit(machines[_selected]);
                }
                break;
            case IntentKind.Add:
                StartAdd();
                break;
            case IntentKind.Remove:
                StartDelete(machines);
                break;
            case IntentKind.TextInput:
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

    private ScreenResult HandleEdit(Intent intent, ScreenContext context) {
        switch (_mode) {
            case EditMode.Name:
                return HandleNameEdit(intent);
            case EditMode.Seconds:
                return HandleSecondsEdit(intent);
            case EditMode.InputTypes:
                return HandleInputTypesEdit(intent);
            case EditMode.OutputTypes:
                return HandleOutputTypesEdit(intent, context);
            case EditMode.DeleteConfirm:
                return HandleDeleteConfirm(intent, context);
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleNameEdit(Intent intent) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char.HasValue) _input += intent.Char.Value;
                break;
            case IntentKind.DeleteChar:
                if (_input.Length > 0) _input = _input[..^1];
                break;
            case IntentKind.Confirm:
                var name = _input.Trim();
                if (string.IsNullOrWhiteSpace(name)) {
                    _status = UiText.ErrorNameRequired;
                    return ScreenResult.None();
                }
                _draftName = name;
                _input = _draftSeconds.ToString();
                _mode = EditMode.Seconds;
                break;
            case IntentKind.Back:
                ResetEdit();
                break;
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleSecondsEdit(Intent intent) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char is >= '0' and <= '9') {
                    _input += intent.Char.Value;
                }
                break;
            case IntentKind.DeleteChar:
                if (_input.Length > 0) _input = _input[..^1];
                break;
            case IntentKind.Confirm:
                if (!int.TryParse(_input, out var seconds) || seconds <= 0) {
                    _status = UiText.ErrorInvalidNumber;
                    return ScreenResult.None();
                }
                _draftSeconds = seconds;
                _input = _draftInputTypes.ToString();
                _mode = EditMode.InputTypes;
                break;
            case IntentKind.Back:
                ResetEdit();
                break;
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleInputTypesEdit(Intent intent) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char is >= '0' and <= '9') {
                    _input += intent.Char.Value;
                }
                break;
            case IntentKind.DeleteChar:
                if (_input.Length > 0) _input = _input[..^1];
                break;
            case IntentKind.Confirm:
                if (!int.TryParse(_input, out var count) || count < 0) {
                    _status = UiText.ErrorInvalidNumber;
                    return ScreenResult.None();
                }
                _draftInputTypes = count;
                _input = _draftOutputTypes.ToString();
                _mode = EditMode.OutputTypes;
                break;
            case IntentKind.Back:
                ResetEdit();
                break;
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleOutputTypesEdit(Intent intent, ScreenContext context) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char is >= '0' and <= '9') {
                    _input += intent.Char.Value;
                }
                break;
            case IntentKind.DeleteChar:
                if (_input.Length > 0) _input = _input[..^1];
                break;
            case IntentKind.Confirm:
                if (!int.TryParse(_input, out var count) || count < 0) {
                    _status = UiText.ErrorInvalidNumber;
                    return ScreenResult.None();
                }
                _draftOutputTypes = count;
                SaveMachine(context);
                break;
            case IntentKind.Back:
                ResetEdit();
                break;
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleDeleteConfirm(Intent intent, ScreenContext context) {
        if (intent.Kind == IntentKind.TextInput && intent.Char is 'y' or 'Y') {
            if (_editingKey != null && IsMachineReferenced(context, _editingKey)) {
                _status = UiText.ErrorDeleteInUse;
                ResetEdit();
                return ScreenResult.None();
            }

            if (_editingKey != null) {
                context.Data.Machines.Remove(_editingKey);
                context.SaveData();
            }
            ResetEdit();
        } else if (intent.Kind == IntentKind.TextInput && intent.Char is 'n' or 'N') {
            ResetEdit();
        } else if (intent.Kind == IntentKind.Back) {
            ResetEdit();
        }

        return ScreenResult.None();
    }

    private void StartAdd() {
        _isNew = true;
        _editingKey = null;
        _draftName = string.Empty;
        _draftSeconds = 1;
        _draftInputTypes = 0;
        _draftOutputTypes = 0;
        _input = string.Empty;
        _mode = EditMode.Name;
    }

    private void StartEdit(Machine machine) {
        _isNew = false;
        _editingKey = machine.Key;
        _draftName = machine.DisplayName;
        _draftSeconds = Math.Max(1, machine.Seconds);
        _draftInputTypes = Math.Max(0, machine.InputTypeCount);
        _draftOutputTypes = Math.Max(0, machine.OutputTypeCount);
        _input = _draftName;
        _mode = EditMode.Name;
    }

    private void StartDelete(List<Machine> machines) {
        if (machines.Count == 0) return;
        _editingKey = machines[_selected].Key;
        _mode = EditMode.DeleteConfirm;
        _status = UiText.LabelDeleteConfirm;
    }

    private void SaveMachine(ScreenContext context) {
        if (_isNew) {
            var key = Guid.NewGuid().ToString("N");
            context.Data.Machines[key] = new Machine {
                Key = key,
                DisplayName = _draftName,
                Seconds = _draftSeconds,
                InputTypeCount = _draftInputTypes,
                OutputTypeCount = _draftOutputTypes
            };
        } else if (_editingKey != null && context.Data.Machines.TryGetValue(_editingKey, out var machine)) {
            machine.DisplayName = _draftName;
            machine.Seconds = _draftSeconds;
            machine.InputTypeCount = _draftInputTypes;
            machine.OutputTypeCount = _draftOutputTypes;
        }

        context.SaveData();
        ResetEdit();
    }

    private void ResetEdit() {
        _mode = EditMode.None;
        _input = string.Empty;
        _editingKey = null;
        _isNew = false;
    }

    private string BuildEditStatus() {
        return _mode switch {
            EditMode.Name => $"{UiText.LabelNameInput}: {_input}",
            EditMode.Seconds => $"{UiText.LabelSecondsInput}: {_input}",
            EditMode.InputTypes => $"{UiText.LabelInputTypeCountInput}: {_input}",
            EditMode.OutputTypes => $"{UiText.LabelOutputTypeCountInput}: {_input}",
            EditMode.DeleteConfirm => UiText.LabelDeleteConfirm,
            _ => string.Empty
        };
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

    private void RenderList(IRenderContext context, ScreenContext screenContext, List<Machine> machines) {
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

    private static bool IsMachineReferenced(ScreenContext context, string key) {
        return context.Data.Recipes.Any(r => r.MachineKey == key);
    }

    private void ClampOffset(IRenderContext context, int count) {
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + context.BodyHeight) {
            _offset = Math.Min(_selected, Math.Max(0, count - context.BodyHeight));
        }
    }

    private enum EditMode {
        None,
        Name,
        Seconds,
        InputTypes,
        OutputTypes,
        DeleteConfirm
    }
}
