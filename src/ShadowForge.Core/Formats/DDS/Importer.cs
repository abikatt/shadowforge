using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShadowForge.Formats.DDS;

/// <summary>
/// Encodes PNG images into Xbox 360 textures, either with a header built from the format
/// or with a reference file's header copied verbatim.
/// </summary>
public static class Importer
{
    public static void Import(string pngPath, string outputDDSPath, GraphicFormat format)
    {
        byte[] rgbaData = LoadPngToRgba(pngPath, out int width, out int height);
        File.WriteAllBytes(outputDDSPath, ImportFromRgba(rgbaData, width, height, format));
    }

    /// <summary>
    /// Replaces the texture data of <paramref name="referenceDDSPath"/>. The PNG must match its
    /// dimensions, and the output keeps its header byte for byte and its data size.
    /// </summary>
    public static void ImportWithReference(string pngPath, string referenceDDSPath,
        string outputDDSPath, GraphicFormat? formatOverride = null)
    {
        byte[] refData = ReadReference(referenceDDSPath, "Reference DDS file");

        var (refFormat, refWidth, refHeight, _) = Header.Parse(refData);
        GraphicFormat format = formatOverride ?? refFormat;

        byte[] rgbaData = LoadPngToRgba(pngPath, out int width, out int height);
        if (width != refWidth || height != refHeight)
            throw new InvalidDataException(
                $"PNG dimensions ({width}x{height}) do not match reference DDS ({refWidth}x{refHeight}).");

        int swizzledDataSize = refData.Length - Header.HeaderSize;
        byte[] data = Encode2D(rgbaData, width, height, format, swizzledDataSize);
        File.WriteAllBytes(outputDDSPath, Assemble(refData, data, swizzledDataSize));
    }

    public static byte[] ImportFromRgba(byte[] rgbaData, int width, int height, GraphicFormat format)
    {
        int swizzledDataSize = Header.ComputeSwizzledDataSize(width, height, format);
        byte[] data = Encode2D(rgbaData, width, height, format, swizzledDataSize);
        return Assemble(Header.Build(width, height, format), data, swizzledDataSize);
    }

    /// <summary>
    /// Replaces the texture data of a .36t volume. Takes one PNG per depth slice, in depth
    /// order, each matching the reference's edge length.
    /// </summary>
    public static void ImportVolumeWithReference(string[] pngSlicesPaths, string reference36tPath, string outputPath)
    {
        byte[] refData = ReadReference(reference36tPath, "Reference .36t file");

        GraphicFormat format = Header.ReadFormat(refData);
        var (edge, depth) = Header.ReadVolumeSize(refData);

        if (pngSlicesPaths.Length != depth)
            throw new InvalidDataException($"Expected {depth} slices based on reference header, but got {pngSlicesPaths.Length}.");

        using var linear = new MemoryStream();
        for (int z = 0; z < depth; z++)
        {
            byte[] slice = LoadPngToRgba(pngSlicesPaths[z], out int sliceWidth, out int sliceHeight);
            if (sliceWidth != edge || sliceHeight != edge)
                throw new InvalidDataException($"Slice {z} dimensions ({sliceWidth}x{sliceHeight}) do not match reference volume ({edge}x{edge}).");
            linear.Write(DXTCompressor.CompressRgba(slice, edge, edge, format));
        }

        int swizzledDataSize = refData.Length - Header.HeaderSize;
        byte[] swizzled = Deswizzler.ConvertToSwizzledTexture3D(linear.ToArray(), edge, edge, depth, format, swizzledDataSize);
        File.WriteAllBytes(outputPath, Assemble(refData, SwapFor(format, swizzled), swizzledDataSize));
    }

    private static byte[] ReadReference(string path, string label)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < Header.HeaderSize)
            throw new InvalidDataException($"{label} is too small.");
        return data;
    }

    private static byte[] Encode2D(byte[] rgbaData, int width, int height, GraphicFormat format, int swizzledDataSize)
    {
        byte[] encoded = DXTCompressor.CompressRgba(rgbaData, width, height, format);
        byte[] swizzled = Deswizzler.ConvertToSwizzledTexture(encoded, width, height, format, swizzledDataSize);
        return SwapFor(format, swizzled);
    }

    private static byte[] SwapFor(GraphicFormat format, byte[] swizzled)
        => Header.IsByteSwapped(format) ? Deswizzler.SwapByteOrderX360(swizzled) : swizzled;

    /// <summary>
    /// The first <see cref="Header.HeaderSize"/> bytes of <paramref name="header"/>, then
    /// <paramref name="data"/> truncated or zero-padded to <paramref name="dataSize"/>.
    /// </summary>
    private static byte[] Assemble(byte[] header, byte[] data, int dataSize)
    {
        byte[] result = new byte[Header.HeaderSize + dataSize];
        Buffer.BlockCopy(header, 0, result, 0, Header.HeaderSize);
        Buffer.BlockCopy(data, 0, result, Header.HeaderSize, Math.Min(data.Length, dataSize));
        return result;
    }

    private static byte[] LoadPngToRgba(string pngPath, out int width, out int height)
    {
        using var image = Image.Load<Rgba32>(pngPath);
        width = image.Width;
        height = image.Height;
        byte[] rgbaData = new byte[width * height * 4];
        image.CopyPixelDataTo(rgbaData);
        return rgbaData;
    }
}
