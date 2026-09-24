using ShadowForge.Formats.DDS;

namespace ShadowForge.Tests.Formats.DDS;

public sealed class ConverterStreamTests
{
    [Fact]
    public void StreamInput_MatchesFileInput()
    {
        const string fixture = "testdata/models/enemy/em001_ipk/ene/em001/em001_01.dds";

        byte[] fromPath = Converter.ConvertToPngBytes(fixture);

        byte[] raw = File.ReadAllBytes(fixture);
        using var ms = new MemoryStream(raw, writable: false);
        byte[] fromStream = Converter.ConvertToPngBytes(ms, isVolume: false);

        Assert.Equal(fromPath, fromStream);
    }
}
