namespace ShadowForge.Manifests;

/// <summary>
/// The "{id}.sfmod.json" file written beside an exported entity's GLB.
/// </summary>
public sealed record EntityManifest(
    int Schema, EntityInfo Entity, InstallInfo Install, ModelInfo Model,
    IReadOnlyList<FileInfoDto> Files, IReadOnlyList<TextureInfo> Textures, ExportInfo Export);

public sealed record EntityInfo(string Id, string Class, string RigId, string RigClass);

public sealed record InstallInfo(string GameRoot, string Source, string? ModsRoot, bool ModsAvailable);

public sealed record ClipInfo(string Name, string HMB);

public sealed record ModelInfo(
    string? Path, string? ObjectHDB, string? ObjectL0HDB, string? MotPack, IReadOnlyList<ClipInfo> Clips,
    string? TextureOverrideCsv, string? FurLen, string? Face, string? MotPackF, string? MotPackB,
    IReadOnlyList<ObjectOptInfo> ObjectOpts, IReadOnlyList<ClipInfo> Motions);

public sealed record ObjectOptInfo(int Slot, string HDB);

public sealed record FileInfoDto(string Role, string VfsPath, string LooseRelPath, string IPKName, string IPKInnerPath, bool Exists);

public sealed record TextureInfo(string File, string? Material);

public sealed record ExportInfo(string Glb, string UpAxis, string Unit, int ClipCount);
