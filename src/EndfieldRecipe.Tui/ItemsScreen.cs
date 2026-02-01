using EndfieldRecipe.Core.Domain;

namespace EndfieldRecipe.Tui;

public sealed class ItemsScreen : IScreen {
    private int _selected;
    private int _offset;

    public string Title => UiText.ItemsTitle;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        var items = GetItems(screenContext);
        RenderList(context, items);
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        var items = GetItems(context);
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                break;
            case IntentKind.MoveDown:
                _selected = Math.Min(items.Count - 1, _selected + 1);
                break;
            case IntentKind.Back:
                return ScreenResult.Pop();
        }

        ClampOffset(context, items.Count);
        return ScreenResult.None();
    }

    private static List<Item> GetItems(ScreenContext context) {
        return context.Data.Items.Values
            .OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RenderList(IRenderContext context, List<Item> items) {
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
            context.WriteBodyLine(line, $"{prefix}{item.DisplayName} ({item.Key})");
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
