using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class OptimizationInputScreen : IScreen, ITextEntryModeProvider {
    private int _selected;
    private int _offset;
    private bool _searchMode;
    private string _searchQuery = string.Empty;
    private string _status = string.Empty;
    private bool _inputMode;
    private string _input = string.Empty;
    private string? _pendingItemKey;

    public string Title => UiText.OptimizationTitle;
    public bool PreferTextInput => _searchMode || _inputMode;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        if (_searchMode) {
            view.Status = $"{UiText.LabelSearch}: {_searchQuery}";
        } else if (_inputMode) {
            view.Status = $"{UiText.LabelInputPerMinInput}: {_input}";
        } else if (!string.IsNullOrEmpty(_status)) {
            view.Status = _status;
            _status = string.Empty;
        }

        var supplies = GetSupplies(screenContext, _searchQuery);
        if (_selected >= supplies.Count) _selected = Math.Max(0, supplies.Count - 1);

        context.WriteBodyLine(0, $"{UiText.ColumnItem} | {UiText.LabelInputPerMinInput}");
        context.WriteBodyLine(1, new string('-', context.Width));

        if (supplies.Count == 0) {
            context.WriteBodyLine(2, UiText.StatusEmpty);
            return;
        }

        ClampOffset(context, supplies.Count);
        var bodyHeight = Math.Max(1, context.BodyHeight - 2);
        var max = Math.Min(supplies.Count, _offset + bodyHeight);
        var line = 2;
        for (var i = _offset; i < max; i++) {
            var entry = supplies[i];
            var prefix = i == _selected ? ">" : " ";
            var name = ResolveItemName(screenContext, entry.ItemKey);
            context.WriteBodyLine(line, $"{prefix} {name} | {entry.AmountPerMin:G29}");
            line++;
        }
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        if (_searchMode) {
            return HandleSearch(intent, context);
        }

        if (_inputMode) {
            return HandleInput(intent, context);
        }

        var supplies = GetSupplies(context, _searchQuery);
        var page = Math.Max(1, context.BodyHeight - 2);
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(Math.Max(0, supplies.Count - 1), _selected + 1);
                break;
            case IntentKind.PageUp:
                _selected = Math.Max(0, _selected - page);
                break;
            case IntentKind.PageDown:
                _selected = Math.Min(Math.Max(0, supplies.Count - 1), _selected + page);
                break;
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, supplies.Count - 1);
                break;
            case IntentKind.SearchStart:
                _searchMode = true;
                break;
            case IntentKind.Confirm:
                if (supplies.Count > 0) {
                    _pendingItemKey = supplies[_selected].ItemKey;
                    _input = supplies[_selected].AmountPerMin.ToString("G29");
                    _inputMode = true;
                }
                break;
            case IntentKind.Add:
                return StartAdd(context);
            case IntentKind.Remove:
                RemoveSelected(context, supplies);
                break;
            case IntentKind.OpenResult:
                return ScreenResult.Push(new OptimizationResultScreen());
            case IntentKind.TextInput:
                if (intent.Char is 'r' or 'R') {
                    return ScreenResult.Push(new OptimizationResultScreen());
                }
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context, supplies.Count);
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

        var supplies = GetSupplies(context, _searchQuery);
        if (_selected >= supplies.Count) _selected = Math.Max(0, supplies.Count - 1);
        ClampOffset(context, supplies.Count);
        return ScreenResult.None();
    }

    private ScreenResult HandleInput(Intent intent, ScreenContext context) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char is >= '0' and <= '9' or '.' or '-') {
                    _input += intent.Char.Value;
                }
                break;
            case IntentKind.DeleteChar:
                if (_input.Length > 0) {
                    _input = _input[..^1];
                }
                break;
            case IntentKind.Confirm:
                if (!decimal.TryParse(_input, out var value) || value < 0m) {
                    _status = UiText.ErrorInvalidNumber;
                    return ScreenResult.None();
                }
                CommitInput(context, value);
                break;
            case IntentKind.Back:
                _inputMode = false;
                _input = string.Empty;
                _pendingItemKey = null;
                break;
        }

        return ScreenResult.None();
    }

    private ScreenResult StartAdd(ScreenContext context) {
        return ScreenResult.Push(new ItemPickerScreen(key => {
            if (!context.Data.Items.ContainsKey(key)) {
                _status = UiText.ErrorItemNotFound;
                return;
            }

            _pendingItemKey = key;
            _input = "0";
            _inputMode = true;
        }));
    }

    private void CommitInput(ScreenContext context, decimal value) {
        if (string.IsNullOrWhiteSpace(_pendingItemKey)) {
            _inputMode = false;
            _input = string.Empty;
            return;
        }

        var entry = context.Data.Optimization.Supplies.FirstOrDefault(s =>
            string.Equals(s.ItemKey, _pendingItemKey, StringComparison.OrdinalIgnoreCase));
        if (entry == null) {
            context.Data.Optimization.Supplies.Add(new SupplyEntry {
                ItemKey = _pendingItemKey,
                AmountPerMin = value
            });
        } else {
            entry.AmountPerMin = value;
        }

        context.SaveData();
        _inputMode = false;
        _input = string.Empty;
        _pendingItemKey = null;
    }

    private void RemoveSelected(ScreenContext context, List<SupplyEntry> supplies) {
        if (supplies.Count == 0) return;
        var target = supplies[_selected];
        var actual = context.Data.Optimization.Supplies.FirstOrDefault(s =>
            string.Equals(s.ItemKey, target.ItemKey, StringComparison.OrdinalIgnoreCase));
        if (actual != null) {
            context.Data.Optimization.Supplies.Remove(actual);
            context.SaveData();
        }
    }

    private List<SupplyEntry> GetSupplies(ScreenContext context, string query) {
        var supplies = context.Data.Optimization.Supplies.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query)) {
            supplies = supplies.Where(s => ResolveItemName(context, s.ItemKey)
                .Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.ItemKey.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return supplies
            .OrderBy(s => ResolveItemName(context, s.ItemKey), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ClampOffset(IRenderContext context, int count) {
        var body = Math.Max(1, context.BodyHeight - 2);
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + body) {
            _offset = Math.Min(_selected, Math.Max(0, count - body));
        }
    }

    private string ResolveItemName(ScreenContext context, string key) {
        if (context.Data.Items.TryGetValue(key, out var item)) {
            var baseName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.Key : item.DisplayName;
            return context.Settings.HideInternalKeys ? baseName : $"{baseName} ({item.Key})";
        }
        return key;
    }

}
