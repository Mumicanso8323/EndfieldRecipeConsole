using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class KeymapEditorScreen : IScreen {
    private static readonly IntentKind[] IntentList = {
        IntentKind.Confirm,
        IntentKind.Back,
        IntentKind.DeleteChar,
        IntentKind.Add,
        IntentKind.Remove,
        IntentKind.MoveUp,
        IntentKind.MoveDown,
        IntentKind.MoveLeft,
        IntentKind.MoveRight,
        IntentKind.PageUp,
        IntentKind.PageDown,
        IntentKind.JumpTop,
        IntentKind.JumpBottom,
        IntentKind.SearchStart,
        IntentKind.Help,
        IntentKind.OpenResult,
        IntentKind.ToggleExpand,
        IntentKind.Undo,
        IntentKind.Redo
    };

    private int _selected;
    private int _offset;
    private bool _captureMode;
    private string _status = string.Empty;

    public string Title => UiText.KeymapTitle;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        if (_captureMode) {
            view.Status = UiText.KeymapCapture;
        } else if (!string.IsNullOrEmpty(_status)) {
            view.Status = _status;
            _status = string.Empty;
        }

        ClampOffset(context);
        var max = Math.Min(IntentList.Length, _offset + context.BodyHeight);
        var line = 0;
        for (var i = _offset; i < max; i++) {
            var kind = IntentList[i];
            var label = UiText.GetIntentLabel(kind);
            var bindings = GetBindings(screenContext.Settings, kind);
            var bindingText = bindings.Count > 0 ? string.Join(", ", bindings) : UiText.LabelNotSet;
            var prefix = i == _selected ? "> " : "  ";
            context.WriteBodyLine(line, $"{prefix}{label}: {bindingText}");
            line++;
        }
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        if (_captureMode) {
            return HandleCapture(context);
        }

        if (IsCommandKey(context, "D")) {
            RemoveBinding(context);
            return ScreenResult.None();
        }

        if (IsCommandKey(context, "X")) {
            ResetBinding(context);
            return ScreenResult.None();
        }

        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(IntentList.Length - 1, _selected + 1);
                break;
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, IntentList.Length - 1);
                break;
            case IntentKind.Confirm:
                _captureMode = true;
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context);
        return ScreenResult.None();
    }

    private ScreenResult HandleCapture(ScreenContext context) {
        var token = KeyToken.FromConsoleKeyInfo(context.LastKeyInfo);
        if (string.IsNullOrWhiteSpace(token)) {
            _status = UiText.KeymapInvalid;
            _captureMode = false;
            return ScreenResult.None();
        }

        var intent = IntentList[_selected];
        var intentName = intent.ToString();
        var settings = context.Settings;
        settings.Keymap ??= new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entry in settings.Keymap) {
            if (string.Equals(entry.Key, intentName, StringComparison.OrdinalIgnoreCase)) continue;
            entry.Value?.RemoveAll(value => string.Equals(value, token, StringComparison.OrdinalIgnoreCase));
        }

        var list = GetBindings(settings, intent);
        if (!list.Any(value => string.Equals(value, token, StringComparison.OrdinalIgnoreCase))) {
            list.Add(token);
        }

        settings.Keymap[intentName] = list;
        context.SaveSettings();
        _captureMode = false;
        return ScreenResult.None();
    }

    private void RemoveBinding(ScreenContext context) {
        var intent = IntentList[_selected];
        var list = GetBindings(context.Settings, intent);
        if (list.Count == 0) {
            _status = UiText.KeymapNoBinding;
            return;
        }

        list.RemoveAt(list.Count - 1);
        context.Settings.Keymap[intent.ToString()] = list;
        context.SaveSettings();
    }

    private void ResetBinding(ScreenContext context) {
        var intent = IntentList[_selected];
        var defaults = AppSettings.CreateDefaultKeymap();
        if (defaults.TryGetValue(intent.ToString(), out var list)) {
            context.Settings.Keymap[intent.ToString()] = new List<string>(list);
            context.SaveSettings();
        }
    }

    private static List<string> GetBindings(AppSettings settings, IntentKind intent) {
        settings.Keymap ??= new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var key = intent.ToString();
        if (!settings.Keymap.TryGetValue(key, out var list) || list == null) {
            list = new List<string>();
            settings.Keymap[key] = list;
        }

        return list;
    }

    private void ClampOffset(IRenderContext context) {
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + context.BodyHeight) {
            _offset = Math.Min(_selected, Math.Max(0, IntentList.Length - context.BodyHeight));
        }
    }

    private static bool IsCommandKey(ScreenContext context, string token) {
        return string.Equals(KeyToken.FromConsoleKeyInfo(context.LastKeyInfo), token, StringComparison.OrdinalIgnoreCase);
    }
}
