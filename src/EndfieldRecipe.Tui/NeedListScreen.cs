using EndfieldRecipe.Core.Application;
using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class NeedListScreen : IScreen, ITextEntryModeProvider {
    private int _selected;
    private int _offset;
    private InputMode _mode = InputMode.None;
    private string _input = string.Empty;
    private string? _pendingItemKey;
    private string _status = string.Empty;

    public string Title => UiText.NeedTitle;
    public bool PreferTextInput => _mode != InputMode.None;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        view.Status = _status;
        _status = string.Empty;

        var targets = GetTargets(screenContext);
        RenderTable(context, screenContext, targets);

        if (_mode != InputMode.None) {
            view.Status = $"{UiText.InputLineCount}: {_input}";
        }
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        var targets = GetTargets(context);

        if (intent.Kind == IntentKind.Undo) {
            context.History.Undo(context.Data.NeedPlan);
            context.SaveData();
            return ScreenResult.None();
        }

        if (intent.Kind == IntentKind.Redo) {
            context.History.Redo(context.Data.NeedPlan);
            context.SaveData();
            return ScreenResult.None();
        }

        if (_mode != InputMode.None) {
            return HandleInputMode(intent, context);
        }

        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(Math.Max(0, targets.Count - 1), _selected + 1);
                break;
            case IntentKind.Confirm:
                if (targets.Count > 0) {
                    _input = targets[_selected].LineCount.ToString();
                    _mode = InputMode.EditLine;
                }
                break;
            case IntentKind.Add:
                return ScreenResult.Push(new ItemPickerScreen(key => {
                    _pendingItemKey = key;
                    _mode = InputMode.AddLine;
                    _input = string.Empty;
                }));
            case IntentKind.Remove:
                RemoveSelected(context, targets);
                break;
            case IntentKind.OpenResult:
                return ScreenResult.Push(new NeedResultScreen());
            case IntentKind.TextInput:
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, targets.Count - 1);
                break;
        }

        ClampOffset(context, targets.Count);
        return ScreenResult.None();
    }

    private ScreenResult HandleInputMode(Intent intent, ScreenContext context) {
        switch (intent.Kind) {
            case IntentKind.TextInput:
                if (intent.Char.HasValue) {
                    _input += intent.Char.Value;
                }
                return ScreenResult.None();
            case IntentKind.DeleteChar:
                if (_input.Length > 0) {
                    _input = _input[..^1];
                }
                return ScreenResult.None();
            case IntentKind.Confirm:
                return CommitInput(context);
            case IntentKind.Back:
                _mode = InputMode.None;
                _input = string.Empty;
                _pendingItemKey = null;
                return ScreenResult.None();
            default:
                return ScreenResult.None();
        }
    }

    private ScreenResult CommitInput(ScreenContext context) {
        if (!int.TryParse(_input.Trim(), out var lineCount) || lineCount <= 0) {
            _status = UiText.ErrorLineCount;
            return ScreenResult.None();
        }

        if (_mode == InputMode.EditLine) {
            var targets = context.Data.NeedPlan.Targets;
            if (_selected >= 0 && _selected < targets.Count) {
                var itemKey = targets[_selected].ItemKey;
                context.History.Execute(context.Data.NeedPlan, new SetLineCountCommand(itemKey, lineCount));
                context.SaveData();
            }
        } else if (_mode == InputMode.AddLine && _pendingItemKey != null) {
            var existingIndex = context.Data.NeedPlan.Targets
                .FindIndex(t => string.Equals(t.ItemKey, _pendingItemKey, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0) {
                _selected = existingIndex;
                _input = context.Data.NeedPlan.Targets[existingIndex].LineCount.ToString();
                _mode = InputMode.EditLine;
                _pendingItemKey = null;
                return ScreenResult.None();
            }

            context.History.Execute(context.Data.NeedPlan, new AddTargetCommand(_pendingItemKey, lineCount));
            context.SaveData();
            _selected = context.Data.NeedPlan.Targets.Count - 1;
        }

        _mode = InputMode.None;
        _input = string.Empty;
        _pendingItemKey = null;
        return ScreenResult.None();
    }

    private void RemoveSelected(ScreenContext context, List<NeedTarget> targets) {
        if (targets.Count == 0) return;
        var itemKey = targets[_selected].ItemKey;
        context.History.Execute(context.Data.NeedPlan, new RemoveTargetCommand(itemKey));
        context.SaveData();
        _selected = Math.Min(_selected, Math.Max(0, context.Data.NeedPlan.Targets.Count - 1));
    }

    private static List<NeedTarget> GetTargets(ScreenContext context) {
        return context.Data.NeedPlan.Targets
            .OrderBy(t => t.ItemKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RenderTable(IRenderContext context, ScreenContext screenContext, List<NeedTarget> targets) {
        var header = $"{UiText.ColumnItem} | {UiText.ColumnLines} | {UiText.ColumnFlow}";
        context.WriteBodyLine(0, header);
        context.WriteBodyLine(1, new string('-', context.Width));

        if (targets.Count == 0) {
            context.WriteBodyLine(2, UiText.StatusEmpty);
            return;
        }

        ClampOffset(context, targets.Count);
        var line = 2;
        var max = Math.Min(targets.Count, _offset + (context.BodyHeight - 2));
        for (var i = _offset; i < max; i++) {
            var target = targets[i];
            var name = ResolveItemName(screenContext, target.ItemKey);
            var flow = FormatFlow(screenContext, target);
            var prefix = i == _selected ? ">" : " ";
            context.WriteBodyLine(line, $"{prefix} {name} | {target.LineCount} | {flow}");
            line++;
        }
    }

    private string FormatFlow(ScreenContext context, NeedTarget target) {
        return FlowCalculator.GetFlowPerMin(context.Data, target.ItemKey, target.LineCount).ToString("G29");
    }

    private string ResolveItemName(ScreenContext context, string key) {
        var name = FlowCalculator.ResolveItemName(context.Data, key);
        if (string.IsNullOrWhiteSpace(name)) name = key;
        if (!context.Settings.HideInternalKeys) {
            name = $"{name} ({key})";
        }
        return name;
    }

    private void ClampOffset(IRenderContext context, int count) {
        if (_selected < _offset) _offset = _selected;
        var body = Math.Max(1, context.BodyHeight - 2);
        if (_selected >= _offset + body) {
            _offset = Math.Min(_selected, Math.Max(0, count - body));
        }
    }

    private enum InputMode {
        None,
        AddLine,
        EditLine
    }
}
