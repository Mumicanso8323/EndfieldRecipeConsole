using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class RecipeEditorScreen : IScreen, ITextEntryModeProvider {
    private readonly string? _recipeId;
    private string _machineKey = string.Empty;
    private readonly List<RecipeEntry> _inputs = new();
    private readonly List<RecipeEntry> _outputs = new();
    private int _selected;
    private int _offset;
    private bool _inputMode;
    private string _input = string.Empty;
    private string _status = string.Empty;
    private StackKind _editingKind;
    private int _editingIndex = -1;

    public RecipeEditorScreen(string? recipeId) {
        _recipeId = recipeId;
    }

    public string Title => UiText.RecipesTitle;
    public bool PreferTextInput => _inputMode;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        if (_inputMode) {
            view.Status = $"{UiText.LabelAmountInput}: {_input}";
        } else if (!string.IsNullOrEmpty(_status)) {
            view.Status = _status;
            _status = string.Empty;
        }

        if (_inputs.Count == 0 && _outputs.Count == 0 && string.IsNullOrEmpty(_machineKey)) {
            LoadRecipe(screenContext);
        }

        var rows = BuildRows(screenContext);
        if (_selected >= rows.Count) _selected = Math.Max(0, rows.Count - 1);
        RenderRows(context, rows);
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        if (_inputMode) {
            return HandleAmountInput(intent);
        }

        var rows = BuildRows(context);
        var page = Math.Max(1, context.BodyHeight);
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(Math.Max(0, rows.Count - 1), _selected + 1);
                break;
            case IntentKind.PageUp:
                _selected = Math.Max(0, _selected - page);
                break;
            case IntentKind.PageDown:
                _selected = Math.Min(Math.Max(0, rows.Count - 1), _selected + page);
                break;
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, rows.Count - 1);
                break;
            case IntentKind.Confirm:
                return HandleConfirm(context, rows);
            case IntentKind.Remove:
                RemoveSelectedRow(rows);
                break;
            case IntentKind.TextInput:
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context, rows.Count);
        return ScreenResult.None();
    }

    private ScreenResult HandleConfirm(ScreenContext context, List<Row> rows) {
        if (rows.Count == 0) return ScreenResult.None();

        var row = rows[_selected];
        switch (row.Type) {
            case RowType.Machine:
                return ScreenResult.Push(new MachinePickerScreen(key => { _machineKey = key; }));
            case RowType.InputEntry:
                return StartItemEdit(row, StackKind.Input);
            case RowType.OutputEntry:
                return StartItemEdit(row, StackKind.Output);
            case RowType.AddInput:
                return StartItemAdd(StackKind.Input);
            case RowType.AddOutput:
                return StartItemAdd(StackKind.Output);
            case RowType.Save:
                if (!SaveRecipe(context)) {
                    _status = UiText.ErrorRecipeInvalid;
                    return ScreenResult.None();
                }
                context.SaveData();
                return ScreenResult.Pop();
            default:
                return ScreenResult.None();
        }
    }

    private ScreenResult StartItemEdit(Row row, StackKind kind) {
        _editingKind = kind;
        _editingIndex = row.Index;
        return ScreenResult.Push(new ItemPickerScreen(key => {
            var list = kind == StackKind.Input ? _inputs : _outputs;
            if (_editingIndex >= 0 && _editingIndex < list.Count) {
                list[_editingIndex].Key = key;
                _inputMode = true;
                _input = list[_editingIndex].Amount.ToString("G29");
            }
        }));
    }

    private ScreenResult StartItemAdd(StackKind kind) {
        _editingKind = kind;
        return ScreenResult.Push(new ItemPickerScreen(key => {
            var list = kind == StackKind.Input ? _inputs : _outputs;
            list.Add(new RecipeEntry { Key = key, Amount = 1m });
            _editingIndex = list.Count - 1;
            _inputMode = true;
            _input = "1";
        }));
    }

    private ScreenResult HandleAmountInput(Intent intent) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char is >= '0' and <= '9' or '.' or '-') {
                    _input += intent.Char.Value;
                }
                break;
            case IntentKind.DeleteChar:
                if (_input.Length > 0) _input = _input[..^1];
                break;
            case IntentKind.Confirm:
                if (!decimal.TryParse(_input, out var value) || value <= 0m) {
                    _status = UiText.ErrorInvalidNumber;
                } else {
                    var list = _editingKind == StackKind.Input ? _inputs : _outputs;
                    if (_editingIndex >= 0 && _editingIndex < list.Count) {
                        list[_editingIndex].Amount = value;
                    }
                }
                _inputMode = false;
                _input = string.Empty;
                break;
            case IntentKind.Back:
                _inputMode = false;
                _input = string.Empty;
                break;
        }

        return ScreenResult.None();
    }

    private void RemoveSelectedRow(List<Row> rows) {
        if (rows.Count == 0) return;
        var row = rows[_selected];
        switch (row.Type) {
            case RowType.InputEntry:
                if (row.Index >= 0 && row.Index < _inputs.Count) _inputs.RemoveAt(row.Index);
                break;
            case RowType.OutputEntry:
                if (row.Index >= 0 && row.Index < _outputs.Count) _outputs.RemoveAt(row.Index);
                break;
        }
    }

    private bool SaveRecipe(ScreenContext context) {
        if (string.IsNullOrWhiteSpace(_machineKey)) return false;
        if (!context.Data.Machines.ContainsKey(_machineKey)) return false;
        if (_inputs.Count == 0 || _outputs.Count == 0) return false;
        if (_inputs.Any(i => i.Amount <= 0m) || _outputs.Any(o => o.Amount <= 0m)) return false;
        if (_inputs.Any(i => !context.Data.Items.ContainsKey(i.Key))) return false;
        if (_outputs.Any(o => !context.Data.Items.ContainsKey(o.Key))) return false;

        if (_recipeId == null) {
            var id = Guid.NewGuid().ToString("N");
            context.Data.Recipes.Add(new Recipe {
                Id = id,
                MachineKey = _machineKey,
                Inputs = _inputs.Select(i => new ItemStack(i.Key, i.Amount)).ToList(),
                Outputs = _outputs.Select(o => new ItemStack(o.Key, o.Amount)).ToList()
            });
        } else {
            var recipe = context.Data.Recipes.FirstOrDefault(r => r.Id == _recipeId);
            if (recipe == null) return false;
            recipe.MachineKey = _machineKey;
            recipe.Inputs = _inputs.Select(i => new ItemStack(i.Key, i.Amount)).ToList();
            recipe.Outputs = _outputs.Select(o => new ItemStack(o.Key, o.Amount)).ToList();
        }

        return true;
    }

    private List<Row> BuildRows(ScreenContext context) {
        var rows = new List<Row> { new(RowType.Machine, -1, BuildMachineLabel(context)) };

        for (var i = 0; i < _inputs.Count; i++) {
            var input = _inputs[i];
            rows.Add(new Row(RowType.InputEntry, i, $"{UiText.LabelIn}:{ResolveItemName(context, input.Key)} x{input.Amount:G29}"));
        }
        rows.Add(new Row(RowType.AddInput, -1, UiText.LabelAddInput));

        for (var i = 0; i < _outputs.Count; i++) {
            var output = _outputs[i];
            rows.Add(new Row(RowType.OutputEntry, i, $"{UiText.LabelOut}:{ResolveItemName(context, output.Key)} x{output.Amount:G29}"));
        }
        rows.Add(new Row(RowType.AddOutput, -1, UiText.LabelAddOutput));

        rows.Add(new Row(RowType.Save, -1, UiText.LabelSave));
        return rows;
    }

    private void RenderRows(IRenderContext context, List<Row> rows) {
        if (rows.Count == 0) {
            context.WriteBodyLine(0, UiText.StatusEmpty);
            return;
        }

        ClampOffset(context, rows.Count);
        var max = Math.Min(rows.Count, _offset + context.BodyHeight);
        var line = 0;
        for (var i = _offset; i < max; i++) {
            var row = rows[i];
            var prefix = i == _selected ? "> " : "  ";
            context.WriteBodyLine(line, $"{prefix}{row.Label}");
            line++;
        }
    }

    private string BuildMachineLabel(ScreenContext context) {
        var name = string.IsNullOrWhiteSpace(_machineKey)
            ? UiText.LabelNotSet
            : ResolveMachineName(context, _machineKey);
        return $"{UiText.LabelMachine}:{name}";
    }

    private static string ResolveItemName(ScreenContext context, string key) {
        if (context.Data.Items.TryGetValue(key, out var item)) {
            var baseName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.Key : item.DisplayName;
            return context.Settings.HideInternalKeys ? baseName : $"{baseName} ({item.Key})";
        }
        return key;
    }

    private static string ResolveMachineName(ScreenContext context, string key) {
        if (context.Data.Machines.TryGetValue(key, out var machine)) {
            var name = string.IsNullOrWhiteSpace(machine.DisplayName) ? machine.Key : machine.DisplayName;
            return context.Settings.HideInternalKeys ? name : $"{name} ({machine.Key})";
        }
        return key;
    }

    private void LoadRecipe(ScreenContext context) {
        if (_recipeId == null) return;
        var recipe = context.Data.Recipes.FirstOrDefault(r => r.Id == _recipeId);
        if (recipe == null) return;
        _machineKey = recipe.MachineKey;
        _inputs.Clear();
        _inputs.AddRange(recipe.Inputs.Select(i => new RecipeEntry { Key = i.Key, Amount = i.Amount }));
        _outputs.Clear();
        _outputs.AddRange(recipe.Outputs.Select(o => new RecipeEntry { Key = o.Key, Amount = o.Amount }));
    }

    private void ClampOffset(IRenderContext context, int count) {
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + context.BodyHeight) {
            _offset = Math.Min(_selected, Math.Max(0, count - context.BodyHeight));
        }
    }

    private sealed record Row(RowType Type, int Index, string Label);

    private sealed class RecipeEntry {
        public string Key { get; set; } = "";
        public decimal Amount { get; set; }
    }

    private enum RowType {
        Machine,
        InputEntry,
        OutputEntry,
        AddInput,
        AddOutput,
        Save
    }

    private enum StackKind {
        Input,
        Output
    }
}
