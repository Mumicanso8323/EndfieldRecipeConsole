using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class ItemsScreen : IScreen, ITextEntryModeProvider {
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
    private int _draftValue;
    private bool _draftRaw;

    public string Title => UiText.ItemsTitle;
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

        var items = GetItems(screenContext, _searchQuery);
        if (_selected >= items.Count) _selected = Math.Max(0, items.Count - 1);
        RenderList(context, screenContext, items);
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        if (_searchMode) {
            return HandleSearch(intent, context);
        }

        if (_mode != EditMode.None) {
            return HandleEdit(intent, context);
        }

        var items = GetItems(context, _searchQuery);
        var page = Math.Max(1, context.BodyHeight);
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(Math.Max(0, items.Count - 1), _selected + 1);
                break;
            case IntentKind.PageUp:
                _selected = Math.Max(0, _selected - page);
                break;
            case IntentKind.PageDown:
                _selected = Math.Min(Math.Max(0, items.Count - 1), _selected + page);
                break;
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, items.Count - 1);
                break;
            case IntentKind.SearchStart:
                _searchMode = true;
                break;
            case IntentKind.Confirm:
                if (items.Count > 0) {
                    StartEdit(items[_selected]);
                }
                break;
            case IntentKind.Add:
                StartAdd();
                break;
            case IntentKind.Remove:
                StartDelete(items, context);
                break;
            case IntentKind.TextInput:
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context, items.Count);
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

        var items = GetItems(context, _searchQuery);
        if (_selected >= items.Count) _selected = Math.Max(0, items.Count - 1);
        ClampOffset(context, items.Count);
        return ScreenResult.None();
    }

    private ScreenResult HandleEdit(Intent intent, ScreenContext context) {
        switch (_mode) {
            case EditMode.Name:
                return HandleNameEdit(intent, context);
            case EditMode.Value:
                return HandleValueEdit(intent, context);
            case EditMode.Raw:
                return HandleRawEdit(intent, context);
            case EditMode.DeleteConfirm:
                return HandleDeleteConfirm(intent, context);
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleNameEdit(Intent intent, ScreenContext context) {
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
                _input = _draftValue.ToString();
                _mode = EditMode.Value;
                break;
            case IntentKind.Back:
                ResetEdit();
                break;
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleValueEdit(Intent intent, ScreenContext context) {
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
                if (!int.TryParse(_input, out var value)) {
                    _status = UiText.ErrorInvalidNumber;
                    return ScreenResult.None();
                }
                _draftValue = value;
                _mode = EditMode.Raw;
                break;
            case IntentKind.Back:
                ResetEdit();
                break;
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleRawEdit(Intent intent, ScreenContext context) {
        switch (intent.Kind) {
            case IntentKind.MoveLeft:
            case IntentKind.MoveRight:
                _draftRaw = !_draftRaw;
                break;
            case IntentKind.Confirm:
                SaveItem(context);
                break;
            case IntentKind.Back:
                ResetEdit();
                break;
        }

        return ScreenResult.None();
    }

    private ScreenResult HandleDeleteConfirm(Intent intent, ScreenContext context) {
        if (intent.Kind == IntentKind.TextInput && intent.Char is 'y' or 'Y') {
            if (_editingKey != null && IsItemReferenced(context, _editingKey)) {
                _status = UiText.ErrorDeleteInUse;
                ResetEdit();
                return ScreenResult.None();
            }

            if (_editingKey != null) {
                context.Data.Items.Remove(_editingKey);
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
        _draftValue = 0;
        _draftRaw = false;
        _input = string.Empty;
        _mode = EditMode.Name;
    }

    private void StartEdit(Item item) {
        _isNew = false;
        _editingKey = item.Key;
        _draftName = item.DisplayName;
        _draftValue = item.Value;
        _draftRaw = item.IsRawInput;
        _input = _draftName;
        _mode = EditMode.Name;
    }

    private void StartDelete(List<Item> items, ScreenContext context) {
        if (items.Count == 0) return;
        _editingKey = items[_selected].Key;
        _mode = EditMode.DeleteConfirm;
        _status = UiText.LabelDeleteConfirm;
    }

    private void SaveItem(ScreenContext context) {
        if (_isNew) {
            var key = Guid.NewGuid().ToString("N");
            context.Data.Items[key] = new Item {
                Key = key,
                DisplayName = _draftName,
                Value = _draftValue,
                IsRawInput = _draftRaw
            };
        } else if (_editingKey != null && context.Data.Items.TryGetValue(_editingKey, out var item)) {
            item.DisplayName = _draftName;
            item.Value = _draftValue;
            item.IsRawInput = _draftRaw;
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
            EditMode.Value => $"{UiText.LabelValueInput}: {_input}",
            EditMode.Raw => $"{UiText.LabelRawInput}: {(_draftRaw ? UiText.LabelYes : UiText.LabelNo)}",
            EditMode.DeleteConfirm => UiText.LabelDeleteConfirm,
            _ => string.Empty
        };
    }

    private static List<Item> GetItems(ScreenContext context, string query) {
        var items = context.Data.Items.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query)) {
            items = items.Where(i =>
                i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || i.Key.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var sortMode = context.Settings.ItemSortMode;
        if (string.Equals(sortMode, "Value", StringComparison.OrdinalIgnoreCase)) {
            return items
                .OrderByDescending(i => i.Value)
                .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return items
            .OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RenderList(IRenderContext context, ScreenContext screenContext, List<Item> items) {
        if (items.Count == 0) {
            context.WriteBodyLine(0, UiText.StatusEmpty);
            return;
        }

        ClampOffset(context, items.Count);
        var max = Math.Min(items.Count, _offset + context.BodyHeight);
        var line = 0;
        for (var i = _offset; i < max; i++) {
            var item = items[i];
            var prefix = i == _selected ? "> " : "  ";
            var rawFlag = item.IsRawInput ? $" {UiText.LabelRaw}" : string.Empty;
            var baseName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.Key : item.DisplayName;
            var name = screenContext.Settings.HideInternalKeys
                ? baseName
                : $"{baseName} ({item.Key})";
            context.WriteBodyLine(line, $"{prefix}{name} {UiText.LabelValue}:{item.Value}{rawFlag}");
            line++;
        }
    }

    private static bool IsItemReferenced(ScreenContext context, string key) {
        if (context.Data.NeedPlan.Targets.Any(t => t.ItemKey == key)) return true;
        return context.Data.Recipes.Any(r =>
            r.Inputs.Any(i => i.Key == key) || r.Outputs.Any(o => o.Key == key));
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
        Value,
        Raw,
        DeleteConfirm
    }
}
