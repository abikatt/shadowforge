namespace ShadowForge.IO;

public static class Align
{
    /// <summary>
    /// Rounds <paramref name="value"/> up to a multiple of <paramref name="alignment"/>.
    /// An alignment of zero returns the value unchanged.
    /// </summary>
    public static uint Up(uint value, uint alignment)
        => alignment == 0 ? value : (value + alignment - 1) / alignment * alignment;

    /// <inheritdoc cref="Up(uint, uint)"/>
    public static int Up(int value, int alignment)
        => alignment == 0 ? value : (value + alignment - 1) / alignment * alignment;

    /// <inheritdoc cref="Up(uint, uint)"/>
    public static long Up(long value, long alignment)
        => alignment == 0 ? value : (value + alignment - 1) / alignment * alignment;
}
