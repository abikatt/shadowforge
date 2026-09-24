namespace ShadowForge.Manifests;

public sealed record EntityListDto(IReadOnlyList<CatalogEntryDto> Entities);

public sealed record CatalogEntryDto(string Id, string Category, string Class, string ModelDefPath, string DisplayName);
