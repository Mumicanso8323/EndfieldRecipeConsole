namespace EndfieldRecipe.Tui;

public sealed class ScreenNavigator {
    private readonly Stack<IScreen> _stack = new();
    private readonly ConsoleRenderer _renderer = new();
    private readonly ITextMeasurer _measurer = new EastAsianTextMeasurer();

    public ScreenNavigator(IScreen root) {
        _stack.Push(root);
    }

    public void Run(ScreenContext context) {
        Console.CursorVisible = false;
        var width = Console.WindowWidth;
        var height = Console.WindowHeight;
        var screen = new VirtualScreen(width, height);

        while (_stack.Count > 0) {
            if (Console.WindowWidth != width || Console.WindowHeight != height) {
                width = Console.WindowWidth;
                height = Console.WindowHeight;
                screen = new VirtualScreen(width, height);
            }

            screen.Clear();
            var current = _stack.Peek();
            var view = ResolveView(current, context);
            var renderContext = new RenderContext(screen, _measurer);
            context.SetRenderContext(renderContext);
            renderContext.WriteHeader(UiText.AppTitle, view.Breadcrumb);
            current.Render(renderContext, view, context);
            renderContext.WriteFooter(view.Help, view.Status);
            _renderer.Render(screen);

            var key = Console.ReadKey(intercept: true);
            var intent = KeyBinding.FromConsoleKey(key);
            var result = current.Handle(intent, context);
            ApplyResult(result);
        }

        Console.CursorVisible = true;
    }

    private static ScreenView ResolveView(IScreen screen, ScreenContext context) {
        return screen switch {
            HomeScreen => new ScreenView(UiText.HomeTitle, UiText.HelpHome, string.Empty),
            NeedListScreen => new ScreenView(UiText.NeedTitle, UiText.HelpNeedList, string.Empty),
            NeedResultScreen => new ScreenView(UiText.NeedResultTitle, UiText.HelpNeedResult, string.Empty),
            ItemsScreen => new ScreenView(UiText.ItemsTitle, UiText.HelpList, string.Empty),
            MachinesScreen => new ScreenView(UiText.MachinesTitle, UiText.HelpList, string.Empty),
            RecipesScreen => new ScreenView(UiText.RecipesTitle, UiText.HelpList, string.Empty),
            _ => new ScreenView(screen.Title, UiText.HelpList, string.Empty)
        };
    }

    private void ApplyResult(ScreenResult result) {
        switch (result.Action) {
            case ScreenAction.Push:
                if (result.Screen != null) _stack.Push(result.Screen);
                break;
            case ScreenAction.Pop:
                if (_stack.Count > 0) _stack.Pop();
                break;
            case ScreenAction.Replace:
                if (_stack.Count > 0) _stack.Pop();
                if (result.Screen != null) _stack.Push(result.Screen);
                break;
            case ScreenAction.Exit:
                _stack.Clear();
                break;
        }
    }
}
