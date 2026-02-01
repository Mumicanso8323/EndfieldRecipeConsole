using EndfieldRecipe.Tui;
using EndfieldRecipe.Core.Domain;
using EndfieldRecipe.Core.Ports;
using EndfieldRecipe.Core.Application;
using Xunit;

public sealed class VirtualScreenTests {
    private readonly EastAsianTextMeasurer _measurer = new();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void WriteText_WithFullWidthCharacters_DoesNotInsertVisibleSpaces(int startX) {
        var screen = new VirtualScreen(20, 1);

        screen.WriteText(startX, 0, "アイテム", _measurer);

        var line = screen.ToLines()[0];
        Assert.Contains("アイテム", line);
        Assert.DoesNotContain("ア イ", line);
        Assert.True(_measurer.GetDisplayWidth(line) <= screen.Width);
    }

    [Fact]
    public void ToLines_NeverExceedsConfiguredWidth() {
        var screen = new VirtualScreen(12, 2);

        screen.WriteText(0, 0, "ABCアイテムDEF", _measurer);
        screen.WriteText(0, 1, "🙂アイテム", _measurer);

        foreach (var line in screen.ToLines()) {
            Assert.True(_measurer.GetDisplayWidth(line) <= screen.Width);
        }
    }

    [Fact]
    public void HomeMenu_DownDown_DoesNotDuplicateMarkers() {
        var screen = new VirtualScreen(24, 10);
        var context = new RenderContext(screen, _measurer);
        var home = new HomeScreen();
        var view = new ScreenView("Home", string.Empty, string.Empty);

        RenderHome(screen, context, home, view);
        Assert.Equal(1, CountMarkers(screen));
        Assert.Contains("> ", GetBodyLine(screen, context, 0));

        home.Handle(new Intent(IntentKind.MoveDown), new ScreenContext(new AppData(), new FakeRepository(), new NeedHistory()));
        RenderHome(screen, context, home, view);
        Assert.Equal(1, CountMarkers(screen));
        Assert.Contains("> ", GetBodyLine(screen, context, 1));

        home.Handle(new Intent(IntentKind.MoveDown), new ScreenContext(new AppData(), new FakeRepository(), new NeedHistory()));
        RenderHome(screen, context, home, view);
        Assert.Equal(1, CountMarkers(screen));
        Assert.Contains("> ", GetBodyLine(screen, context, 2));
    }

    private static void RenderHome(VirtualScreen screen, RenderContext context, HomeScreen home, ScreenView view) {
        screen.Clear();
        home.Render(context, view, new ScreenContext(new AppData(), new FakeRepository(), new NeedHistory()));
    }

    private static int CountMarkers(VirtualScreen screen) {
        return screen.ToLines().Sum(line => line.Count(ch => ch == '>'));
    }

    private static string GetBodyLine(VirtualScreen screen, RenderContext context, int line) {
        var y = context.BodyTop + line;
        return screen.ToLines()[y];
    }

    private sealed class FakeRepository : IAppRepository {
        public AppData Load() => new();
        public void Save(AppData data) {
        }
    }
}
