using ShadowForge.Minimap;

namespace ShadowForge.Tests.Minimap;

public sealed class MmpWriterTests
{
    [Fact]
    public void EmitsShippedFormatDescriptor()
    {
        var m = new MaskResult { Mask = new float[512 * 256], Elevation = new float[512 * 256],
                                 Width = 512, Height = 256,
                                 WorldMinX = -68f, WorldMinZ = -192f,
                                 WorldScaleX = 768f, WorldScaleY = 384f };
        string text = MmpWriter.Emit(m);
        Assert.Equal(
            "TEXSIZE\t512\t256\r\n" +
            "MAPSCALE\t768\t384\r\n" +
            "DISPSIZE\t200\t200\r\n" +
            "OFFSET\t68\t192\r\n",
            text);
    }

    [Fact]
    public void ClampsDispSizeToFloorForLargeWorldScale()
    {
        var m = new MaskResult { Mask = new float[512 * 256], Elevation = new float[512 * 256],
                                 Width = 512, Height = 256,
                                 WorldMinX = 0f, WorldMinZ = 0f,
                                 WorldScaleX = 20000f, WorldScaleY = 10000f };
        string text = MmpWriter.Emit(m);
        Assert.Contains("DISPSIZE\t70\t70\r\n", text);
    }
}
