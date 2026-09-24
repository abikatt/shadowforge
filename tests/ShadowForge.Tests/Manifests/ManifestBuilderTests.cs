using System.Text.Json;
using ShadowForge.Manifests;
using ShadowForge.GameData;
using ShadowForge.GameData.Entities;

namespace ShadowForge.Tests.Manifests;

public sealed class ManifestBuilderTests
{
    private static string FixtureRoot => Path.Combine(AppContext.BaseDirectory, "GameData/Fixtures/gamedata");

    [Fact]
    public void Resolve_ProjectsEntity_WithCamelCaseJson()
    {
        var install = GameInstall.Locate(FixtureRoot);
        var e = new EntityResolver(install).Resolve("em901");
        var dto = ManifestBuilder.Resolve(e, install);

        Assert.Equal("em901", dto.Entity.Id);
        Assert.Contains(dto.Files, f => f.Role == "Skeleton");

        string json = JsonSerializer.Serialize(dto, ManifestJson.Default.ResolvedEntityDto);
        Assert.Contains("\"entity\"", json);
        Assert.Contains("\"ipkInnerPath\"", json);
        Assert.DoesNotContain("\"Entity\"", json);
    }
}
