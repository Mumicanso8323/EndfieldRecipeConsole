namespace EndfieldRecipe.Tui;

public sealed class ConsoleRenderer {
    private string[]? _previous;

    public void Render(VirtualScreen screen) {
        var lines = screen.ToLines();
        if (_previous == null || _previous.Length != lines.Length) {
            Console.Clear();
            _previous = new string[lines.Length];
        }

        for (var y = 0; y < lines.Length; y++) {
            if (_previous![y] == lines[y]) continue;
            Console.SetCursorPosition(0, y);
            Console.Write(lines[y]);
            _previous[y] = lines[y];
        }
    }
}
