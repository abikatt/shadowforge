namespace ShadowForge.GameData.Mods;

/// <summary>
/// Rewrites a template motscr_{id}.csv for a new player id. A borrowed placeholder rig has no
/// BT_ clips, only fc_* and a few FD_ ones, so every SET_MOTION clip is remapped to an FD_ clip
/// the rig has. SET_SILUET_MOTION keeps its BT_ clip because it plays on the vanilla shadow
/// model, not on the borrowed rig.
/// </summary>
public sealed class MotScrGenerator
{
    private const string IdleClip = "FD_WT01";
    private const string ActionClip = "FD_TK01B";
    private const string MoveForwardClip = "FD_RN01";
    private const string MoveBackClip = "FD_WK01";

    private static readonly string[] ActionGroups =
        { "at", "sk", "ra", "mg", "ca", "cc", "gs", "it", "ap", "wi" };

    private readonly GameFileSystem _gfs;

    public MotScrGenerator(GameInstall install) => _gfs = new GameFileSystem(install);

    public byte[] Generate(string templateId, string newId)
    {
        byte[] bytes = _gfs.ReadVfs($@"database\battle\motscr\motscr_{templateId}.csv");
        string text = EncodingExtensions.DecodeShiftJISRaw(bytes, 0, bytes.Length);

        var lines = text.Split('\n').Select(line => Rewrite(line.TrimEnd('\r'), templateId, newId));
        return string.Join("\r\n", lines).EncodeShiftJIS();
    }

    private static string Rewrite(string line, string templateId, string newId)
    {
        string renamed = line.Replace(templateId + "_", newId + "_");
        var cols = renamed.Split(',');
        if (cols.Length < 3 || cols[1] != "SET_MOTION") return renamed;

        cols[2] = ClipFor(cols[0], newId);
        return string.Join(',', cols);
    }

    private static string ClipFor(string section, string newId)
    {
        string suffix = section.StartsWith(newId + "_", StringComparison.Ordinal)
            ? section[(newId.Length + 1)..]
            : section;
        string group = suffix.Length >= 2 ? suffix[..2] : suffix;

        if (ActionGroups.Contains(group)) return ActionClip;
        if (group == "mf") return MoveForwardClip;
        if (group == "mb") return MoveBackClip;
        return IdleClip;
    }
}
