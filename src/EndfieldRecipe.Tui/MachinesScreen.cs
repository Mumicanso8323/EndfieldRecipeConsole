using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class MachinesScreen : IScreen {
    private int _selected;
    private int _offset;

    public string Title => UiText.MachinesTitle;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        var machines = GetMachines(screenContext);
        RenderList(context, machines);
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        var machines = GetMachines(context);
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(machines.Count - 1, _selected + 1);
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context, machines.Count);
        return ScreenResult.None();
    }

    private static List<Machine> GetMachines(ScreenContext context) {
        return context.Data.Machines.Values
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RenderList(IRenderContext context, List<Machine> machines) {
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
            context.WriteBodyLine(line, $"{prefix}{machine.DisplayName} ({machine.Key})");
            line++;
        }
    }

    private void ClampOffset(IRenderContext context, int count) {
        if (_selected < _offset) _offset = _selected;
        if (_selected >= _offset + context.BodyHeight) {
            _offset = Math.Min(_selected, Math.Max(0, count - context.BodyHeight));
        }
    }
}
