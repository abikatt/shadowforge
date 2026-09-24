using System.Text.Json;
using static ShadowForge.Tests.CLI.EntityCommandRunner;

namespace ShadowForge.Tests.CLI;

public sealed class EntityJsonTests
{
    [Fact]
    public void Export_UnknownId_EmitsErrorEnvelope()
    {
        var (code, output) = Run("export", "zz99", "--json", "--game-root", MissingPath("no_such_root_"));
        Assert.Equal(1, code);
        using var doc = JsonDocument.Parse(output);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("entity.export", doc.RootElement.GetProperty("verb").GetString());
        Assert.NotEqual(JsonValueKind.Null, doc.RootElement.GetProperty("error").ValueKind);
    }

    [Fact]
    public void Import_MissingFromDir_EmitsErrorEnvelope()
    {
        var (code, output) = Run("import", "zz99", "--from", MissingPath("no_such_"), "--json",
            "--game-root", MissingPath("no_root_"));
        Assert.Equal(1, code);
        using var doc = JsonDocument.Parse(output);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("entity.import", doc.RootElement.GetProperty("verb").GetString());
    }

    [Fact]
    public void Deploy_MissingArgs_EmitsErrorEnvelope()
    {
        var (code, output) = Run("deploy", "zz99", "--from", MissingPath("no_"),
            "--mod", "TestMod", "--json", "--game-root", MissingPath("nr_"));
        Assert.Equal(1, code);
        using var doc = JsonDocument.Parse(output);
        Assert.Equal("entity.deploy", doc.RootElement.GetProperty("verb").GetString());
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void Derive_UnknownClass_EmitsErrorEnvelope()
    {
        var (code, output) = Run("derive", "zz99", "--as", "zz99", "--out", MissingPath("no_out_"), "--json",
            "--game-root", MissingPath("no_such_root_"));
        Assert.Equal(1, code);
        using var doc = JsonDocument.Parse(output);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("entity.derive", doc.RootElement.GetProperty("verb").GetString());
        Assert.NotEqual(JsonValueKind.Null, doc.RootElement.GetProperty("error").ValueKind);
    }
}
