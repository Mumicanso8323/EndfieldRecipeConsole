using EndfieldRecipe.Core.Application;

namespace EndfieldRecipe.Tui;

public sealed class DiagnosticsScreen : IScreen {
    private int _selected;
    private int _offset;

    public string Title => UiText.DiagnosticsTitle;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        var diagnostics = AppDiagnostics.Analyze(screenContext.Data);
        var warningCount = diagnostics.Count(d => d.Level == DiagnosticLevel.Warning);
        var errorCount = diagnostics.Count(d => d.Level == DiagnosticLevel.Error);
        context.WriteBodyLine(0, $"{UiText.DiagnosticsWarning}:{warningCount} / {UiText.DiagnosticsError}:{errorCount}");

        if (diagnostics.Count == 0) {
            context.WriteBodyLine(1, UiText.StatusEmpty);
            return;
        }

        var bodyHeight = Math.Max(1, context.BodyHeight - 1);
        if (_selected >= diagnostics.Count) _selected = Math.Max(0, diagnostics.Count - 1);
        ClampOffset(bodyHeight, diagnostics.Count);

        var max = Math.Min(diagnostics.Count, _offset + bodyHeight);
        var line = 1;
        for (var i = _offset; i < max; i++) {
            var diag = diagnostics[i];
            var prefix = i == _selected ? "> " : "  ";
            var level = diag.Level switch {
                DiagnosticLevel.Warning => UiText.DiagnosticsWarning,
                DiagnosticLevel.Error => UiText.DiagnosticsError,
                _ => UiText.DiagnosticsInfo
            };
            context.WriteBodyLine(line, $"{prefix}{level}: {diag.Message}");
            line++;
        }
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        var diagnostics = AppDiagnostics.Analyze(context.Data);
        var count = diagnostics.Count;
        var page = Math.Max(1, context.BodyHeight - 1);

        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(Math.Max(0, count - 1), _selected + 1);
                break;
            case IntentKind.PageUp:
                _selected = Math.Max(0, _selected - page);
                break;
            case IntentKind.PageDown:
                _selected = Math.Min(Math.Max(0, count - 1), _selected + page);
                break;
            case IntentKind.JumpTop:
                _selected = 0;
                break;
            case IntentKind.JumpBottom:
                _selected = Math.Max(0, count - 1);
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(page, count);
        return ScreenResult.None();
    }

    private void ClampOffset(int bodyHeight, int count) {
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + bodyHeight) {
            _offset = Math.Min(_selected, Math.Max(0, count - bodyHeight));
        }
    }
}
