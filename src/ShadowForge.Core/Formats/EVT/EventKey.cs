namespace ShadowForge.Formats.EVT;

public sealed class EventKey
{
    /// <summary>
    /// The type and size words that open every key.
    /// </summary>
    public const int HeaderSize = 8;

    public const int TypeOffset = 0;
    public const int SizeOffset = 4;

    public int Type;

    /// <summary>
    /// Bytes including the header.
    /// </summary>
    public int Size;

    /// <summary>
    /// The Size - 8 payload bytes after the header.
    /// </summary>
    public byte[] Data = [];
}
