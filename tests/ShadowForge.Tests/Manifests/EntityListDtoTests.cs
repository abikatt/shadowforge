using System.Text.Json;
using ShadowForge.Manifests;

namespace ShadowForge.Tests.Manifests;

public sealed class EntityListDtoTests
{
    [Fact]
    public void Serializes_CamelCase_EntitiesArray()
    {
        var dto = new EntityListDto(new[]
        {
            new CatalogEntryDto("pc01", "chara", "ply",
                @"database\model\chara\ply\model_pc01.mdl", "pc01"),
        });
        string json = JsonSerializer.Serialize(dto, ManifestJson.Default.EntityListDto);
        Assert.Contains("\"entities\"", json);
        Assert.Contains("\"id\": \"pc01\"", json);
        Assert.Contains("\"category\": \"chara\"", json);
        Assert.Contains("\"class\": \"ply\"", json);
        Assert.Contains("\"modelDefPath\"", json);
        Assert.Contains("\"displayName\": \"pc01\"", json);
    }
}
