using ShadowForge.Formats.IPK;

namespace ShadowForge.Tests.IPK;

public sealed class ArchiveWriterTests
{
    [Fact]
    public void Build_RoundTripsThroughReader()
    {
        var entries = new[]
        {
            new ArchiveWriter.InputEntry("a.hmb", new byte[] { 1, 2, 3, 4, 5 }),
            new ArchiveWriter.InputEntry("b.hmb", Enumerable.Range(0, 300).Select(i => (byte)i).ToArray()),
        };
        var bytes = ArchiveWriter.Build(entries, alignment: 0x80);

        using var ms = new MemoryStream(bytes);
        var archive = ArchiveReader.ReadArchive(ms);
        Assert.Equal(2u, archive.FileCount);
        Assert.Equal((uint)bytes.Length, archive.ArchiveSize);
        Assert.Equal(0x80u, archive.Alignment);

        for (int i = 0; i < entries.Length; i++)
        {
            var e = archive.Entries[i];
            Assert.Equal(entries[i].Name, e.Name);
            Assert.False(e.IsCompressed);
            Assert.Equal((uint)entries[i].Data.Length, e.OriginalSize);
            Assert.Equal(0u, e.Offset % 0x80);
            using var outMs = new MemoryStream();
            ms.Position = 0;
            ArchiveReader.ExtractEntry(ms, e, outMs, archive.UsesZlib);
            Assert.Equal(entries[i].Data, outMs.ToArray());
        }
    }

    [Fact]
    public void Build_NameTooLong_Throws()
    {
        var name = new string('x', 64) + ".hmb";
        Assert.Throws<ArgumentException>(() =>
            ArchiveWriter.Build(new[] { new ArchiveWriter.InputEntry(name, new byte[1]) }));
    }
}
