using System.Text;
using ShadowForge.Formats.Text;

namespace ShadowForge.Formats.MDL;

/// <summary>
/// A .mdl file: binds one object's skeleton (OBJECT), optional shadow LOD (OBJECTL0),
/// motion pack (MOTPACK) and named clips (MOTINPK). Values are tab-separated and a
/// line starting with // is a comment. Unmodified files round-trip byte for byte.
/// </summary>
public sealed class ModelDef
{
    private readonly LineDocument _doc;

    private ModelDef(LineDocument doc) => _doc = doc;

    public static ModelDef Read(byte[] bytes) => new(LineDocument.Parse(bytes));

    public static ModelDef ReadFile(string path) => Read(File.ReadAllBytes(path));

    public byte[] Write() => _doc.ToBytes();

    public string? Path => QuotedValue("PATH");

    public string? ObjectHDB => QuotedValue("OBJECT");

    public string? ObjectL0HDB => QuotedValue("OBJECTL0");

    public string? MotPack => QuotedValue("MOTPACK");

    public string? Face => QuotedValue("FACE");

    public string? MotPackF => QuotedValue("MOTPACK_F");

    public string? MotPackB => QuotedValue("MOTPACK_B");

    /// <summary>
    /// The unquoted "length layer-count" text.
    /// </summary>
    public string? FurLen
    {
        get
        {
            var fields = KeyFields("FURLEN");
            return fields is null ? null : fields[1].Trim();
        }
    }

    /// <summary>
    /// The second quoted string on the OBJECT line: a texture-override csv that remaps the
    /// HDB texture table for a shared-rig reskin (bs12 uses sw03_tex_a.csv). Retail files put
    /// it either in its own tab field or after a space in the HDB's field, so the whole line is
    /// scanned for quote pairs. A // comment ends the scan.
    /// </summary>
    public string? TextureOverrideCsv
    {
        get
        {
            int i = FindKeyLine("OBJECT");
            if (i < 0) return null;
            string content = _doc.Lines[i].Content;
            int comment = content.IndexOf("//", StringComparison.Ordinal);
            string scan = comment >= 0 ? content[..comment] : content;
            if (!DslValue.TryFindQuoted(scan, 0, out _, out int firstClose)) return null;
            if (!DslValue.TryFindQuoted(scan, firstClose + 1, out int open, out int close)) return null;
            string second = scan[(open + 1)..close];
            return second.Length == 0 ? null : second;
        }
    }

    public IReadOnlyList<ObjectOpt> ObjectOpts
    {
        get
        {
            var list = new List<ObjectOpt>();
            foreach (var line in _doc.Lines)
            {
                if (IsComment(line.Content)) continue;
                var fields = DslValue.SplitFields(line.Content);
                if (fields.Length < 2 || fields[0] != "OBJECTOPT") continue;
                string payload = fields[1];
                if (!DslValue.TryFindQuoted(payload, 0, out int open, out int close)) continue;
                int slot = int.TryParse(payload[..open].Trim(), out int s) ? s : 0;
                list.Add(new ObjectOpt(slot, payload[(open + 1)..close]));
            }
            return list;
        }
    }

    /// <summary>
    /// Bare MOTION lines (loose .hmb files, mct models), laid out like MOTINPK.
    /// </summary>
    public IReadOnlyList<Clip> Motions => ClipsForKey("MOTION");

    public IReadOnlyList<Clip> Clips => ClipsForKey("MOTINPK");

    public void SetObjectHDB(string hdbFileName) => SetQuotedValue("OBJECT", hdbFileName);

    public void SetPath(string rigDir) => SetQuotedValue("PATH", rigDir);

    /// <summary>
    /// Replaces an asset-name token inside every quoted value, leaving clip names, trailing
    /// flags and comments untouched. FACE is skipped: it names a shared directory, not this
    /// rig's assets. Returns the number of lines changed.
    /// </summary>
    public int RenameAssetToken(string from, string to)
    {
        int changed = 0;
        for (int i = 0; i < _doc.Lines.Count; i++)
        {
            string content = _doc.Lines[i].Content;
            if (IsComment(content)) continue;
            var fields = DslValue.SplitFields(content);
            if (fields.Length >= 1 && fields[0] == "FACE") continue;

            string rewritten = ReplaceInsideQuotes(content, from, to);
            if (rewritten == content) continue;
            _doc.ReplaceContent(i, rewritten);
            changed++;
        }
        return changed;
    }

    private static bool IsComment(string content) => content.TrimStart().StartsWith("//");

    private int FindKeyLine(string key)
    {
        for (int i = 0; i < _doc.Lines.Count; i++)
        {
            var content = _doc.Lines[i].Content;
            if (IsComment(content)) continue;
            var fields = DslValue.SplitFields(content);
            if (fields.Length >= 2 && fields[0] == key) return i;
        }
        return -1;
    }

    private string[]? KeyFields(string key)
    {
        int i = FindKeyLine(key);
        return i < 0 ? null : DslValue.SplitFields(_doc.Lines[i].Content);
    }

    private string? QuotedValue(string key)
    {
        var fields = KeyFields(key);
        return fields is null ? null : DslValue.FirstQuoted(fields[1]);
    }

    private List<Clip> ClipsForKey(string key)
    {
        var list = new List<Clip>();
        foreach (var line in _doc.Lines)
        {
            if (IsComment(line.Content)) continue;
            var fields = DslValue.SplitFields(line.Content);
            if (fields.Length >= 3 && fields[0] == key)
                list.Add(new Clip(fields[1], DslValue.Unquote(fields[2].Trim().TrimEnd(','))));
        }
        return list;
    }

    private static string ReplaceInsideQuotes(string content, string from, string to)
    {
        var sb = new StringBuilder(content.Length);
        int pos = 0;
        while (DslValue.TryFindQuoted(content, pos, out int open, out int close))
        {
            sb.Append(content, pos, open + 1 - pos);
            sb.Append(content[(open + 1)..close].Replace(from, to));
            sb.Append('"');
            pos = close + 1;
        }
        sb.Append(content, pos, content.Length - pos);
        return sb.ToString();
    }

    private void SetQuotedValue(string key, string newValue)
    {
        int i = FindKeyLine(key);
        if (i < 0)
            throw new InvalidOperationException($"No '{key}' line to rewrite in this .mdl.");
        var content = _doc.Lines[i].Content;
        if (!DslValue.TryFindQuoted(content, 0, out int open, out int close))
            throw new InvalidOperationException($"'{key}' line has no quoted value to rewrite.");
        _doc.ReplaceContent(i, content[..open] + DslValue.Quote(newValue) + content[(close + 1)..]);
    }
}
