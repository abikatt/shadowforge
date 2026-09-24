using System.Globalization;

namespace ShadowForge.Formats.Text;

/// <summary>
/// Field and value helpers for the tab-separated Shift-JIS text files (.mdl, .map).
/// </summary>
public static class DslValue
{
    public static string[] SplitFields(string content) =>
        content.Split('\t', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Removes one pair of surrounding double quotes if present.
    /// </summary>
    public static string Unquote(string token)
    {
        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
            return token[1..^1];
        return token;
    }

    public static string Quote(string value) => "\"" + value + "\"";

    /// <summary>
    /// The game's C printf "%f": six decimals, invariant culture.
    /// </summary>
    public static string FormatFloat(float value) =>
        value.ToString("F6", CultureInfo.InvariantCulture);

    /// <summary>
    /// Positions of the first complete "..." pair at or after start.
    /// </summary>
    public static bool TryFindQuoted(string s, int start, out int open, out int close)
    {
        open = s.IndexOf('"', start);
        close = open < 0 ? -1 : s.IndexOf('"', open + 1);
        return close >= 0;
    }

    /// <summary>
    /// Content of the first "..." pair, ignoring anything after it: an OBJECT field
    /// "a.hdb" "b.csv" gives a.hdb, a PARTS path "map\a.efc",0 gives map\a.efc.
    /// Without a complete pair, the trimmed input.
    /// </summary>
    public static string FirstQuoted(string s) =>
        TryFindQuoted(s, 0, out int open, out int close) ? s[(open + 1)..close] : s.Trim();
}
