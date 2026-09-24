namespace ShadowForge.Formats.MDL;

/// <summary>
/// A texture-override csv: HDB,{stem}, NUM,{n}, then n rows of DDS,{texture-stem}. The DDS
/// rows replace the HDB's texture-name table by ordinal, which is how a shared-rig reskin
/// points one skeleton at a different texture set.
/// </summary>
public sealed class TexCsvFile
{
    public string? HDBName { get; private init; }

    public IReadOnlyList<string> DDSNames { get; private init; } = [];

    public static TexCsvFile ReadFile(string path) => Parse(File.ReadAllText(path));

    /// <summary>
    /// Throws when NUM disagrees with the number of DDS rows.
    /// </summary>
    public static TexCsvFile Parse(string text)
    {
        string? hdb = null;
        int? num = null;
        var dds = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var parts = raw.Trim().Split(',', 2);
            if (parts.Length != 2) continue;
            switch (parts[0].Trim().ToUpperInvariant())
            {
                case "HDB": hdb = parts[1].Trim(); break;
                case "NUM": if (int.TryParse(parts[1].Trim(), out int n)) num = n; break;
                case "DDS": dds.Add(parts[1].Trim()); break;
            }
        }
        if (num is int expected && expected != dds.Count)
            throw new InvalidDataException(
                $"Texture csv NUM={expected} but {dds.Count} DDS rows - refusing to remap.");
        return new TexCsvFile { HDBName = hdb, DDSNames = dds };
    }
}
