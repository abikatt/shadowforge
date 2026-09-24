using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Raw;
using ShadowForge.Formats.IPK;
using ShadowForge.Formats.MDL;

namespace ShadowForge.GameData.Entities;

public sealed record DerivedEntity(string Id, string SourceId, IReadOnlyList<string> Files);

/// <summary>
/// Writes a complete entity directory for an id the game does not ship by re-iding an existing
/// one. The source rig id is replaced by the new id in file names, the model def bindings, the
/// .hdb texture table, the motion pack entry names and rig .csv files.
/// </summary>
public sealed class EntityDeriver
{
    private readonly EntityAssets _assets;
    private readonly EntityResolver _resolver;
    private readonly GameFileSystem _gfs;

    public EntityDeriver(GameInstall install)
    {
        _assets = new EntityAssets(install);
        _resolver = new EntityResolver(install);
        _gfs = new GameFileSystem(install);
    }

    public DerivedEntity Derive(string sourceId, string newId, string newClass, string outDir)
    {
        ResolvedEntity source = _resolver.Resolve(sourceId);
        string rigToken = source.RigId;

        Directory.CreateDirectory(outDir);
        string staging = Path.Combine(outDir, ".rig");
        _assets.MaterializeRig(sourceId, staging);

        var written = new List<string>();
        try
        {
            foreach (string file in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
            {
                string dest = Path.Combine(outDir, Path.GetFileName(file).Replace(rigToken, newId));
                File.WriteAllBytes(dest, Transform(File.ReadAllBytes(file), file, rigToken, newId));
                written.Add(dest);
            }
        }
        finally
        {
            DirectoryTree.DeleteStaging(staging);
        }

        written.Add(WriteModelDef(source, rigToken, newId, newClass, outDir));
        written.Sort(StringComparer.OrdinalIgnoreCase);
        return new DerivedEntity(newId, sourceId, written);
    }

    private static byte[] Transform(byte[] bytes, string path, string from, string to) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".hdb" => RenameHDBTextures(bytes, from, to),
            ".mpk" => EntryRenamer.Token(bytes, from, to),
            ".csv" => RenameTextBytes(bytes, from, to),
            _ => bytes,
        };

    private static byte[] RenameHDBTextures(byte[] bytes, string from, string to)
    {
        var raw = ModelReader.Read(bytes);
        foreach (var table in raw.FirstTable.OfType<RawTextureTableEntry>())
            foreach (var rec in table.Records)
                if (rec.Name.Contains(from, StringComparison.Ordinal))
                    rec.SetName(rec.Name.Replace(from, to));
        return ModelWriter.Write(raw);
    }

    private static byte[] RenameTextBytes(byte[] bytes, string from, string to)
    {
        string text = EncodingExtensions.DecodeShiftJISRaw(bytes, 0, bytes.Length);
        return text.Replace(from, to).EncodeShiftJIS();
    }

    private string WriteModelDef(
        ResolvedEntity source, string from, string to, string newClass, string outDir)
    {
        var def = source.Files.First(f => f.Role == FileRole.ModelDef);
        var mdl = ModelDef.Read(_gfs.ReadVfs(def.VfsPath));

        mdl.RenameAssetToken(from, to);
        mdl.SetPath($@"chara\{newClass}\{to}\");

        string dest = Path.Combine(outDir, EntityId.ModelDefFileName(to));
        File.WriteAllBytes(dest, mdl.Write());
        return dest;
    }
}
