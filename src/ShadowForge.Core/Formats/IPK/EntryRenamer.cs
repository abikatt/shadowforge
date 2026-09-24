namespace ShadowForge.Formats.IPK;

/// <summary>
/// Rebuilds an IPK1 archive with a token replaced in every entry name. Payloads are
/// carried across byte for byte. The rebuild is uncompressed, which the format allows
/// per entry, so a renamed motion pack stays readable without matching the original
/// compressor.
/// </summary>
public static class EntryRenamer
{
    public static byte[] Token(byte[] archiveBytes, string from, string to)
    {
        using var ms = new MemoryStream(archiveBytes, writable: false);
        var archive = ArchiveReader.ReadArchive(ms);

        var rebuilt = new List<ArchiveWriter.InputEntry>(archive.Entries.Count);
        foreach (var entry in archive.Entries)
        {
            using var payload = new MemoryStream();
            ArchiveReader.ExtractEntry(ms, entry, payload, archive.UsesZlib);
            rebuilt.Add(new ArchiveWriter.InputEntry(entry.Name.Replace(from, to), payload.ToArray()));
        }
        return ArchiveWriter.Build(rebuilt);
    }
}
