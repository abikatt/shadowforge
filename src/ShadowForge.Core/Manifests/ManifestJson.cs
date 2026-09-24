using System.Text.Json.Serialization;

namespace ShadowForge.Manifests;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EntityManifest))]
[JsonSerializable(typeof(ResolvedEntityDto))]
[JsonSerializable(typeof(EntityListDto))]
[JsonSerializable(typeof(MapListDto))]
[JsonSerializable(typeof(MapManifest))]
public partial class ManifestJson : JsonSerializerContext
{
}
