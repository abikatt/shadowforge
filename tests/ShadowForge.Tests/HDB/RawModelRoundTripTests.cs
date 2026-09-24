using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Raw;
using ShadowForge.IO;

namespace ShadowForge.Tests.HDB;

public sealed class RawModelRoundTripTests
{
    [Fact]
    public void Reader_ProducesNonEmptyFirstTable()
    {
        var bytes = File.ReadAllBytes(TestFile.HDB);

        var raw = ModelReader.Read(bytes);

        Assert.NotNull(raw);
        Assert.Equal(0x42444840u, raw.Header.Magic);
        Assert.NotEmpty(raw.FirstTable);
    }

    [Fact]
    public void Reader_AllEntriesAreTypedSubclasses()
    {
        var raw = ModelReader.Read(File.ReadAllBytes(TestFile.HDB));

        var unknowns = raw.FirstTable.OfType<RawUnknownGenericEntry>().ToList();
        Assert.Empty(unknowns);

        Assert.NotEmpty(raw.FirstTable.OfType<RawBoneEntry>());
        Assert.NotEmpty(raw.FirstTable.OfType<RawTextureTableEntry>());
        Assert.NotEmpty(raw.FirstTable.OfType<RawVASetupEntry>());
        Assert.NotEmpty(raw.FirstTable.OfType<RawRenderCommandEntry>());
    }

    [Fact]
    public void Reader_PopulatesSecondTableAndBlocks()
    {
        var raw = ModelReader.Read(File.ReadAllBytes(TestFile.HDB));

        Assert.NotEmpty(raw.SecondTable.Entries);
        Assert.NotEmpty(raw.IndexBlocks);
        Assert.NotEmpty(raw.VertexArrays);

        int type3Count = raw.FirstTable.OfType<RawRenderCommandEntry>().Count();
        Assert.Equal(type3Count, raw.IndexBlocks.Count);

        int expectedVACount = raw.FirstTable
            .OfType<RawVASetupEntry>()
            .Sum(e => e.Records.Count);
        Assert.Equal(expectedVACount, raw.VertexArrays.Count);
    }

    [Fact]
    public void Writer_HeaderAndFirstTableMatchInput()
    {
        var bytes = File.ReadAllBytes(TestFile.HDB);
        var raw = ModelReader.Read(bytes);

        var output = ModelWriter.Write(raw);

        int ftStart = 32;
        int ftLength = (int)BigEndian.ReadUInt32(bytes, ftStart + 4);
        int ftEnd = ftStart + ftLength + 4;

        for (int i = 0; i < ftEnd; i++)
        {
            Assert.True(output.Length > i, $"Output too short at 0x{i:X}");
            Assert.True(bytes[i] == output[i],
                $"Mismatch at byte 0x{i:X}: expected 0x{bytes[i]:X2}, got 0x{output[i]:X2}");
        }
    }

    [Fact]
    public void RoundTrip_ByteIdentical()
    {
        var expected = File.ReadAllBytes(TestFile.HDB);
        var raw = ModelReader.Read(expected);
        var actual = ModelWriter.Write(raw);

        AssertByteIdentical(expected, actual);
    }

    [Fact]
    public void RoundTrip_KeepsATextureNameWiderThanSixteenBytes()
    {
        const string wide = "np109_eyelid_l_01";
        var raw = ModelReader.Read(File.ReadAllBytes(TestFile.HDB));
        raw.FirstTable.OfType<RawTextureTableEntry>().First().Records[0].SetName(wide);

        var written = ModelReader.Read(ModelWriter.Write(raw));

        Assert.Equal(wide,
            written.FirstTable.OfType<RawTextureTableEntry>().First().Records[0].Name);
    }

    private static void AssertByteIdentical(byte[] expected, byte[] actual)
    {
        if (expected.Length != actual.Length)
            Assert.Fail($"length mismatch - expected {expected.Length}, got {actual.Length}");

        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                int start = Math.Max(0, i - 16);
                int end = Math.Min(expected.Length, i + 16);
                var expHex = BitConverter.ToString(expected, start, end - start).Replace("-", " ");
                var actHex = BitConverter.ToString(actual, start, end - start).Replace("-", " ");
                Assert.Fail(
                    $"first mismatch at 0x{i:X}\n" +
                    $"  expected [{start:X}..{end:X}]: {expHex}\n" +
                    $"  actual   [{start:X}..{end:X}]: {actHex}");
            }
        }
    }
}
