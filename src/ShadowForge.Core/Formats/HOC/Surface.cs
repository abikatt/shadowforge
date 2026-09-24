namespace ShadowForge.Formats.HOC;

/// <summary>
/// A collision surface descriptor shared by the triangles that select it.
/// </summary>
public sealed class Surface
{
    /// <summary>
    /// Material index the game uses for footstep and effect selection.
    /// </summary>
    public required uint Material { get; init; }

    public required uint Flags { get; init; }

    /// <summary>
    /// ARGB the authoring tool draws this surface with.
    /// </summary>
    public required uint DebugColor { get; init; }
}
