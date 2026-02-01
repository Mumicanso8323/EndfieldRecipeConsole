using System.Text;

namespace EndfieldRecipe.Tui;

public interface ITextMeasurer {
    int GetDisplayWidth(string s);
    string FitToWidth(string s, int width);
}

public sealed class EastAsianTextMeasurer : ITextMeasurer {
    public int GetDisplayWidth(string s) {
        if (string.IsNullOrEmpty(s)) return 0;
        var width = 0;
        foreach (var rune in s.EnumerateRunes()) {
            width += GetRuneWidth(rune);
        }
        return width;
    }

    public string FitToWidth(string s, int width) {
        if (width <= 0) return string.Empty;
        var builder = new StringBuilder();
        var current = 0;
        foreach (var rune in s.EnumerateRunes()) {
            var runeWidth = GetRuneWidth(rune);
            if (current + runeWidth > width) {
                break;
            }
            builder.Append(rune.ToString());
            current += runeWidth;
        }

        if (current < GetDisplayWidth(s) && width >= 1) {
            if (current + 1 > width && builder.Length > 0) {
                builder.Remove(builder.Length - 1, 1);
                current = GetDisplayWidth(builder.ToString());
            }
            if (current < width) {
                builder.Append('…');
                current += 1;
            }
        }

        if (current < width) {
            builder.Append(' ', width - current);
        }

        return builder.ToString();
    }

    private static int GetRuneWidth(Rune rune) {
        var value = rune.Value;
        if (value <= 0x007F) return 1;

        if (IsWide(value)) return 2;
        return 1;
    }

    private static bool IsWide(int codePoint) {
        return codePoint switch {
            >= 0x1100 and <= 0x115F => true,
            >= 0x2329 and <= 0x232A => true,
            >= 0x2E80 and <= 0xA4CF => true,
            >= 0xAC00 and <= 0xD7A3 => true,
            >= 0xF900 and <= 0xFAFF => true,
            >= 0xFE10 and <= 0xFE6F => true,
            >= 0xFF00 and <= 0xFF60 => true,
            >= 0xFFE0 and <= 0xFFE6 => true,
            >= 0x1F300 and <= 0x1FAFF => true,
            _ => false
        };
    }
}
