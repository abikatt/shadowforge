namespace ShadowForge.Minimap;

/// <summary>
/// A stage's two triangle sources: what it draws and what it collides with.
/// </summary>
public sealed class StageMesh
{
    /// <summary>
    /// World-space triangles of the stage's placed render models.
    /// </summary>
    public required IReadOnlyList<Tri> Render { get; init; }

    /// <summary>
    /// Stage-space triangles of the PARTS OCT collision mesh, empty when the
    /// .map declares none.
    /// </summary>
    public required IReadOnlyList<Tri> Collision { get; init; }
}
