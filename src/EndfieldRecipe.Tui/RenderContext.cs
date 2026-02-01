namespace EndfieldRecipe.Tui;

public sealed class RenderContext : IRenderContext {
    private readonly VirtualScreen _screen;
    private readonly ITextMeasurer _measurer;

    public RenderContext(VirtualScreen screen, ITextMeasurer measurer) {
        _screen = screen;
        _measurer = measurer;
    }

    public int Width => _screen.Width;
    public int Height => _screen.Height;
    public int BodyTop => 2;
    public int BodyHeight => Math.Max(1, Height - 4);
    public ITextMeasurer Measurer => _measurer;

    public void Clear() => _screen.Clear();

    public void WriteHeader(string title, string breadcrumb) {
        var headerText = string.IsNullOrWhiteSpace(breadcrumb)
            ? title
            : $"{title} / {breadcrumb}";
        WriteLine(0, headerText);
        WriteLine(1, new string('─', Width));
    }

    public void WriteFooter(string help, string status = "") {
        WriteLine(Height - 2, new string('─', Width));
        var footerText = string.IsNullOrWhiteSpace(status) ? help : $"{help} | {status}";
        WriteLine(Height - 1, footerText);
    }

    public void WriteBodyLine(int line, string text) {
        var y = BodyTop + line;
        if (y < BodyTop || y >= BodyTop + BodyHeight) return;
        WriteLine(y, text);
    }

    private void WriteLine(int y, string text) {
        var fitted = _measurer.FitToWidth(text, Width);
        _screen.WriteText(0, y, fitted, _measurer);
    }
}

public interface IRenderContext {
    int Width { get; }
    int Height { get; }
    int BodyTop { get; }
    int BodyHeight { get; }
    ITextMeasurer Measurer { get; }
    void Clear();
    void WriteHeader(string title, string breadcrumb);
    void WriteFooter(string help, string status = "");
    void WriteBodyLine(int line, string text);
}
