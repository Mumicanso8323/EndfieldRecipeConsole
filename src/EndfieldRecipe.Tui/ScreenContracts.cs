using System;
using EndfieldRecipe.Core.Domain;
using EndfieldRecipe.Core.Application;
using EndfieldRecipe.Core.Ports;

namespace EndfieldRecipe.Tui;

public interface IScreen {
    string Title { get; }
    void Render(IRenderContext context, ScreenView view, ScreenContext screenContext);
    ScreenResult Handle(Intent intent, ScreenContext context);
}

public interface ITextEntryModeProvider {
    bool PreferTextInput { get; }
}

public sealed class ScreenContext : IRenderContext {
    private IRenderContext? _renderContext;
    private ConsoleKeyInfo _lastKeyInfo;

    public ScreenContext(
        AppData data,
        IAppRepository repository,
        NeedHistory history,
        AppSettings settings,
        ISettingsRepository settingsRepository
    ) {
        Data = data;
        Repository = repository;
        History = history;
        Settings = settings;
        SettingsRepository = settingsRepository;
    }

    public AppData Data { get; }
    public IAppRepository Repository { get; }
    public NeedHistory History { get; }
    public AppSettings Settings { get; }
    public ISettingsRepository SettingsRepository { get; }
    public ConsoleKeyInfo LastKeyInfo => _lastKeyInfo;

    public void SetLastKey(ConsoleKeyInfo key) {
        _lastKeyInfo = key;
    }

    public void SaveData() => Repository.Save(Data);
    public void SaveSettings() => SettingsRepository.Save(Settings);

    public void SetRenderContext(IRenderContext renderContext) {
        _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
    }

    private IRenderContext RenderContext =>
        _renderContext ?? throw new InvalidOperationException("Render context is not set.");

    public int Width => RenderContext.Width;
    public int Height => RenderContext.Height;
    public int BodyTop => RenderContext.BodyTop;
    public int BodyHeight => RenderContext.BodyHeight;
    public ITextMeasurer Measurer => RenderContext.Measurer;
    public void Clear() => RenderContext.Clear();
    public void WriteHeader(string title, string breadcrumb) => RenderContext.WriteHeader(title, breadcrumb);
    public void WriteFooter(string help, string status = "") => RenderContext.WriteFooter(help, status);
    public void WriteBodyLine(int line, string text) => RenderContext.WriteBodyLine(line, text);
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
