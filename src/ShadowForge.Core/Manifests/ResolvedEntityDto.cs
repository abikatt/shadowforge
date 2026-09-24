namespace ShadowForge.Manifests;

public sealed record ResolvedEntityDto(
    EntityInfo Entity, InstallInfo Install, ModelInfo Model, IReadOnlyList<FileInfoDto> Files);
