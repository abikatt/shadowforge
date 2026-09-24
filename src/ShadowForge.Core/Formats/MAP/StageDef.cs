using System.Globalization;
using ShadowForge.Formats.Text;

namespace ShadowForge.Formats.MAP;

/// <summary>
/// A .map file: environment keys, PARTS sidecar references and MODEL blocks, each an
/// OBJECT .hdb plus transform and flags. Unmodified files round-trip byte for byte.
/// </summary>
public sealed class StageDef
{
    private readonly LineDocument _doc;

    private StageDef(LineDocument doc) => _doc = doc;

    public static StageDef Read(byte[] bytes) => new(LineDocument.Parse(bytes));

    public static StageDef ReadFile(string path) => Read(File.ReadAllBytes(path));

    public byte[] Write() => _doc.ToBytes();

    public IReadOnlyList<PartRef> Parts
    {
        get
        {
            var list = new List<PartRef>();
            foreach (var line in _doc.Lines)
            {
                var fields = DslValue.SplitFields(line.Content);
                if (fields.Length >= 3 && fields[0] == "PARTS")
                    list.Add(new PartRef(fields[1], DslValue.FirstQuoted(fields[2])));
            }
            return list;
        }
    }

    public IReadOnlyList<ModelRef> Models
    {
        get
        {
            var list = new List<ModelRef>();
            string? name = null;
            int area = 0;
            float pri = 0f;
            string? objectHDB = null;
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in _doc.Lines)
            {
                string trimmed = line.Content.TrimStart();
                if (trimmed.StartsWith("<MODEL "))
                {
                    name = QuotedAttribute(trimmed, "NAME");
                    area = int.TryParse(Attribute(trimmed, "AREA"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int a) ? a : 0;
                    pri = float.TryParse(Attribute(trimmed, "PRI"), NumberStyles.Float, CultureInfo.InvariantCulture, out float p) ? p : 0f;
                    objectHDB = null;
                    settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                if (trimmed.StartsWith("</MODEL>"))
                {
                    if (name != null)
                        list.Add(new ModelRef { Name = name, ObjectHDB = objectHDB, Area = area, Pri = pri, Settings = settings });
                    name = null;
                    continue;
                }
                if (name == null) continue;
                var fields = DslValue.SplitFields(line.Content);
                if (fields.Length < 2) continue;
                if (fields[0] == "OBJECT")
                    objectHDB = DslValue.Unquote(fields[1].Trim());
                else
                    settings[fields[0]] = string.Join("\t", fields[1..]);
            }
            return list;
        }
    }

    private static string? QuotedAttribute(string header, string attr)
    {
        int key = header.IndexOf(attr + "=\"", StringComparison.Ordinal);
        if (key < 0) return null;
        int start = key + attr.Length + 2;
        int end = header.IndexOf('"', start);
        return end < 0 ? null : header[start..end];
    }

    /// <summary>
    /// Value of ATTR=value or ATTR="value". A bare value ends at a space, a tab or the closing angle bracket.
    /// </summary>
    private static string? Attribute(string header, string attr)
    {
        int key = header.IndexOf(attr + "=", StringComparison.Ordinal);
        if (key < 0) return null;
        int start = key + attr.Length + 1;
        if (start >= header.Length) return null;
        if (header[start] == '"') return QuotedAttribute(header, attr);
        int end = header.IndexOfAny([' ', '\t', '>'], start);
        return end < 0 ? header[start..] : header[start..end];
    }
}
