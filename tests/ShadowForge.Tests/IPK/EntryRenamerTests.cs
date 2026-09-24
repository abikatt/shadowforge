using System.Text;
using ShadowForge.Formats.IPK;

namespace ShadowForge.Tests.IPK;

public sealed class EntryRenamerTests
{
    private static byte[] Sample() => ArchiveWriter.Build(new[]
    {
        new ArchiveWriter.InputEntry("np109_fc_a.hmb", Encoding.ASCII.GetBytes("ALPHA")),
        new ArchiveWriter.InputEntry("np109_fd_wt02.hmb", Encoding.ASCII.GetBytes("BRAVO-LONGER")),
        new ArchiveWriter.InputEntry("shared_base.hmb", Encoding.ASCII.GetBytes("CHARLIE")),
    });

    private static Archive Read(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return ArchiveReader.ReadArchive(ms);
    }

    private static string Payload(byte[] bytes, string name)
    {
        using var ms = new MemoryStream(bytes);
        var archive = ArchiveReader.ReadArchive(ms);
        var entry = archive.Entries.Single(e => e.Name == name);
        using var outMs = new MemoryStream();
        ArchiveReader.ExtractEntry(ms, entry, outMs, archive.UsesZlib);
        return Encoding.ASCII.GetString(outMs.ToArray());
    }

    [Fact]
    public void Token_RenamesMatchingEntriesOnly()
    {
        byte[] result = EntryRenamer.Token(Sample(), "np109", "pc11");
        var names = Read(result).Entries.Select(e => e.Name).ToArray();

        Assert.Equal(
            new[] { "pc11_fc_a.hmb", "pc11_fd_wt02.hmb", "shared_base.hmb" },
            names);
    }

    [Fact]
    public void Token_PreservesEveryPayload()
    {
        byte[] result = EntryRenamer.Token(Sample(), "np109", "pc11");

        Assert.Equal("ALPHA", Payload(result, "pc11_fc_a.hmb"));
        Assert.Equal("BRAVO-LONGER", Payload(result, "pc11_fd_wt02.hmb"));
        Assert.Equal("CHARLIE", Payload(result, "shared_base.hmb"));
    }

    [Fact]
    public void Token_WithNoMatchesReturnsAReadableArchive()
    {
        byte[] result = EntryRenamer.Token(Sample(), "em001", "em002");

        Assert.Equal(3, Read(result).Entries.Count);
        Assert.Equal("ALPHA", Payload(result, "np109_fc_a.hmb"));
    }

    [Fact]
    public void Token_RejectsANameTooLongForTheField()
    {
        byte[] archive = ArchiveWriter.Build(new[]
        {
            new ArchiveWriter.InputEntry("a.hmb", new byte[] { 1 }),
        });

        Assert.Throws<ArgumentException>(() => EntryRenamer.Token(archive, "a", new string('b', 60)));
    }
}
