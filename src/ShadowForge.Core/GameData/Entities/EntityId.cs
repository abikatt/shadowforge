using System.Text.RegularExpressions;

namespace ShadowForge.GameData.Entities;

/// <summary>
/// An entity id is a 2-letter class prefix, 2 or 3 digits, and an optional _variant
/// (pc01, em001, bs46_a). The prefix selects the asset class folder. Every entity has a
/// model def named model_{id}.mdl under database\model\chara\{class}\.
/// </summary>
public static partial class EntityId
{
    private const string ModelDefPrefix = "model_";
    private const string ModelDefExtension = ".mdl";

    [GeneratedRegex(@"^(pc|em|bs|np|sw|mt)\d{2,3}(_[0-9a-z]+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex IdPattern();

    /// <summary>
    /// True for an entity id, false for a path or any other word.
    /// </summary>
    public static bool IsEntityId(string s) => IdPattern().IsMatch(s);

    public static string? ClassFor(string id) => (id.Length >= 2 ? id[..2] : id).ToLowerInvariant() switch
    {
        "pc" => "ply",
        "em" => "ene",
        "bs" => "ene",
        "np" => "npc",
        "sw" => "sdw",
        "mt" => "mct",
        _ => null,
    };

    public static string ModelDefFileName(string id) => ModelDefPrefix + id + ModelDefExtension;

    public static string ModelDefVfsPath(string cls, string id) =>
        $@"database\model\chara\{cls}\{ModelDefFileName(id)}";

    /// <summary>
    /// The id in a model_{id}.mdl file name, or null for any other name.
    /// </summary>
    public static string? FromModelDefFileName(string fileName) =>
        fileName.StartsWith(ModelDefPrefix, StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(ModelDefExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName[ModelDefPrefix.Length..^ModelDefExtension.Length]
            : null;
}
