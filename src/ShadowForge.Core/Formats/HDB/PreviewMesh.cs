using System.Numerics;

namespace ShadowForge.Formats.HDB;

/// <summary>
/// A model's bind-pose triangles and bounding sphere, built once by
/// <see cref="PreviewRenderer.Prepare"/> and rendered from any camera.
/// </summary>
public sealed class PreviewMesh
{
    internal IReadOnlyList<PreviewRenderer.Triangle> Triangles { get; }
    public Vector3 Center { get; }
    public float Radius { get; }
    public int TriangleCount => Triangles.Count;

    internal PreviewMesh(IReadOnlyList<PreviewRenderer.Triangle> triangles)
    {
        Triangles = triangles;
        if (triangles.Count == 0)
        {
            Radius = 1f;
            return;
        }

        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var t in triangles)
        {
            min = Vector3.Min(min, Vector3.Min(t.A, Vector3.Min(t.B, t.C)));
            max = Vector3.Max(max, Vector3.Max(t.A, Vector3.Max(t.B, t.C)));
        }
        Center = (min + max) * 0.5f;

        float r2 = 0f;
        foreach (var t in triangles)
        {
            r2 = MathF.Max(r2, Vector3.DistanceSquared(t.A, Center));
            r2 = MathF.Max(r2, Vector3.DistanceSquared(t.B, Center));
            r2 = MathF.Max(r2, Vector3.DistanceSquared(t.C, Center));
        }
        Radius = MathF.Max(MathF.Sqrt(r2), 1e-6f);
    }
}

/// <summary>
/// An orbit camera for <see cref="PreviewRenderer.RenderInto"/>. Zoom 1 fits the bounding
/// sphere to the view. PanX and PanY shift the image by a fraction of the shorter side.
/// Start from <see cref="Default"/>: default(PreviewCamera) has Zoom 0 and draws nothing.
/// </summary>
public readonly record struct PreviewCamera(float YawDeg, float PitchDeg, float Zoom, float PanX, float PanY)
{
    /// <summary>
    /// The three-quarter view <see cref="PreviewRenderer.Render"/> uses, fitted to the view.
    /// </summary>
    public static PreviewCamera Default { get; } = new(30f, 22f, 1f, 0f, 0f);
}
