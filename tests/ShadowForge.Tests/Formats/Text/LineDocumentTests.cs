using ShadowForge.Formats.Text;

namespace ShadowForge.Tests.Formats.Text;

public sealed class LineDocumentTests
{
    [Fact]
    public void RoundTrip_PreservesCrlfAndMissingFinalNewline()
    {
        byte[] bytes = "OBJECT\t\"a.hdb\"\r\nMOTPACK\t\"a.mpk\"\r\ntrailer"u8.ToArray();
        var doc = LineDocument.Parse(bytes);
        Assert.Equal(bytes, doc.ToBytes());
    }

    [Fact]
    public void RoundTrip_PreservesFinalCrlf()
    {
        byte[] bytes = "A\r\nB\r\n"u8.ToArray();
        var doc = LineDocument.Parse(bytes);
        Assert.Equal(3, doc.Lines.Count);
        Assert.Equal(bytes, doc.ToBytes());
    }

    [Fact]
    public void ReplaceContent_RewritesOneLineOnly()
    {
        byte[] bytes = "A\r\nB\r\n"u8.ToArray();
        var doc = LineDocument.Parse(bytes);
        doc.ReplaceContent(0, "Z");
        Assert.Equal("Z\r\nB\r\n"u8.ToArray(), doc.ToBytes());
    }

    [Fact]
    public void RoundTrip_PreservesDoubleByteShiftJisContent()
    {
        byte[] bytes = { 0x4F, 0x42, 0x4A, 0x09, 0x22, 0x83, 0x76, 0x22, 0x0D, 0x0A };
        var doc = LineDocument.Parse(bytes);
        Assert.Equal(bytes, doc.ToBytes());
    }

    [Fact]
    public void RoundTrip_PreservesOrphanShiftJisLeadByte()
    {
        byte[] bytes = { 0x41, 0x81 };
        var doc = LineDocument.Parse(bytes);
        Assert.Equal(bytes, doc.ToBytes());
    }
}
