using System.Buffers.Binary;

namespace ShadowForge.Formats.DDS;

/// <summary>
/// Decodes linear, little-endian DXT1/3/5 and DXN block data to RGBA32. DXN carries X and Y
/// in the red and green channels and Z is rebuilt into blue from the unit-length constraint.
/// </summary>
public static class DXTDecompressor
{
    private const int BlockPixels = 16;

    public static byte[] DecompressDXTTexture(byte[] compressedData, int pixelWidth, int pixelHeight, int pixelDepth, GraphicFormat format)
    {
        int blockSizeBytes = format == GraphicFormat.TextureFormatDXT1 ? 8 : 16;
        Action<ReadOnlySpan<byte>, byte[]> decodeBlock = format switch
        {
            GraphicFormat.TextureFormatDXT1 => DecodeColorBlock,
            GraphicFormat.TextureFormatDXT3 => DecodeDXT3Block,
            GraphicFormat.TextureFormatDXT5 => DecodeDXT5Block,
            GraphicFormat.TextureFormatDXN => DecodeDXNBlock,
            _ => throw new ArgumentException("Unsupported DXT format."),
        };

        int paddedWidth = (pixelWidth + 3) / 4 * 4;
        int paddedHeight = (pixelHeight + 3) / 4 * 4;
        int paddedDepth = pixelDepth > 1 ? (pixelDepth + 3) / 4 * 4 : 1;

        int blocksX = paddedWidth / 4;
        int blocksY = paddedHeight / 4;

        byte[] outputPixels = new byte[paddedWidth * paddedHeight * paddedDepth * 4];
        byte[] block = new byte[BlockPixels * 4];

        for (int blockZ = 0; blockZ < paddedDepth; blockZ++)
        for (int blockY = 0; blockY < blocksY; blockY++)
        for (int blockX = 0; blockX < blocksX; blockX++)
        {
            int blockOffset = (blockZ * blocksY * blocksX + blockY * blocksX + blockX) * blockSizeBytes;
            if (blockOffset + blockSizeBytes > compressedData.Length)
                continue;

            decodeBlock(new ReadOnlySpan<byte>(compressedData, blockOffset, blockSizeBytes), block);

            for (int row = 0; row < 4; row++)
            {
                int destIdx = (blockZ * paddedHeight * paddedWidth + (blockY * 4 + row) * paddedWidth + blockX * 4) * 4;
                if (destIdx + 16 <= outputPixels.Length)
                    Buffer.BlockCopy(block, row * 16, outputPixels, destIdx, 16);
            }
        }

        if (paddedWidth == pixelWidth && paddedHeight == pixelHeight && paddedDepth == pixelDepth)
            return outputPixels;

        byte[] croppedOutput = new byte[pixelWidth * pixelHeight * pixelDepth * 4];
        for (int z = 0; z < pixelDepth; z++)
        for (int y = 0; y < pixelHeight; y++)
        {
            int srcStart = (z * paddedHeight * paddedWidth + y * paddedWidth) * 4;
            int destStart = (z * pixelHeight * pixelWidth + y * pixelWidth) * 4;
            Buffer.BlockCopy(outputPixels, srcStart, croppedOutput, destStart, pixelWidth * 4);
        }
        return croppedOutput;
    }

    /// <summary>
    /// Explicit 4-bit alpha, two pixels per byte with the low nibble first, then a DXT1 color block.
    /// </summary>
    private static void DecodeDXT3Block(ReadOnlySpan<byte> block, byte[] pixels)
    {
        DecodeColorBlock(block.Slice(8, 8), pixels);
        for (int i = 0; i < 8; i++)
        {
            byte val = block[i];
            pixels[i * 2 * 4 + 3] = (byte)((val & 0xF) * 17);
            pixels[(i * 2 + 1) * 4 + 3] = (byte)(((val >> 4) & 0xF) * 17);
        }
    }

    private static void DecodeDXT5Block(ReadOnlySpan<byte> block, byte[] pixels)
    {
        DecodeColorBlock(block.Slice(8, 8), pixels);
        Span<byte> alpha = stackalloc byte[BlockPixels];
        DecodeInterpolatedBlock(block.Slice(0, 8), alpha);
        for (int i = 0; i < BlockPixels; i++)
            pixels[i * 4 + 3] = alpha[i];
    }

    private static void DecodeDXNBlock(ReadOnlySpan<byte> block, byte[] pixels)
    {
        Span<byte> red = stackalloc byte[BlockPixels];
        Span<byte> green = stackalloc byte[BlockPixels];
        DecodeInterpolatedBlock(block.Slice(0, 8), red);
        DecodeInterpolatedBlock(block.Slice(8, 8), green);

        for (int i = 0; i < BlockPixels; i++)
        {
            byte r = red[i];
            byte g = green[i];
            double normR = r / 255.0 * 2.0 - 1.0;
            double normG = g / 255.0 * 2.0 - 1.0;
            double dotProductSq = normR * normR + normG * normG;

            byte b = 0;
            if (dotProductSq <= 1.0)
                b = (byte)((Math.Sqrt(1.0 - dotProductSq) * 0.5 + 0.5) * 255);

            pixels[i * 4] = r;
            pixels[i * 4 + 1] = g;
            pixels[i * 4 + 2] = b;
            pixels[i * 4 + 3] = 255;
        }
    }

    /// <summary>
    /// Two RGB565 endpoints and sixteen 2-bit indices. When color0 &lt;= color1 the block is in
    /// three-color mode and index 3 is transparent black.
    /// </summary>
    private static void DecodeColorBlock(ReadOnlySpan<byte> block, byte[] pixels)
    {
        ushort color0 = BinaryPrimitives.ReadUInt16LittleEndian(block);
        ushort color1 = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(2));
        uint indices = BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(4));

        var (r0, g0, b0) = PixelPacking.Unpack565(color0);
        var (r1, g1, b1) = PixelPacking.Unpack565(color1);

        Span<byte> palette = stackalloc byte[16];
        Set(palette, 0, r0, g0, b0, 255);
        Set(palette, 1, r1, g1, b1, 255);
        if (color0 > color1)
        {
            Set(palette, 2, (byte)((2 * r0 + r1) / 3), (byte)((2 * g0 + g1) / 3), (byte)((2 * b0 + b1) / 3), 255);
            Set(palette, 3, (byte)((r0 + 2 * r1) / 3), (byte)((g0 + 2 * g1) / 3), (byte)((b0 + 2 * b1) / 3), 255);
        }
        else
        {
            Set(palette, 2, (byte)((r0 + r1) / 2), (byte)((g0 + g1) / 2), (byte)((b0 + b1) / 2), 255);
            Set(palette, 3, 0, 0, 0, 0);
        }

        for (int i = 0; i < BlockPixels; i++)
        {
            int index = (int)((indices >> (i * 2)) & 0x3);
            palette.Slice(index * 4, 4).CopyTo(pixels.AsSpan(i * 4, 4));
        }
    }

    private static void Set(Span<byte> palette, int entry, byte r, byte g, byte b, byte a)
    {
        palette[entry * 4] = r;
        palette[entry * 4 + 1] = g;
        palette[entry * 4 + 2] = b;
        palette[entry * 4 + 3] = a;
    }

    /// <summary>
    /// Two 8-bit endpoints and sixteen 3-bit indices, the DXT5 alpha and DXN channel block.
    /// When value0 &lt;= value1 there are four interpolated values plus 0 and 255.
    /// </summary>
    private static void DecodeInterpolatedBlock(ReadOnlySpan<byte> block, Span<byte> values)
    {
        byte v0 = block[0];
        byte v1 = block[1];
        Span<byte> table = stackalloc byte[8];
        table[0] = v0;
        table[1] = v1;
        if (v0 > v1)
        {
            for (int i = 1; i < 7; i++)
                table[i + 1] = (byte)(((8 - i) * v0 + i * v1) / 8);
        }
        else
        {
            for (int i = 1; i < 5; i++)
                table[i + 1] = (byte)(((6 - i) * v0 + i * v1) / 6);
            table[6] = 0;
            table[7] = 255;
        }

        ulong indices = 0;
        for (int i = 0; i < 6; i++)
            indices |= (ulong)block[2 + i] << (i * 8);

        for (int i = 0; i < BlockPixels; i++)
            values[i] = table[(int)((indices >> (i * 3)) & 0x7)];
    }
}
