using ShadowForge.Formats.HDB.Raw;

namespace ShadowForge.Tests.HDB;

/// <summary>
/// The texture record name field is 20 bytes, so names past 16 characters
/// such as "np109_eyelid_l_01" are stored whole. Assigning a shorter name
/// must not leave any of the previous one behind.
/// </summary>
public sealed class TextureNameRewriteTests
{
    [Fact]
    public void SetName_ShorterNameLeavesNothingOfTheLongerOriginal()
    {
        var rec = new RawTextureRecord
        {
            Name = "np114_eyelid_l_01",
            FloatParam = 0f,
            FlagField18 = 0,
        };

        rec.SetName("pc13_01");

        Assert.Equal("pc13_01", rec.Name);
    }

    [Fact]
    public void SetName_PreservesTheOtherFields()
    {
        var rec = new RawTextureRecord
        {
            Name = "np109_02_n", FloatParam = 2.5f, FlagField18 = 2,
        };

        rec.SetName("pc11_02_n");

        Assert.Equal("pc11_02_n", rec.Name);
        Assert.Equal(2.5f, rec.FloatParam);
        Assert.Equal(2u, rec.FlagField18);
    }

    [Fact]
    public void SetName_RejectsANameWiderThanTheField()
    {
        var rec = new RawTextureRecord { Name = "np109_02" };

        var ex = Assert.Throws<ArgumentException>(
            () => rec.SetName("a_very_long_texture_name"));
        Assert.Contains("20", ex.Message);
    }

    [Fact]
    public void SetName_AcceptsSeventeenBytes()
    {
        var rec = new RawTextureRecord { Name = "np109_02" };

        rec.SetName("np109_eyelid_l_01");

        Assert.Equal("np109_eyelid_l_01", rec.Name);
    }

    [Fact]
    public void SetName_AcceptsExactlyTwentyBytes()
    {
        var rec = new RawTextureRecord { Name = "np109_02" };

        rec.SetName("np109_eyelid_l_01_xy");

        Assert.Equal("np109_eyelid_l_01_xy", rec.Name);
    }
}
