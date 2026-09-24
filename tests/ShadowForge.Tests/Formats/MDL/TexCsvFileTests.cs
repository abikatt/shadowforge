using ShadowForge.Formats.MDL;

namespace ShadowForge.Tests.Formats.MDL;

public sealed class TexCsvFileTests
{
    [Fact]
    public void Parse_ReadsOrderedDDSNames()
    {
        var csv = TexCsvFile.Parse(
            "HDB,sw03_obj\nNUM,6\nDDS,voltex_bs12_02\nDDS,bs12_02_n\n" +
            "DDS,voltex_bs12_01\nDDS,bs12_01_n\nDDS,bs12_02\nDDS,bs12_03\n");
        Assert.Equal("sw03_obj", csv.HDBName);
        Assert.Equal(6, csv.DDSNames.Count);
        Assert.Equal("voltex_bs12_02", csv.DDSNames[0]);
        Assert.Equal("bs12_03", csv.DDSNames[5]);
    }

    [Fact]
    public void Parse_NumMismatch_Throws()
    {
        Assert.Throws<InvalidDataException>(() =>
            TexCsvFile.Parse("HDB,x\nNUM,3\nDDS,a\nDDS,b\n"));
    }
}
