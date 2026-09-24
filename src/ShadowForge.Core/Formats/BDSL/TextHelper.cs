using System.Globalization;
using System.Text;
using ShadowForge.Scene;

namespace ShadowForge.Formats.BDSL;

public static class TextHelper
{
    /// <summary>
    /// Cuts the line at the first '#' outside double quotes.
    /// </summary>
    public static string StripComment(string line)
    {
        bool inQuote = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuote = !inQuote;
            if (line[i] == '#' && !inQuote) return line[..i];
        }
        return line;
    }

    /// <summary>
    /// Splits on spaces and braces outside double quotes. Braces are dropped and
    /// quotes are kept on the token.
    /// </summary>
    public static List<string> TokenizeLine(string line)
    {
        var tokens = new List<string>();
        bool inQuote = false;
        var current = new StringBuilder();

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
                current.Append(c);
            }
            else if ((c == ' ' || c == '{' || c == '}') && !inQuote)
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
            }
            else current.Append(c);
        }
        if (current.Length > 0) tokens.Add(current.ToString());
        return tokens;
    }

    /// <summary>
    /// Removes surrounding double quotes and drops the backslash of every escape pair.
    /// </summary>
    public static string UnquoteString(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"')) s = s[1..^1];
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length) i++;
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parses decimal or 0x-prefixed hex. A trailing comma is ignored.
    /// </summary>
    public static uint ParseUInt(string s)
    {
        s = s.Trim().TrimEnd(',');
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.Parse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return uint.Parse(s, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a decimal float, or a 0x-prefixed hex string holding the raw IEEE bits.
    /// </summary>
    public static float ParseFloat(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = Convert.FromHexString(s[2..]);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }
        return float.Parse(s, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses "(1.0, 2.0, 3.0)" into its components.
    /// </summary>
    public static float[] ParseFloatTuple(string s)
    {
        s = s.Trim().Trim('(', ')');
        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => ParseFloat(p.Trim())).ToArray();
    }

    /// <summary>
    /// Round-trip decimal text, except NaN and infinity, which are written as 0x-prefixed raw bits.
    /// </summary>
    public static string FormatFloat(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            uint raw = BitConverter.SingleToUInt32Bits(value);
            return $"0x{raw:X8}";
        }
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    public static string AreaTypeName(AreaType t) => t switch
    {
        AreaType.Town    => "town",
        AreaType.Indoor  => "indoor",
        AreaType.Dungeon => "dungeon",
        AreaType.World   => "world",
        AreaType.Cube    => "cube",
        _ => $"type_{(uint)t}",
    };

    public static AreaType ParseAreaType(string s) => s.Trim() switch
    {
        "town"    => AreaType.Town,
        "indoor"  => AreaType.Indoor,
        "dungeon" => AreaType.Dungeon,
        "world"   => AreaType.World,
        "cube"    => AreaType.Cube,
        _ => (AreaType)ParseUInt(s),
    };

    public static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public static string EntryTypeName(EntryType type) => type switch
    {
        EntryType.Spawn  => "spawn",
        EntryType.Box    => "box",
        EntryType.Zone   => "zone",
        EntryType.Enemy  => "enemy",
        EntryType.Link   => "link",
        EntryType.Entity => "entity",
        EntryType.Warp   => "warp",
        _ => $"entry_{(uint)type}",
    };

    public static EntryType ParseEntryType(string s) => s.Trim() switch
    {
        "spawn"  => EntryType.Spawn,
        "box"    => EntryType.Box,
        "zone"   => EntryType.Zone,
        "enemy"  => EntryType.Enemy,
        "link"   => EntryType.Link,
        "entity" => EntryType.Entity,
        "warp"   => EntryType.Warp,
        _ => (EntryType)ParseUInt(s),
    };
}
