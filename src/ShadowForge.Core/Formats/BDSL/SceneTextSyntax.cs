using System.Numerics;
using ShadowForge.IO;

namespace ShadowForge.Formats.BDSL;

/// <summary>
/// Line-level helpers shared by the scene, entry, when-block and waypoint readers and writers.
/// </summary>
internal static class SceneTextSyntax
{
    /// <summary>
    /// The line without its CR, '#' comment and surrounding whitespace.
    /// </summary>
    public static string Clean(string raw) => TextHelper.StripComment(raw.TrimEnd('\r')).Trim();

    public static bool IsBlockEnd(string line) => line is "}" or "};";

    public static FormatException LineError(int lineNumber, string message) =>
        new($"BDSL line {lineNumber}: {message}");

    /// <summary>
    /// Splits "key = value" at the first '='. False when the line has no '='.
    /// </summary>
    public static bool TrySplitField(string line, out string key, out string value)
    {
        int eq = line.IndexOf('=');
        if (eq < 0)
        {
            key = value = "";
            return false;
        }
        key = line[..eq].Trim();
        value = line[(eq + 1)..].Trim();
        return true;
    }

    /// <summary>
    /// Splits "@name value" at the first space.
    /// </summary>
    public static (string Name, string Value) SplitDirective(string line)
    {
        int space = line.IndexOf(' ');
        return space >= 0 ? (line[..space], line[(space + 1)..].Trim()) : (line, "");
    }

    /// <summary>
    /// Parses "(x, y, z)". Missing components read as zero.
    /// </summary>
    public static Vector3 ParseVector(string text)
    {
        var v = TextHelper.ParseFloatTuple(text);
        return new Vector3(v.Length > 0 ? v[0] : 0f, v.Length > 1 ? v[1] : 0f, v.Length > 2 ? v[2] : 0f);
    }

    public static string FormatVector(Vector3 v) =>
        $"({TextHelper.FormatFloat(v.X)}, {TextHelper.FormatFloat(v.Y)}, {TextHelper.FormatFloat(v.Z)})";

    /// <summary>
    /// Component-wise: -0 counts as zero and a NaN component as nonzero.
    /// </summary>
    public static bool IsZero(Vector3 v) => v.X == 0f && v.Y == 0f && v.Z == 0f;

    /// <summary>
    /// Big-endian words as one uppercase hex run.
    /// </summary>
    public static string WordsToHex(params uint[] words)
    {
        var bytes = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++)
            BigEndian.WriteUInt32(bytes, i * 4, words[i]);
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Inverse of <see cref="WordsToHex"/>. A trailing partial word is dropped.
    /// </summary>
    public static uint[] HexToWords(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        var words = new uint[bytes.Length / 4];
        for (int i = 0; i < words.Length; i++)
            words[i] = BigEndian.ReadUInt32(bytes, i * 4);
        return words;
    }
}
