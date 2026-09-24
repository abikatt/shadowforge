using System.Text;

namespace ShadowForge;

public static class EncodingExtensions
{
    private static readonly Lazy<Encoding> _shiftJIS = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932);
    });

    /// <summary>
    /// Code page 932. Registers the code-pages provider on first use.
    /// </summary>
    public static Encoding ShiftJIS => _shiftJIS.Value;

    /// <summary>
    /// Base of the Private Use Area range that carries bytes Shift-JIS cannot round-trip:
    /// byte b decodes to U+F000 + b. It sits above U+E757, the highest PUA code point cp932
    /// assigns to the user-defined characters F040-F9FC.
    /// </summary>
    private const int PuaBase = 0xF000;

    /// <summary>
    /// Decodes a Shift-JIS byte range with no NUL scan. Bytes that do not survive a
    /// decode/encode round-trip, such as an orphan lead byte, become U+F0xx.
    /// </summary>
    public static string DecodeShiftJISRaw(byte[] data, int offset, int length)
        => length <= 0 ? "" : DecodeShiftJISCore(data, offset, length, mapNulls: false);

    /// <summary>
    /// Decodes a fixed-width Shift-JIS field keeping embedded NULs as U+F000, then trims
    /// trailing U+F000 padding.
    /// </summary>
    public static string DecodeShiftJISFull(this byte[] data, int offset, int length)
        => length <= 0 ? "" : DecodeShiftJISCore(data, offset, length, mapNulls: true).TrimEnd((char)PuaBase);

    private static string DecodeShiftJISCore(byte[] data, int offset, int length, bool mapNulls)
    {
        int end = offset + length;
        var sb = new StringBuilder();
        int i = offset;
        while (i < end)
        {
            byte b = data[i];
            if (mapNulls && b == 0)
            {
                sb.Append((char)PuaBase);
                i++;
                continue;
            }

            bool isLead = (b >= 0x81 && b <= 0x9F) || (b >= 0xE0 && b <= 0xFC);
            if (isLead && i + 1 < end)
            {
                if (mapNulls && data[i + 1] == 0)
                {
                    sb.Append((char)(PuaBase + b));
                    sb.Append((char)PuaBase);
                }
                else
                {
                    AppendRoundTripped(sb, data, i, 2);
                }
                i += 2;
            }
            else if (isLead)
            {
                sb.Append((char)(PuaBase + b));
                i++;
            }
            else
            {
                AppendRoundTripped(sb, data, i, 1);
                i++;
            }
        }
        return sb.ToString();
    }

    private static void AppendRoundTripped(StringBuilder sb, byte[] data, int offset, int count)
    {
        string decoded = ShiftJIS.GetString(data, offset, count);
        if (ShiftJIS.GetBytes(decoded).AsSpan().SequenceEqual(data.AsSpan(offset, count)))
        {
            sb.Append(decoded);
            return;
        }
        for (int k = 0; k < count; k++)
            sb.Append((char)(PuaBase + data[offset + k]));
    }

    /// <summary>
    /// Decodes ASCII from a fixed-width field up to the first NUL, the field end or the
    /// end of <paramref name="data"/>, whichever comes first.
    /// </summary>
    public static string DecodeASCII(this byte[] data, int offset, int length)
    {
        int limit = (int)Math.Min((long)offset + length, data.Length);
        int end = offset;
        while (end < limit && data[end] != 0) end++;
        return end == offset ? "" : Encoding.ASCII.GetString(data, offset, end - offset);
    }

    /// <summary>
    /// Encodes to Shift-JIS, writing each U+F000-U+F0FF character back as its raw byte.
    /// </summary>
    public static byte[] EncodeShiftJIS(this string value)
    {
        if (!value.Any(IsRawByte)) return ShiftJIS.GetBytes(value);

        var result = new List<byte>();
        var segment = new StringBuilder();
        foreach (char c in value)
        {
            if (!IsRawByte(c))
            {
                segment.Append(c);
                continue;
            }
            if (segment.Length > 0)
            {
                result.AddRange(ShiftJIS.GetBytes(segment.ToString()));
                segment.Clear();
            }
            result.Add((byte)(c - PuaBase));
        }
        if (segment.Length > 0)
            result.AddRange(ShiftJIS.GetBytes(segment.ToString()));
        return result.ToArray();
    }

    private static bool IsRawByte(char c) => c >= (char)PuaBase && c < (char)(PuaBase + 0x100);

    /// <summary>
    /// Writes the string as a NUL-padded Shift-JIS field, truncating to <paramref name="length"/>.
    /// </summary>
    public static void WriteShiftJIS(this string value, byte[] dest, int offset, int length)
    {
        Array.Clear(dest, offset, length);
        var bytes = value.EncodeShiftJIS();
        Array.Copy(bytes, 0, dest, offset, Math.Min(bytes.Length, length));
    }
}
