using System.Text;
using ShadowForge.IO;

namespace ShadowForge.Formats.IPK;

public static class ArchiveWriter
{
    public sealed record InputEntry(string Name, byte[] Data);

    /// <summary>
    /// Builds an uncompressed IPK1 archive. Entry data starts at the aligned end of the entry
    /// table and every entry sits at an aligned offset with zero padding between. The record
    /// tail is written as zeros, so <see cref="ArchiveReader.ReadArchive"/> reports the result
    /// as zlib. That has no effect because no entry is compressed.
    /// </summary>
    public static byte[] Build(IReadOnlyList<InputEntry> entries, uint alignment = 0x80)
    {
        foreach (var entry in entries)
        {
            if (Encoding.ASCII.GetByteCount(entry.Name) > Archive.NameFieldSize - 1)
            {
                throw new ArgumentException(
                    $"Entry name '{entry.Name}' exceeds {Archive.NameFieldSize - 1} bytes", nameof(entries));
            }
        }

        uint tableEnd = (uint)(Archive.HeaderSize + Archive.EntryRecordSize * entries.Count);

        var offsets = new uint[entries.Count];
        uint cursor = Align.Up(tableEnd, alignment);
        for (int i = 0; i < entries.Count; i++)
        {
            cursor = Align.Up(cursor, alignment);
            offsets[i] = cursor;
            cursor += (uint)entries[i].Data.Length;
        }

        uint totalSize = Align.Up(cursor, alignment);

        using var ms = new MemoryStream((int)totalSize);
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        writer.Write(Archive.Magic);
        writer.Write(alignment);
        writer.Write((uint)entries.Count);
        writer.Write(totalSize);

        for (int i = 0; i < entries.Count; i++)
        {
            var nameBytes = new byte[Archive.NameFieldSize];
            Encoding.ASCII.GetBytes(entries[i].Name, 0, entries[i].Name.Length, nameBytes, 0);
            writer.Write(nameBytes);

            uint size = (uint)entries[i].Data.Length;
            writer.Write(0u);
            writer.Write(size);
            writer.Write(offsets[i]);
            writer.Write(size);
            for (int k = 0; k < 4; k++)
                writer.Write(0u);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ms.Position = offsets[i];
            writer.Write(entries[i].Data);
        }

        ms.SetLength(totalSize);
        return ms.ToArray();
    }
}
