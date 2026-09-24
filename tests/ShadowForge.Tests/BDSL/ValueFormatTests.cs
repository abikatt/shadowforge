using ShadowForge.Formats.BDSL;
using ShadowForge.Scene.Script;

namespace ShadowForge.Tests.BDSL;

public sealed class ValueFormatTests
{
    private static readonly ArgSpec Int = new("n", ArgKind.Int);
    private static readonly ArgSpec Hex = new("n", ArgKind.Hex);
    private static readonly ArgSpec Flt = new("n", ArgKind.Float);
    private static readonly ArgSpec Var = new("n", ArgKind.Var);
    private static readonly ArgSpec Lbl = new("n", ArgKind.Label);
    private static readonly ArgSpec Chr = new("n", ArgKind.Enum, OpcodeTable.CharNames);

    [Theory]
    [InlineData(0u, "0")]
    [InlineData(1070u, "1070")]
    [InlineData(0x00FFFFFFu, "16777215")]
    [InlineData(0x01000000u, "0x1000000")]
    [InlineData(0xFFFFFFFFu, "0xFFFFFFFF")]
    public void Int_DecimalUnlessHighBits(uint value, string expected) =>
        Assert.Equal(expected, ValueFormat.Format(value, Int));

    [Fact]
    public void Hex_AlwaysHex() => Assert.Equal("0xB6E2E7", ValueFormat.Format(0xB6E2E7, Hex));

    [Theory]
    [InlineData(0x40800000u, "4.0")]
    [InlineData(0x3D4CCCCDu, "0.05")]
    [InlineData(0xC2C80000u, "-100.0")]
    [InlineData(0u, "0.0")]
    [InlineData(0x7FC00000u, "0x7FC00000")]
    [InlineData(0x00000001u, "0x1")]
    public void Float_ShortestRoundTrip_HexForOddBits(uint bits, string expected) =>
        Assert.Equal(expected, ValueFormat.Format(bits, Flt));

    [Fact]
    public void Var_Label_Enum()
    {
        Assert.Equal("var[3362]", ValueFormat.Format(3362, Var));
        Assert.Equal("@8", ValueFormat.Format(8, Lbl));
        Assert.Equal("kluke", ValueFormat.Format(2, Chr));
        Assert.Equal("all", ValueFormat.Format(0xFFFFFFFF, Chr));
        Assert.Equal("7", ValueFormat.Format(7, Chr));
    }

    [Theory]
    [InlineData("1070", 1070u)]
    [InlineData("0x1000000", 0x01000000u)]
    [InlineData("-1", 0xFFFFFFFFu)]
    [InlineData("var[3362]", 3362u)]
    [InlineData("@8", 8u)]
    [InlineData("kluke", 2u)]
    [InlineData("all", 0xFFFFFFFFu)]
    public void Parse_AcceptsEveryForm(string token, uint expected)
    {
        Assert.Equal(expected, ValueFormat.Parse(token, Int));
    }

    [Theory]
    [InlineData("4.0", 0x40800000u)]
    [InlineData("0.05", 0x3D4CCCCDu)]
    [InlineData("-100", 0xC2C80000u)]
    [InlineData("0x7FC00000", 0x7FC00000u)]
    public void Parse_FloatKind(string token, uint expected) =>
        Assert.Equal(expected, ValueFormat.Parse(token, Flt));

    [Fact]
    public void Parse_RejectsGarbage() =>
        Assert.Throws<FormatException>(() => ValueFormat.Parse("banana", Int));

    [Fact]
    public void Float_RoundTrip_RandomBits()
    {
        var rng = new Random(1234);
        for (int i = 0; i < 2000; i++)
        {
            uint bits = (uint)rng.Next() ^ ((uint)rng.Next() << 16);
            Assert.Equal(bits, ValueFormat.Parse(ValueFormat.Format(bits, Flt), Flt));
        }
    }
}
