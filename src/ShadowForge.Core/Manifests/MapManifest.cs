namespace ShadowForge.Manifests;

public sealed record MapManifest(
    int Schema, string StageId, string Category, string RegionIPK,
    IReadOnlyList<MapModelEntry> Models, IReadOnlyList<MapSkipEntry> Skipped, MapExportInfo Export);

public sealed record MapModelEntry(string Name, string ObjectHDB, int Area, float Pri, bool Exported);

public sealed record MapSkipEntry(string Kind, string Path, string Reason);

public sealed record MapExportInfo(string Glb, string UpAxis, string Unit);
