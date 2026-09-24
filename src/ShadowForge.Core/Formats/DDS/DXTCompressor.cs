using System.Runtime.InteropServices;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using CommunityToolkit.HighPerformance;

namespace ShadowForge.Formats.DDS;

/// <summary>
/// Encodes RGBA32 pixels into a <see cref="GraphicFormat"/>: BCnEncoder.NET for the block
/// formats, direct packing for the rest. The output is linear, little-endian and headerless.
/// </summary>
public static class DXTCompressor
{
    public static byte[] CompressRgba(byte[] rgbaData, int width, int height, GraphicFormat format)
    {
        ArgumentNullException.ThrowIfNull(rgbaData);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgbaData.Length < width * height * 4)
            throw new ArgumentException("rgbaData is too small for the given dimensions.");

        int pixelCount = width * height;
        return format switch
        {
            GraphicFormat.TextureFormatDXT1 => CompressBlocks(rgbaData, width, height, CompressionFormat.Bc1),
            GraphicFormat.TextureFormatDXT3 => CompressBlocks(rgbaData, width, height, CompressionFormat.Bc2),
            GraphicFormat.TextureFormatDXT5 => CompressBlocks(rgbaData, width, height, CompressionFormat.Bc3),
            GraphicFormat.TextureFormatDXN => CompressBlocks(rgbaData, width, height, CompressionFormat.Bc5),
            GraphicFormat.TextureFormatA8R8G8B8 => rgbaData[..(pixelCount * 4)],
            GraphicFormat.TextureFormatR5G6B5 => Pack16(rgbaData, pixelCount, PixelPacking.Pack565),
            GraphicFormat.TextureFormatX4R4G4B4 => Pack16(rgbaData, pixelCount, PixelPacking.Pack444),
            GraphicFormat.TextureFormatA8L8 => EncodeA8L8(rgbaData, pixelCount),
            GraphicFormat.TextureFormatL8 => EncodeL8(rgbaData, pixelCount),
            _ => throw new ArgumentException($"Unsupported format: {format}", nameof(format)),
        };
    }

    private static byte[] CompressBlocks(byte[] rgbaData, int width, int height, CompressionFormat bcFormat)
    {
        var encoder = new BcEncoder(bcFormat);
        encoder.OutputOptions.GenerateMipMaps = false;
        encoder.OutputOptions.Quality = CompressionQuality.BestQuality;

        ColorRgba32[] pixels = MemoryMarshal.Cast<byte, ColorRgba32>(rgbaData.AsSpan())
            .Slice(0, width * height)
            .ToArray();

        return encoder.EncodeToRawBytes(new ReadOnlyMemory2D<ColorRgba32>(pixels, height, width))[0];
    }

    private static byte[] Pack16(byte[] rgbaData, int pixelCount, Func<byte, byte, byte, ushort> pack)
    {
        byte[] output = new byte[pixelCount * 2];
        for (int i = 0; i < pixelCount; i++)
        {
            int src = i * 4;
            ushort packed = pack(rgbaData[src], rgbaData[src + 1], rgbaData[src + 2]);
            output[i * 2] = (byte)packed;
            output[i * 2 + 1] = (byte)(packed >> 8);
        }
        return output;
    }

    /// <summary>
    /// Two bytes per pixel: luminance, then alpha.
    /// </summary>
    private static byte[] EncodeA8L8(byte[] rgbaData, int pixelCount)
    {
        byte[] output = new byte[pixelCount * 2];
        for (int i = 0; i < pixelCount; i++)
        {
            int src = i * 4;
            output[i * 2] = PixelPacking.Luminance(rgbaData[src], rgbaData[src + 1], rgbaData[src + 2]);
            output[i * 2 + 1] = rgbaData[src + 3];
        }
        return output;
    }

    private static byte[] EncodeL8(byte[] rgbaData, int pixelCount)
    {
        byte[] output = new byte[pixelCount];
        for (int i = 0; i < pixelCount; i++)
        {
            int src = i * 4;
            output[i] = PixelPacking.Luminance(rgbaData[src], rgbaData[src + 1], rgbaData[src + 2]);
        }
        return output;
    }
}
