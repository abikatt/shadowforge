using ShadowForge.Formats.IPK;

namespace ShadowForge.GameData;

/// <summary>
/// Reads game files by VFS path (e.g. chara\ene\em001\em001_obj.hdb) from whichever
/// backing store the located root provides: a loose extract tree, or the packs of an
/// install (pack\chara\ipk\{rigId}.ipk, pack\map\ipk\{region}.ipk,
/// pack\event\{eventId}.ipk, pack\{top}.ipk).
/// </summary>
public sealed class GameFileSystem
{
    private readonly GameInstall _install;
    private PackManifests? _manifests;
    private PackManifests Manifests => _manifests ??= PackManifests.Load(_install);

    public GameFileSystem(GameInstall install) => _install = install;

    /// <summary>
    /// Read a file by VFS path (backslash-separated). Throws if not found.
    /// </summary>
    public byte[] ReadVfs(string vfsPath)
    {
        string vfs = Normalize(vfsPath);
        if (OverlayPath(vfs) is { } overlayHit) return File.ReadAllBytes(overlayHit);
        string looseVfs = Path.Combine(_install.GameDataRoot, vfs);
        if (File.Exists(looseVfs)) return File.ReadAllBytes(looseVfs);
        foreach (string loose in LooseCandidates(vfs))
        {
            string abs = Path.Combine(_install.GameDataRoot, loose);
            if (File.Exists(abs)) return File.ReadAllBytes(abs);
        }
        if (IsMapPath(vfs))
        {
            foreach (string region in MapRegionCandidates(vfs))
            {
                var reader = new MapRegionReader(_install, region);
                if (reader.Exists(vfs)) return reader.Read(vfs);
            }
            throw new FileNotFoundException(
                $"Map VFS path not found in any manifest-listed region pack: {vfs}");
        }
        if (TryReadFromIPK(vfs, out byte[] bytes)) return bytes;
        throw new FileNotFoundException($"VFS path not found in loose tree or packs: {vfs}");
    }

    /// <summary>
    /// Read a file a stage def references. Several region packs can carry the same
    /// map path, so the region the manifest assigns to the stage wins, as it does
    /// when the game loads the stage.
    /// </summary>
    public byte[] ReadStageAsset(string stageOrMapFile, string vfsPath)
    {
        string vfs = Normalize(vfsPath);
        if (OverlayPath(vfs) is { } overlayHit) return File.ReadAllBytes(overlayHit);
        if (Manifests.IPKForMap(stageOrMapFile) is { } region)
        {
            var reader = new MapRegionReader(_install, region);
            if (reader.Exists(vfs)) return reader.Read(vfs);
        }
        return ReadVfs(vfs);
    }

    /// <summary>
    /// Read a stage def (db_*.map). Defs are shelved under database\map by
    /// manifest category (battle/event/world subdirectories, others flat).
    /// </summary>
    public byte[] ReadMapDef(string stageOrMapFile)
    {
        var candidates = Manifests.MapDefVfsCandidates(stageOrMapFile);
        foreach (string vfs in candidates)
        {
            try { return ReadVfs(vfs); }
            catch (FileNotFoundException) { }
        }
        throw new FileNotFoundException(
            "stage def not found at any candidate path: " + string.Join(", ", candidates));
    }

    /// <summary>
    /// Lightweight existence check: loose file, else the owning ipk's entry
    /// names (no extraction).
    /// </summary>
    public bool ExistsVfs(string vfsPath)
    {
        string vfs = Normalize(vfsPath);
        if (OverlayPath(vfs) is not null) return true;
        if (File.Exists(Path.Combine(_install.GameDataRoot, vfs))) return true;
        foreach (string loose in LooseCandidates(vfs))
            if (File.Exists(Path.Combine(_install.GameDataRoot, loose))) return true;

        if (IsMapPath(vfs))
            return MapRegionCandidates(vfs)
                .Any(region => new MapRegionReader(_install, region).Exists(vfs));

        var (ipkPath, needle) = LocateIPK(vfs);
        if (ipkPath is null || !File.Exists(ipkPath)) return false;
        using var stream = File.OpenRead(ipkPath);
        var archive = ArchiveReader.ReadArchive(stream);
        return archive.Entries.Any(e => MatchesNeedle(e.Name, needle));
    }

    /// <summary>
    /// Write every existing resolved file to outDir preserving its loose
    /// layout. Returns count written.
    /// </summary>
    public int Mount(ResolvedEntity entity, string outDir)
    {
        int written = 0;
        foreach (var f in entity.Files)
        {
            if (!f.Exists) continue;

            string vfs = f.Role is FileRole.ModelDef or FileRole.MotScr or FileRole.MotCmd
                ? f.LooseRelPath
                : f.VfsPath;
            byte[] bytes;
            try { bytes = ReadVfs(vfs); }
            catch (FileNotFoundException) { continue; }
            string dest = Path.Combine(outDir, f.LooseRelPath.Replace('\\', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllBytes(dest, bytes);
            written++;
        }
        return written;
    }

    /// <summary>
    /// The archive that owns a VFS path in a packed install, as (ipkName, ipkInnerPath).
    /// Derived exactly the way ReadVfs/ExistsVfs locate packed entries, so it is the single
    /// source of truth for an entity file's owning pack. Even against a loose tree it names
    /// the pack the file would live in (chara\{class}\{id} -> {id}.ipk,
    /// database\... -> database.ipk).
    /// </summary>
    public (string IPKName, string IPKInnerPath) OwningIPK(string vfsPath)
    {
        var (ipkPath, needle) = LocateIPK(Normalize(vfsPath));
        return (ipkPath is null ? "" : Path.GetFileName(ipkPath), needle);
    }

    /// <summary>
    /// List files matching a filename glob under a VFS directory, drawing from BOTH the
    /// loose tree (recursively) and the directory's owning pack. Returns VFS-relative
    /// paths (backslash, relative to the game-data root), deduped and sorted. This is the
    /// discovery primitive the entity catalog uses to enumerate model defs - GameFileSystem
    /// otherwise only reads a single known path. Pack coverage is the single owning pack that
    /// LocateIPK resolves for the directory (not a multi-ipk sweep), and assumes pack entries
    /// are stored segment-relative (leading top segment stripped), as the real database.ipk is.
    /// </summary>
    public IReadOnlyList<string> EnumerateVfs(string vfsDir, string filePattern)
    {
        string dir = Normalize(vfsDir);
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in _install.Overlays)
        {
            string overlayDir = Path.Combine(root, dir);
            if (!Directory.Exists(overlayDir)) continue;
            foreach (string f in Directory.EnumerateFiles(overlayDir, filePattern, SearchOption.AllDirectories))
                results.Add(Path.GetRelativePath(root, f).Replace('/', '\\'));
        }

        string looseDir = Path.Combine(_install.GameDataRoot, dir);
        if (Directory.Exists(looseDir))
        {
            foreach (string f in Directory.EnumerateFiles(looseDir, filePattern, SearchOption.AllDirectories))
                results.Add(Path.GetRelativePath(_install.GameDataRoot, f).Replace('/', '\\'));
        }

        if (IsMapPath(dir))
        {
            foreach (string region in MapRegionCandidates(dir))
            {
                string packPath = Path.Combine(_install.GameDataRoot, "pack", "map", "ipk",
                    region.EndsWith(".ipk", StringComparison.OrdinalIgnoreCase) ? region : region + ".ipk");
                if (!File.Exists(packPath)) continue;
                using var stream = File.OpenRead(packPath);
                var archive = ArchiveReader.ReadArchive(stream);
                string stripped = dir[4..];
                foreach (var entry in archive.Entries)
                {
                    string? vfsForm = null;
                    if (entry.Name.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase))
                        vfsForm = entry.Name;
                    else if (entry.Name.StartsWith(stripped + "\\", StringComparison.OrdinalIgnoreCase))
                        vfsForm = "map\\" + entry.Name;
                    if (vfsForm is null) continue;
                    if (!System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                            filePattern, Path.GetFileName(entry.Name)))
                        continue;
                    results.Add(vfsForm);
                }
            }
            var mapList = results.ToList();
            mapList.Sort(StringComparer.OrdinalIgnoreCase);
            return mapList;
        }

        var (ipkPath, innerPrefix) = dir.Contains('\\')
            ? LocateIPK(dir)
            : (Path.Combine(_install.GameDataRoot, "pack", $"{dir}.ipk"), "");
        if (ipkPath is not null && File.Exists(ipkPath))
        {
            string topSegment = dir.Split('\\')[0];
            using var stream = File.OpenRead(ipkPath);
            var archive = ArchiveReader.ReadArchive(stream);
            foreach (var entry in archive.Entries)
            {
                if (innerPrefix.Length > 0
                    && !entry.Name.StartsWith(innerPrefix + "\\", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                        filePattern, Path.GetFileName(entry.Name)))
                    continue;
                results.Add(topSegment + "\\" + entry.Name);
            }
        }

        var list = results.ToList();
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    private string? OverlayPath(string vfs)
    {
        foreach (string root in _install.Overlays)
        {
            string abs = Path.Combine(root, vfs);
            if (File.Exists(abs)) return abs;
        }
        return null;
    }

    private static string Normalize(string vfsPath) => vfsPath.Replace('/', '\\').TrimStart('\\');

    private static IEnumerable<string> LooseCandidates(string vfs)
    {
        string[] p = vfs.Split('\\');
        if (p.Length >= 4 && p[0].Equals("chara", StringComparison.OrdinalIgnoreCase))
        {
            string cls = p[1], id = p[2], rest = string.Join('\\', p[3..]);
            yield return $@"chara\ipk\{id}\{cls}\{id}\{rest}";
        }
    }

    private IEnumerable<string> MapRegionCandidates(string vfs)
    {
        string[] p = vfs.Split('\\');
        if (p.Length < 3) yield break;
        string area = p[2];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string ipk in Manifests.MapToIPK.Values)
        {
            string stem = ipk.EndsWith(".ipk", StringComparison.OrdinalIgnoreCase) ? ipk[..^4] : ipk;
            if ((stem.Equals(area, StringComparison.OrdinalIgnoreCase)
                 || stem.StartsWith(area + "_", StringComparison.OrdinalIgnoreCase))
                && seen.Add(stem))
                yield return ipk;
        }
    }

    private static bool IsMapPath(string vfs) =>
        vfs.StartsWith(@"map\", StringComparison.OrdinalIgnoreCase);

    private (string? ipkPath, string innerNeedle) LocateIPK(string vfs)
    {
        string[] p = vfs.Split('\\');
        if (p.Length >= 4 && p[0].Equals("chara", StringComparison.OrdinalIgnoreCase))
        {
            string rigIPK = Manifests.IPKForRig(p[2]) ?? $"{p[2]}.ipk";
            return (Path.Combine(_install.GameDataRoot, "pack", "chara", "ipk", rigIPK),
                    string.Join('\\', p[1..]));
        }
        if (p.Length >= 2 && p[0].Equals("map", StringComparison.OrdinalIgnoreCase))
        {
            string? owner = null;
            foreach (string region in MapRegionCandidates(vfs))
            {
                owner ??= region;
                if (new MapRegionReader(_install, region).Exists(vfs)) { owner = region; break; }
            }
            return (owner is null ? null
                : Path.Combine(_install.GameDataRoot, "pack", "map", "ipk", owner),
                string.Join('\\', p[1..]));
        }
        if (p.Length >= 2)
            return (Path.Combine(_install.GameDataRoot, "pack", $"{p[0]}.ipk"),
                    string.Join('\\', p[1..]));
        return (null, "");
    }

    private bool TryReadFromIPK(string vfs, out byte[] bytes)
    {
        bytes = [];
        var (ipkPath, needle) = LocateIPK(vfs);
        if (ipkPath is null || !File.Exists(ipkPath)) return false;
        using var stream = File.OpenRead(ipkPath);
        var archive = ArchiveReader.ReadArchive(stream);
        var entry = archive.Entries.FirstOrDefault(e => MatchesNeedle(e.Name, needle));
        if (entry is null) return false;
        using var ms = new MemoryStream();
        ArchiveReader.ExtractEntry(stream, entry, ms, archive.UsesZlib);
        bytes = ms.ToArray();
        return true;
    }

    private static bool MatchesNeedle(string entryName, string needle) =>
        entryName.EndsWith(needle, StringComparison.OrdinalIgnoreCase)
        || entryName.Equals(needle, StringComparison.OrdinalIgnoreCase);
}
