using ShadowForge.GameData.Entities;

namespace ShadowForge.Tests.GameData.Entities;

public sealed class EntityIdTests
{
    [Theory]
    [InlineData("pc01", "ply")]
    [InlineData("em001", "ene")]
    [InlineData("bs46", "ene")]
    [InlineData("np035", "npc")]
    [InlineData("sw01", "sdw")]
    [InlineData("mt01", "mct")]
    public void ClassFor_MapsPrefixToClass(string id, string expected)
        => Assert.Equal(expected, EntityId.ClassFor(id));

    [Fact]
    public void ClassFor_UnknownPrefix_Null() => Assert.Null(EntityId.ClassFor("zz99"));

    [Theory]
    [InlineData("pc01", true)]
    [InlineData("em001", true)]
    [InlineData("bs46_a", true)]
    [InlineData(@"path\model_pc01.mdl", false)]
    [InlineData("pc01_obj.hdb", false)]
    [InlineData("randomword", false)]
    public void IsEntityId_DistinguishesIdsFromPaths(string s, bool expected)
        => Assert.Equal(expected, EntityId.IsEntityId(s));

    [Theory]
    [InlineData("model_em901.mdl", "em901")]
    [InlineData("MODEL_bs46_a.MDL", "bs46_a")]
    [InlineData("em901_obj.hdb", null)]
    public void FromModelDefFileName_ExtractsTheId(string fileName, string? expected)
        => Assert.Equal(expected, EntityId.FromModelDefFileName(fileName));
}
