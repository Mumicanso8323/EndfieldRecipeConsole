using System.Text;

namespace EndfieldRecipe.Tui;

public sealed class VirtualScreen {
    private readonly char[][] _cells;

    public VirtualScreen(int width, int height) {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _cells = new char[Height][];
        for (var y = 0; y < Height; y++) {
            _cells[y] = Enumerable.Repeat(' ', Width).ToArray();
        }
    }

    public int Width { get; }
    public int Height { get; }

    public void Clear(char fill = ' ') {
        for (var y = 0; y < Height; y++) {
            Array.Fill(_cells[y], fill);
        }
    }

    public void WriteText(int x, int y, string text, ITextMeasurer measurer) {
        if (y < 0 || y >= Height || x >= Width) return;

        var cursor = x;
        foreach (var rune in text.EnumerateRunes()) {
            var width = measurer.GetDisplayWidth(rune.ToString());
            if (cursor >= Width) break;
            if (width == 1) {
                _cells[y][cursor] = rune.ToString()[0];
                cursor += 1;
                continue;
            }

            var runeText = rune.ToString();
            if (runeText.Length == 2 && cursor + 1 < Width) {
                _cells[y][cursor] = runeText[0];
                _cells[y][cursor + 1] = runeText[1];
                cursor += 2;
            } else {
                _cells[y][cursor] = runeText[0];
                if (cursor + 1 < Width) {
                    _cells[y][cursor + 1] = ' ';
                    cursor += 2;
                } else {
                    cursor += 1;
                }
            }
        }
    }

    public string[] ToLines() {
        var lines = new string[Height];
        for (var y = 0; y < Height; y++) {
            lines[y] = new string(_cells[y]);
        }
        return lines;
    }
}
