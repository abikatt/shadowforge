namespace ShadowForge.Manifests;

public sealed record MapListDto(IReadOnlyList<MapStageDto> Stages);

public sealed record MapStageDto(string Id, string Category, string Class, string DisplayName, string RegionIPK, bool Available, int ModelCount);
