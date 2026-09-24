using System.Globalization;
using ShadowForge.Scene.Script;

namespace ShadowForge.Formats.BDSL;

/// <summary>
/// Formats and parses one argument word according to its kind.
/// </summary>
public static class ValueFormat
{
    private const uint PlainIntLimit = 0x00FFFFFF;

    internal static readonly ArgSpec PlainInt = new("n", ArgKind.Int);
    internal static readonly ArgSpec Character = new("chara", ArgKind.Enum, OpcodeTable.CharNames);

    /// <summary>
    /// Text for a value under the argument's kind. Int values above 0xFFFFFF print as hex.
    /// </summary>
    public static string Format(uint value, ArgSpec spec) => spec.Kind switch
    {
        ArgKind.Hex => "0x" + value.ToString("X", CultureInfo.InvariantCulture),
        ArgKind.Float => FormatFloatBits(value),
        ArgKind.Var => $"var[{value}]",
        ArgKind.Label => $"@{value}",
        ArgKind.Enum when spec.EnumNames is not null && spec.EnumNames.TryGetValue(value, out var name) => name,
        _ => value > PlainIntLimit ? "0x" + value.ToString("X", CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Shortest decimal that round-trips the float, or hex for NaN, infinity and denormals.
    /// </summary>
    public static string FormatFloatBits(uint bits)
    {
        float f = BitConverter.UInt32BitsToSingle(bits);
        bool plain = bits == 0 || (float.IsFinite(f) && float.IsNormal(f));
        if (!plain) return "0x" + bits.ToString("X", CultureInfo.InvariantCulture);
        string s = f.ToString("R", CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('E')) s += ".0";
        return s;
    }

    /// <summary>
    /// Accepts every form Format writes. The kind only decides how a bare number is read.
    /// </summary>
    public static uint Parse(string token, ArgSpec spec)
    {
        token = token.Trim();
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.Parse(token.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (token.StartsWith("var[", StringComparison.Ordinal) && token.EndsWith(']'))
            return uint.Parse(token.AsSpan(4, token.Length - 5), CultureInfo.InvariantCulture);
        if (token.StartsWith('@'))
            return uint.Parse(token.AsSpan(1), CultureInfo.InvariantCulture);
        if (spec.EnumNames is not null)
            foreach (var pair in spec.EnumNames)
                if (pair.Value == token) return pair.Key;
        foreach (var pair in OpcodeTable.CharNames)
            if (pair.Value == token) return pair.Key;
        if (spec.Kind == ArgKind.Float)
        {
            if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                return BitConverter.SingleToUInt32Bits(f);
            throw new FormatException($"not a float: '{token}'");
        }
        if (int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int signed))
            return unchecked((uint)signed);
        if (uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out uint unsigned))
            return unsigned;
        throw new FormatException($"not a value: '{token}'");
    }
}
