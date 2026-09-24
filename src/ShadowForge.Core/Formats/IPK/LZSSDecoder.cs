namespace ShadowForge.Formats.IPK;

/// <summary>
/// LZSS with a 4096-byte ring window pre-filled with spaces and first written at 4096 - 18.
/// Each flag byte covers eight items, LSB first: a set bit is a literal byte, a clear bit a
/// two-byte match whose window offset is lo | (hi &amp; 0xF0) &lt;&lt; 4 and length (hi &amp; 0x0F) + 3.
/// </summary>
public static class LZSSDecoder
{
    private const int WindowSize = 4096;
    private const int MaxMatchLen = 18;
    private const int MinMatchLen = 3;
    private const byte WindowFill = 0x20;

    public static byte[] Decompress(byte[] input, int outputSize)
    {
        var output = new byte[outputSize];
        var window = new byte[WindowSize];
        Array.Fill(window, WindowFill);

        int srcPos = 0;
        int dstPos = 0;
        int winPos = WindowSize - MaxMatchLen;
        int flags = 0;
        int flagBits = 0;

        void Emit(byte b)
        {
            output[dstPos++] = b;
            window[winPos] = b;
            winPos = (winPos + 1) & (WindowSize - 1);
        }

        while (dstPos < outputSize && srcPos < input.Length)
        {
            if (flagBits == 0)
            {
                flags = input[srcPos++];
                flagBits = 8;
            }

            if ((flags & 1) != 0)
            {
                if (srcPos >= input.Length) break;
                Emit(input[srcPos++]);
            }
            else
            {
                if (srcPos + 1 >= input.Length) break;
                int lo = input[srcPos++];
                int hi = input[srcPos++];
                int offset = lo | ((hi & 0xF0) << 4);
                int length = (hi & 0x0F) + MinMatchLen;

                for (int i = 0; i < length && dstPos < outputSize; i++)
                    Emit(window[(offset + i) & (WindowSize - 1)]);
            }

            flags >>= 1;
            flagBits--;
        }

        return output;
    }
}
