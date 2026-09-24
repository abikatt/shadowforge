using System.Text;
using ShadowForge.GameData.Mods;

namespace ShadowForge.Tests.GameData.Mods;

public sealed class DbOverlayTests
{
    private const string Vanilla = "\"model_pc01.mdl\",\"pc01.ipk\"\r\n" +
                                   "\"model_pc02.mdl\",\"pc02.ipk\"\r\n";

    private const string Vfs = @"pack\chara\pack_chr_model.txt";

    private static DbOverlay OverlayWithBase(byte[] baseBytes)
    {
        string overlay = GameDataFixture.NewTempDir("sf_db_");
        string dest = Path.Combine(overlay, "pack", "chara", "pack_chr_model.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllBytes(dest, baseBytes);
        return new DbOverlay(GameDataFixture.Install().WithOverlays(new[] { overlay }));
    }

    private static DbOverlay OverlayWithBase(string baseText) =>
        OverlayWithBase(Encoding.ASCII.GetBytes(baseText));

    private static DbOverlay Overlay() => OverlayWithBase(Vanilla);

    [Fact]
    public void Apply_AppendsRowsAfterTheVanillaBase()
    {
        byte[] result = Overlay().Apply(Vfs, new[] { "\"model_pc11.mdl\",\"np109.ipk\"" });
        string text = Encoding.ASCII.GetString(result);

        Assert.StartsWith(Vanilla, text);
        Assert.EndsWith("\"model_pc11.mdl\",\"np109.ipk\"\r\n", text);
    }

    [Fact]
    public void Apply_PreservesTheBaseByteForByteWhenNoRowsAreDeclared()
    {
        byte[] result = Overlay().Apply(Vfs, Array.Empty<string>());

        Assert.Equal(Encoding.ASCII.GetBytes(Vanilla), result);
    }

    [Fact]
    public void Apply_AppendsEveryRowInDeclaredOrder()
    {
        byte[] result = Overlay().Apply(Vfs, new[]
        {
            "\"model_pc11.mdl\",\"np109.ipk\"",
            "\"model_pc12.mdl\",\"np132.ipk\"",
        });
        string text = Encoding.ASCII.GetString(result);

        Assert.True(text.IndexOf("pc11", StringComparison.Ordinal)
                  < text.IndexOf("pc12", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_RejectsARowAlreadyPresentInTheBase()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Overlay().Apply(Vfs, new[] { "\"model_pc02.mdl\",\"pc02.ipk\"" }));

        Assert.Contains("already present", ex.Message);
    }

    [Fact]
    public void Apply_RejectsADuplicateWithinTheDeclaredRows()
    {
        var row = "\"model_pc11.mdl\",\"np109.ipk\"";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            Overlay().Apply(Vfs, new[] { row, row }));

        Assert.Contains("declared twice", ex.Message);
    }

    [Fact]
    public void Apply_PreservesShiftJISBytesInTheBase()
    {
        var shiftJisBase = new byte[] { 0x82, 0xa0, 0x82, 0xa2, 0x0d, 0x0a };

        byte[] result = OverlayWithBase(shiftJisBase).Apply(Vfs, new[] { "test" });

        Assert.Equal(shiftJisBase, result.AsSpan(0, shiftJisBase.Length).ToArray());
    }

    [Fact]
    public void Apply_HandlesBaseNotEndingWithNewline()
    {
        byte[] result = OverlayWithBase("\"model_pc01.mdl\",\"pc01.ipk\"")
            .Apply(Vfs, new[] { "\"model_pc11.mdl\",\"np109.ipk\"" });
        string text = Encoding.ASCII.GetString(result);

        Assert.Contains("\"model_pc01.mdl\",\"pc01.ipk\"\r\n", text);
        Assert.EndsWith("\"model_pc11.mdl\",\"np109.ipk\"\r\n", text);
    }

    [Fact]
    public void Apply_HandlesEmptyBase()
    {
        byte[] result = OverlayWithBase("").Apply(Vfs, new[] { "\"model_pc11.mdl\",\"np109.ipk\"" });

        Assert.Equal("\"model_pc11.mdl\",\"np109.ipk\"\r\n", Encoding.ASCII.GetString(result));
    }

    [Fact]
    public void Apply_RejectsBlankLineAsRedeclaration()
    {
        var db = OverlayWithBase("\"model_pc01.mdl\",\"pc01.ipk\"\r\n\r\nline3\r\n");

        var ex = Assert.Throws<InvalidOperationException>(() => db.Apply(Vfs, new[] { "" }));

        Assert.Contains("already present", ex.Message);
    }
}
