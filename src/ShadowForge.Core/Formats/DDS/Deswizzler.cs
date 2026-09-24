using System.Numerics;
using ShadowForge.IO;

namespace ShadowForge.Formats.DDS;

/// <summary>
/// Converts between linear texture data and the Xenos tiled layout. Addressing works in
/// units of one 4x4 block for block-compressed formats and one pixel otherwise.
/// </summary>
public static class Deswizzler
{
    private const int TileEdge = 32;

    /// <summary>
    /// Swaps every 16-bit word, converting between the console's 8-in-16 byte order and
    /// little-endian. An odd-length input is padded with one zero byte first.
    /// </summary>
    public static byte[] SwapByteOrderX360(byte[] imageData)
    {
        byte[] result = new byte[imageData.Length + (imageData.Length % 2)];
        Buffer.BlockCopy(imageData, 0, result, 0, imageData.Length);
        ByteSwap.Swap16(result);
        return result;
    }

    public static byte[] ConvertToLinearTexture(byte[] data, int pixelWidth, int pixelHeight, GraphicFormat format)
    {
        int pitch = format.BytesPerUnit();
        int w = format.UnitsAcross(pixelWidth);
        int h = format.UnitsAcross(pixelHeight);
        byte[] linear = new byte[w * h * pitch];
        Copy2D(data, linear, w, h, pitch, toLinear: true);
        return linear;
    }

    public static byte[] ConvertToSwizzledTexture(byte[] linearData, int pixelWidth, int pixelHeight, GraphicFormat format, int swizzledDataSize)
    {
        int pitch = format.BytesPerUnit();
        byte[] tiled = new byte[swizzledDataSize];
        Copy2D(tiled, linearData, format.UnitsAcross(pixelWidth), format.UnitsAcross(pixelHeight), pitch, toLinear: false);
        return tiled;
    }

    public static byte[] ConvertToLinearTexture3D(byte[] data, int pixelWidth, int pixelHeight, int pixelDepth, GraphicFormat format)
    {
        int pitch = format.BytesPerUnit();
        int w = format.UnitsAcross(pixelWidth);
        int h = format.UnitsAcross(pixelHeight);
        byte[] linear = new byte[w * h * pixelDepth * pitch];
        Copy3D(data, linear, w, h, pixelDepth, pitch, toLinear: true);
        return linear;
    }

    public static byte[] ConvertToSwizzledTexture3D(byte[] linearData, int pixelWidth, int pixelHeight, int pixelDepth, GraphicFormat format, int swizzledDataSize)
    {
        int pitch = format.BytesPerUnit();
        byte[] tiled = new byte[swizzledDataSize];
        Copy3D(tiled, linearData, format.UnitsAcross(pixelWidth), format.UnitsAcross(pixelHeight), pixelDepth, pitch, toLinear: false);
        return tiled;
    }

    /// <summary>
    /// Walks every unit of the tiled buffer and copies it to or from its linear position.
    /// Units whose linear position falls outside w x h, or outside either buffer, are skipped.
    /// </summary>
    private static void Copy2D(byte[] tiled, byte[] linear, int w, int h, int pitch, bool toLinear)
    {
        int units = tiled.Length / pitch;
        for (int i = 0; i < units; i++)
        {
            int x = TiledX(i, w, pitch);
            int y = TiledY(i, w, pitch);
            int tiledOffset = i * pitch;
            int linearOffset = (y * w + x) * pitch;
            if (x < w && y < h && linearOffset + pitch <= linear.Length && tiledOffset + pitch <= tiled.Length)
                CopyUnit(tiled, tiledOffset, linear, linearOffset, pitch, toLinear);
        }
    }

    private static void Copy3D(byte[] tiled, byte[] linear, int w, int h, int d, int pitch, bool toLinear)
    {
        for (int z = 0; z < d; z++)
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int tiledOffset = Tiled3DOffset(x, y, z, w, h, pitch);
            int linearOffset = (z * h * w + y * w + x) * pitch;
            if (tiledOffset >= 0 && tiledOffset + pitch <= tiled.Length &&
                linearOffset >= 0 && linearOffset + pitch <= linear.Length)
                CopyUnit(tiled, tiledOffset, linear, linearOffset, pitch, toLinear);
        }
    }

    private static void CopyUnit(byte[] tiled, int tiledOffset, byte[] linear, int linearOffset, int pitch, bool toLinear)
    {
        if (toLinear) Buffer.BlockCopy(tiled, tiledOffset, linear, linearOffset, pitch);
        else Buffer.BlockCopy(linear, linearOffset, tiled, tiledOffset, pitch);
    }

    private static int TiledX(int unitOffset, int widthInUnits, int pitch)
    {
        int alignedWidth = (widthInUnits + 31) & ~31;
        int logBpp = BitOperations.Log2((uint)pitch);

        int offsetByte = unitOffset << logBpp;
        int offsetTile = ((offsetByte & ~0xFFF) >> 3) + ((offsetByte & 0x700) >> 2) + (offsetByte & 0x3F);
        int offsetMacro = offsetTile >> (7 + logBpp);

        int macroX = (offsetMacro % (alignedWidth >> 5)) << 2;
        int tile = (((offsetTile >> (5 + logBpp)) & 2) + (offsetByte >> 6)) & 3;
        int macro = (macroX + tile) << 3;
        int micro = ((((offsetTile >> 1) & ~0xF) + (offsetTile & 0xF)) & ((pitch << 3) - 1)) >> logBpp;

        return macro + micro;
    }

    private static int TiledY(int unitOffset, int widthInUnits, int pitch)
    {
        int alignedWidth = (widthInUnits + 31) & ~31;
        int logBpp = BitOperations.Log2((uint)pitch);

        int offsetByte = unitOffset << logBpp;
        int offsetTile = ((offsetByte & ~0xFFF) >> 3) + ((offsetByte & 0x700) >> 2) + (offsetByte & 0x3F);
        int offsetMacro = offsetTile >> (7 + logBpp);

        int macroY = (offsetMacro / (alignedWidth >> 5)) << 2;
        int tile = ((offsetTile >> (6 + logBpp)) & 1) + ((offsetByte & 0x800) >> 10);
        int macro = (macroY + tile) << 3;
        int micro = (((offsetTile & (((pitch << 6) - 1) & ~0x1F)) + ((offsetTile & 0xF) << 1)) >> (3 + logBpp)) & ~1;

        return macro + micro + ((offsetTile & 0x10) >> 4);
    }

    private static int Tiled3DOffset(int x, int y, int z, int widthInUnits, int heightInUnits, int pitch)
    {
        int logBpp = BitOperations.Log2((uint)pitch);

        int alignedWidth = (widthInUnits + TileEdge - 1) & ~(TileEdge - 1);
        int alignedHeight = (heightInUnits + TileEdge - 1) & ~(TileEdge - 1);

        int macroOuter = ((y >> 4) + (z >> 2) * (alignedHeight >> 4)) * (alignedWidth >> 5);
        int macro = ((((x >> 5) + macroOuter) << (logBpp + 6)) & 0xFFFFFFF) << 1;
        int micro = (((x & 7) + ((y & 6) << 2)) << (logBpp + 6)) >> 6;
        int offsetOuter = ((y >> 3) + (z >> 2)) & 1;
        int offset1 = offsetOuter + ((((x >> 3) + (offsetOuter << 1)) & 3) << 1);

        int offset2 = ((macro + (micro & ~15)) << 1) + (micro & 15) + ((z & 3) << (logBpp + 6)) + ((y & 1) << 4);

        int address = (offset1 & 1) << 3;
        address += (offset2 >> 6) & 7;
        address <<= 3;
        address += offset1 & ~1;
        address <<= 2;
        address += offset2 & ~511;
        address <<= 3;
        address += offset2 & 63;

        return address;
    }
}
