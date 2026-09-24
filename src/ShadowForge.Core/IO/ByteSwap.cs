namespace ShadowForge.IO;

public static class ByteSwap
{
    /// <summary>
    /// Swaps the two bytes of every 16-bit word in place. A trailing odd byte is left as is.
    /// </summary>
    public static void Swap16(Span<byte> data)
    {
        for (int i = 0; i + 1 < data.Length; i += 2)
            (data[i], data[i + 1]) = (data[i + 1], data[i]);
    }
}
