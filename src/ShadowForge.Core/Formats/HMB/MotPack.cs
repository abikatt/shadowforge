using ShadowForge.Formats.IPK;

namespace ShadowForge.Formats.HMB;

/// <summary>
/// A mot.mpk: an IPK1 archive of .hmb clips.
/// </summary>
public static class MotPack
{
    /// <summary>
    /// Reads every .hmb entry, named after its file name. Other entries and clips with extra
    /// sections are reported in <paramref name="warnings"/>.
    /// </summary>
    public static List<MotionClip> ReadAll(string mpkPath, out List<string> warnings)
    {
        warnings = [];
        var clips = new List<MotionClip>();
        using var stream = File.OpenRead(mpkPath);
        var archive = ArchiveReader.ReadArchive(stream);
        foreach (var entry in archive.Entries)
        {
            if (!entry.Name.EndsWith(".hmb", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"skipping non-hmb entry: {entry.Name}");
                continue;
            }
            using var ms = new MemoryStream();
            ArchiveReader.ExtractEntry(stream, entry, ms, archive.UsesZlib);
            var clip = MotionReader.Read(ms.ToArray(), Path.GetFileNameWithoutExtension(entry.Name));
            if (clip.ExtraSectionTypes.Count > 0)
                warnings.Add($"{entry.Name}: extra FT sections " +
                    string.Join(", ", clip.ExtraSectionTypes.Select(t => $"0x{t:X}")));
            clips.Add(clip);
        }
        return clips;
    }
}
