using ShadowForge.Formats.Text;

namespace ShadowForge.Tests.Formats.Text;

public sealed class DslValueTests
{
    [Fact]
    public void FormatFloat_UsesSixDecimalsInvariant()
    {
        Assert.Equal("0.300000", DslValue.FormatFloat(0.3f));
        Assert.Equal("1.000000", DslValue.FormatFloat(1f));
        Assert.Equal("90.000000", DslValue.FormatFloat(90f));
    }

    [Fact]
    public void Unquote_StripsSurroundingQuotes()
    {
        Assert.Equal("pc01_obj.hdb", DslValue.Unquote("\"pc01_obj.hdb\""));
        Assert.Equal("bare", DslValue.Unquote("bare"));
    }
}
