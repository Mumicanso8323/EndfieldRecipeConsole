using EndfieldRecipe.Core.Domain;
using EndfieldRecipe.Core.Application;
using EndfieldRecipe.Core.Ports;

namespace EndfieldRecipe.Tui;

public interface IScreen {
    string Title { get; }
    void Render(IRenderContext context, ScreenView view, ScreenContext screenContext);
    ScreenResult Handle(Intent intent, ScreenContext context);
}

public sealed class ScreenContext {
    public ScreenContext(AppData data, IAppRepository repository, NeedHistory history) {
        Data = data;
        Repository = repository;
        History = history;
    }

    public AppData Data { get; }
    public IAppRepository Repository { get; }
    public NeedHistory History { get; }

    public void Save() => Repository.Save(Data);
}

public sealed class ScreenView {
    public ScreenView(string breadcrumb, string help, string status) {
        Breadcrumb = breadcrumb;
        Help = help;
        Status = status;
    }

    public string Breadcrumb { get; set; }
    public string Help { get; set; }
    public string Status { get; set; }
}

public enum ScreenAction {
    None,
    Push,
    Pop,
    Replace,
    Exit
}

public sealed class ScreenResult {
    private ScreenResult(ScreenAction action, IScreen? screen = null) {
        Action = action;
        Screen = screen;
    }

    public ScreenAction Action { get; }
    public IScreen? Screen { get; }

    public static ScreenResult None() => new(ScreenAction.None);
    public static ScreenResult Pop() => new(ScreenAction.Pop);
    public static ScreenResult Exit() => new(ScreenAction.Exit);
    public static ScreenResult Push(IScreen screen) => new(ScreenAction.Push, screen);
    public static ScreenResult Replace(IScreen screen) => new(ScreenAction.Replace, screen);
}
