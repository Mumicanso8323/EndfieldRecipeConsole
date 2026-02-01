namespace EndfieldRecipe.Tui;

public sealed class HomeScreen : IScreen {
    private readonly string[] _menu = {
        UiText.ItemsTitle,
        UiText.MachinesTitle,
        UiText.RecipesTitle,
        UiText.NeedTitle,
        "終了"
    };

    private int _selected;

    public string Title => UiText.HomeTitle;

    public void Render(IRenderContext context, ScreenView view, ScreenContext screenContext) {
        for (var i = 0; i < _menu.Length && i < context.BodyHeight; i++) {
            var prefix = i == _selected ? "> " : "  ";
            context.WriteBodyLine(i, prefix + _menu[i]);
        }
    }

    public ScreenResult Handle(Intent intent, ScreenContext context) {
        switch (intent.Kind) {
            case IntentKind.MoveUp:
                _selected = Math.Max(0, _selected - 1);
                return ScreenResult.None();
            case IntentKind.MoveDown:
                _selected = Math.Min(_menu.Length - 1, _selected + 1);
                return ScreenResult.None();
            case IntentKind.Confirm:
                return _selected switch {
                    0 => ScreenResult.Push(new ItemsScreen()),
                    1 => ScreenResult.Push(new MachinesScreen()),
                    2 => ScreenResult.Push(new RecipesScreen()),
                    3 => ScreenResult.Push(new NeedListScreen()),
                    4 => ScreenResult.Exit(),
                    _ => ScreenResult.None()
                };
            case IntentKind.Back:
                return ScreenResult.Exit();
            default:
                return ScreenResult.None();
        }
    }
}
