using ShadowForge.GameData.Entities;

namespace ShadowForge.Tests.GameData.Entities;

public sealed class EntityCatalogTests
{
    [Fact]
    public void ListChara_ReturnsIdClassCategoryFromModelDef()
    {
        var entries = new EntityCatalog(GameDataFixture.Install()).ListChara();
        var em901 = entries.SingleOrDefault(e => e.Id == "em901");
        Assert.NotNull(em901);
        Assert.Equal("chara", em901!.Category);
        Assert.Equal("ene", em901.Class);
        Assert.Equal("em901", em901.DisplayName);
        Assert.Equal(@"database\model\chara\ene\model_em901.mdl", em901.ModelDefRelPath);
    }

    [Fact]
    public void ListChara_IsDedupedAndSortedById()
    {
        var ids = new EntityCatalog(GameDataFixture.Install()).ListChara()
            .Select(e => e.Id).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
        Assert.Equal(ids.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), ids);
    }
}
