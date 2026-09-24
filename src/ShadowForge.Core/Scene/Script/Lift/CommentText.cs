namespace ShadowForge.Scene.Script.Lift;

/// <summary>
/// UTF-16 packing of opcode 5050 developer comments, low half of each word first.
/// </summary>
public static class CommentText
{
    public const int Words = 14;
    private const int Units = Words * 2;

    /// <summary>
    /// False when the words are not a NUL-terminated string followed by NUL padding,
    /// or the text holds a control character the one-line text form cannot carry.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<uint> words, out string text)
    {
        text = "";
        if (words.Length != Words) return false;
        var units = new char[Units];
        for (int i = 0; i < Words; i++)
        {
            units[2 * i] = (char)(words[i] & 0xFFFF);
            units[2 * i + 1] = (char)(words[i] >> 16);
        }
        int end = Array.IndexOf(units, '\0');
        if (end < 0) end = Units;
        for (int i = 0; i < end; i++)
            if (units[i] < ' ') return false;
        for (int i = end; i < Units; i++)
            if (units[i] != '\0') return false;
        text = new string(units, 0, end);
        return true;
    }

    /// <summary>
    /// NUL-padded to 14 words.
    /// </summary>
    public static uint[] Encode(string text)
    {
        if (text.Length > Units)
            throw new FormatException($"comment longer than {Units} UTF-16 units: '{text}'");
        if (text.Contains('\0'))
            throw new FormatException("comment contains a NUL character");
        var words = new uint[Words];
        for (int i = 0; i < text.Length; i++)
        {
            int shift = (i & 1) == 0 ? 0 : 16;
            words[i / 2] |= (uint)text[i] << shift;
        }
        return words;
    }
}
