using System.Text;

namespace ShadowForge.GameData;

/// <summary>
/// The game's own display names, from the UTF-16 CSVs under !necessity\bd_gamedat:
/// namelist_chr_{lang}.u16 (model id to character name) and namelist_map_{lang}.u16 (stage
/// id to place name, one row per warp point). Rows marked deleted or named "-" are ignored.
/// A table that is missing or unreadable is empty, so callers fall back to the id.
/// </summary>
public sealed class NameTables
{
    private const string Dir = @"!necessity\bd_gamedat";

    private readonly Dictionary<string, string> _characters;
    private readonly Dictionary<string, string> _stages;

    public static NameTables Empty { get; } = new([], []);

    private NameTables(Dictionary<string, string> characters, Dictionary<string, string> stages)
    {
        _characters = characters;
        _stages = stages;
    }

    /// <param name="files">The game data to read the tables from.</param>
    /// <param name="language">The table suffix: "us" (English), "de" or "es".</param>
    public static NameTables Load(GameFileSystem files, string language = "us") => new(
        ReadTable(files, $@"{Dir}\namelist_chr_{language}.u16", idColumn: 1, nameColumns: [2]),
        ReadTable(files, $@"{Dir}\namelist_map_{language}.u16", idColumn: 1, nameColumns: [3, 5]));

    internal static NameTables FromText(string characterCsv, string stageCsv) => new(
        Parse(characterCsv, idColumn: 1, nameColumns: [2]),
        Parse(stageCsv, idColumn: 1, nameColumns: [3, 5]));

    /// <summary>
    /// The name of a model id. A variant with no row of its own (bs27_a, np003_b) takes its
    /// base id's name, and a bare NPC id (ch101) takes its first costume's (ch101_00).
    /// </summary>
    public string? Character(string id)
    {
        if (_characters.TryGetValue(id, out string? name)) return name;
        if (_characters.TryGetValue(id + "_00", out name)) return name;
        int cut = id.LastIndexOf('_');
        return cut > 0 && _characters.TryGetValue(id[..cut], out name) ? name : null;
    }

    /// <summary>
    /// The place name of a stage id. A stage with no row of its own takes the name of the
    /// first named stage in the same area (bg13_01 from bg13_02).
    /// </summary>
    public string? Stage(string stageId)
    {
        if (_stages.TryGetValue(stageId, out string? name)) return name;
        int cut = stageId.LastIndexOf('_');
        if (cut <= 0) return null;
        string area = stageId[..(cut + 1)];
        return _stages
            .Where(kv => kv.Key.StartsWith(area, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }

    private static Dictionary<string, string> ReadTable(GameFileSystem files, string vfs, int idColumn, int[] nameColumns)
    {
        try
        {
            return Parse(Decode(files.ReadVfs(vfs)), idColumn, nameColumns);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException
                                       or InvalidDataException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// UTF-16 with a byte-order mark, little-endian when there is none.
    /// </summary>
    private static string Decode(byte[] bytes)
    {
        if (bytes is [0xFE, 0xFF, ..]) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes is [0xFF, 0xFE, ..]) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        return Encoding.Unicode.GetString(bytes);
    }

    /// <summary>
    /// The first row is the header. Column 0 is the delete marker, so a row with anything in
    /// it is skipped. The first id wins, and within a row the first non-empty name column.
    /// </summary>
    private static Dictionary<string, string> Parse(string csv, int idColumn, int[] nameColumns)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ReadRecords(csv).Skip(1))
        {
            if (row.Count <= idColumn || row[0].Trim().Length > 0) continue;
            string id = row[idColumn].Trim();
            if (id.Length == 0 || names.ContainsKey(id)) continue;
            string? name = nameColumns
                .Where(c => c < row.Count)
                .Select(c => row[c].Trim())
                .FirstOrDefault(n => n.Length > 0 && n != "-");
            if (name is not null) names[id] = name;
        }
        return names;
    }

    /// <summary>
    /// CSV records, where a quoted field may hold commas, doubled quotes and line breaks.
    /// </summary>
    internal static IEnumerable<List<string>> ReadRecords(string csv)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (quoted)
            {
                if (c != '"') field.Append(c);
                else if (i + 1 < csv.Length && csv[i + 1] == '"') field.Append(csv[++i]);
                else quoted = false;
                continue;
            }
            switch (c)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    yield return row;
                    row = [];
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            yield return row;
        }
    }
}
