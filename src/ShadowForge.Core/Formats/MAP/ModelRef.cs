namespace ShadowForge.Formats.MAP;

/// <summary>
/// One MODEL NAME="..." block of a .map file.
/// </summary>
public sealed class ModelRef
{
    public required string Name { get; init; }

    public string? ObjectHDB { get; init; }

    public int Area { get; init; }

    public float Pri { get; init; }

    /// <summary>
    /// Every other key in the block, with its tab-joined values. Keys compare case-insensitively.
    /// </summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } =
        new Dictionary<string, string>();
}
