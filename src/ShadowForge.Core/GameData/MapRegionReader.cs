using ShadowForge.Formats.IPK;

namespace ShadowForge.GameData;

/// <summary>
/// Reads map assets from one region archive (pack\map\ipk\{region}.ipk) or its loose mirror
/// (map\ipk\{region}\...). Town, dungeon and indoor regions store entries under "map\...",
/// battle and world regions drop that segment ("battle\..."), so every read tries both forms.
/// </summary>
public sealed class MapRegionReader
{
    private readonly GameInstall _install;
    private readonly string _regionName;

    public string RegionIPKName => _regionName + ".ipk";
    public bool Available { get; }

    private string PackedPath => Path.Combine(
        _install.GameDataRoot, "pack", "map", "ipk", RegionIPKName);
    private string LooseDir => Path.Combine(_install.GameDataRoot, "map", "ipk", _regionName);

    public MapRegionReader(GameInstall install, string regionIPKName)
    {
        _install = install;
        _regionName = regionIPKName.EndsWith(".ipk", StringComparison.OrdinalIgnoreCase)
            ? regionIPKName[..^4] : regionIPKName;
        Available = File.Exists(PackedPath) || Directory.Exists(LooseDir);
    }

    /// <summary>
    /// The region-relative forms of a map VFS path: as given, then without its "map\" segment.
    /// </summary>
    internal static IEnumerable<string> EntryCandidates(string vfs)
    {
        string v = VfsPath.Normalize(vfs);
        yield return v;
        if (v.StartsWith(@"map\", StringComparison.OrdinalIgnoreCase))
            yield return v[4..];
    }

    public bool Exists(string vfsPath) => TryRead(vfsPath, extract: false, out _);

    public byte[] Read(string vfsPath)
    {
        if (TryRead(vfsPath, extract: true, out byte[] bytes)) return bytes;
        throw new FileNotFoundException(
            $"'{vfsPath}' not found in map region {_regionName} " +
            $"(packed: {PackedPath}, loose: {LooseDir}).");
    }

    public void ExtractAll(string destDir)
    {
        Directory.CreateDirectory(destDir);
        if (File.Exists(PackedPath))
        {
            using var s = File.OpenRead(PackedPath);
            new ArchiveReader(s).ExtractAll(destDir);
            return;
        }
        if (!Directory.Exists(LooseDir))
            throw new DirectoryNotFoundException(
                $"Map region {_regionName} is not present (packed or loose).");
        DirectoryTree.CopyContents(LooseDir, destDir);
    }

    private bool TryRead(string vfsPath, bool extract, out byte[] bytes)
    {
        bytes = [];
        foreach (string cand in EntryCandidates(vfsPath))
        {
            string loose = Path.Combine(LooseDir, cand);
            if (File.Exists(loose))
            {
                if (extract) bytes = File.ReadAllBytes(loose);
                return true;
            }
        }
        if (!File.Exists(PackedPath)) return false;
        using var stream = File.OpenRead(PackedPath);
        var archive = ArchiveReader.ReadArchive(stream);
        foreach (string cand in EntryCandidates(vfsPath))
        {
            var entry = archive.Entries.FirstOrDefault(
                e => e.Name.Equals(cand, StringComparison.OrdinalIgnoreCase));
            if (entry is null) continue;
            if (extract)
            {
                using var ms = new MemoryStream();
                ArchiveReader.ExtractEntry(stream, entry, ms, archive.UsesZlib);
                bytes = ms.ToArray();
            }
            return true;
        }
        return false;
    }
}
