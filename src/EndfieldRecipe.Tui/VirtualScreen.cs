using System.Text;

namespace EndfieldRecipe.Tui;

public sealed class VirtualScreen {
    private readonly ScreenCell[][] _cells;

    public VirtualScreen(int width, int height) {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _cells = new ScreenCell[Height][];
        for (var y = 0; y < Height; y++) {
            _cells[y] = new ScreenCell[Width];
            for (var x = 0; x < Width; x++) {
                _cells[y][x] = ScreenCell.Create(' ');
            }
        }
    }

    public int Width { get; }
    public int Height { get; }

    public void Clear(char fill = ' ') {
        for (var y = 0; y < Height; y++) {
            for (var x = 0; x < Width; x++) {
                _cells[y][x] = ScreenCell.Create(fill);
            }
        }
    }

    public void WriteText(int x, int y, string text, ITextMeasurer measurer) {
        if (y < 0 || y >= Height || x >= Width) return;

        var cursor = x;
        foreach (var rune in text.EnumerateRunes()) {
            var runeText = rune.ToString();
            var width = measurer.GetDisplayWidth(runeText);
            var cellWidth = runeText.Length == 2 ? Math.Max(width, 2) : width;
            if (cursor >= Width) break;
            if (cellWidth == 1) {
                ClearForOverwrite(y, cursor);
                _cells[y][cursor] = ScreenCell.Create(runeText[0]);
                cursor += 1;
                continue;
            }

            if (cursor + 1 >= Width) break;

            ClearForOverwrite(y, cursor);
            ClearForOverwrite(y, cursor + 1);
            if (runeText.Length == 2) {
                _cells[y][cursor] = ScreenCell.Create(runeText[0]);
                _cells[y][cursor + 1] = ScreenCell.Create(runeText[1]);
            } else {
                _cells[y][cursor] = ScreenCell.Create(runeText[0]);
                _cells[y][cursor + 1] = ScreenCell.Continuation();
            }
            cursor += 2;
        }
    }

    public string[] ToLines() {
        var lines = new string[Height];
        for (var y = 0; y < Height; y++) {
            var builder = new StringBuilder(Width);
            for (var x = 0; x < Width; x++) {
                var cell = _cells[y][x];
                if (cell.IsContinuation) continue;
                builder.Append(cell.Value);
            }
            lines[y] = builder.ToString();
        }
        return lines;
    }

    private void ClearForOverwrite(int y, int x) {
        if (x < 0 || x >= Width) return;
        if (_cells[y][x].IsContinuation) {
            _cells[y][x] = ScreenCell.Create(' ');
            if (x > 0 && !_cells[y][x - 1].IsContinuation) {
                _cells[y][x - 1] = ScreenCell.Create(' ');
            }
            return;
        }

        if (x + 1 < Width && _cells[y][x + 1].IsContinuation) {
            _cells[y][x + 1] = ScreenCell.Create(' ');
        }
    }

    private readonly struct ScreenCell {
        private ScreenCell(char value, bool isContinuation) {
            Value = value;
            IsContinuation = isContinuation;
        }

        public char Value { get; }
        public bool IsContinuation { get; }

        public static ScreenCell Create(char value) => new(value, false);
        public static ScreenCell Continuation() => new('\0', true);
    }
}
