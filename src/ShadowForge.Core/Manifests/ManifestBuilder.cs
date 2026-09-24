using ShadowForge.Formats.MDL;
using ShadowForge.GameData;

namespace ShadowForge.Manifests;

public static class ManifestBuilder
{
    public static ResolvedEntityDto Resolve(ResolvedEntity e, GameInstall install) =>
        new(Entity(e), Install(install), Model(e.ModelDef), Files(e));

    public static EntityManifest ForExport(
        ResolvedEntity e, GameInstall install,
        IReadOnlyList<string> textureFiles, string glbName, int clipCount) =>
        new(1, Entity(e), Install(install), Model(e.ModelDef), Files(e),
            textureFiles.Select(t => new TextureInfo(System.IO.Path.GetFileName(t), null)).ToList(),
            new ExportInfo(glbName, "Y", "meter", clipCount));

    private static EntityInfo Entity(ResolvedEntity e) => new(e.Id, e.Class, e.RigId, e.RigClass);

    private static InstallInfo Install(GameInstall i) =>
        new(i.GameDataRoot, i.Source.ToString(), i.ModsRoot, i.ModsRoot is not null);

    private static ModelInfo Model(ModelDef m) =>
        new(m.Path, m.ObjectHDB, m.ObjectL0HDB, m.MotPack,
            m.Clips.Select(c => new ClipInfo(c.Name, c.HMBFile)).ToList(),
            m.TextureOverrideCsv, m.FurLen, m.Face, m.MotPackF, m.MotPackB,
            m.ObjectOpts.Select(o => new ObjectOptInfo(o.Slot, o.HDBFile)).ToList(),
            m.Motions.Select(c => new ClipInfo(c.Name, c.HMBFile)).ToList());

    private static IReadOnlyList<FileInfoDto> Files(ResolvedEntity e) =>
        e.Files.Select(f => new FileInfoDto(
            f.Role.ToString(), f.VfsPath, f.LooseRelPath, f.IPKName, f.IPKInnerPath, f.Exists)).ToList();
}
