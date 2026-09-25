namespace ShadowForge.Formats.HDB;

/// <summary>
/// How <see cref="PreviewRenderer.RenderInto(PreviewMesh, PreviewCamera, PreviewShading, Span{SixLabors.ImageSharp.PixelFormats.Rgba32}, Span{float}, int, int)"/> colours a <see cref="PreviewMesh"/>.
/// </summary>
public enum PreviewShading
{
    /// <summary>
    /// One grey per face, lit by its face normal.
    /// </summary>
    Flat,

    /// <summary>
    /// Grey lit by the model's own vertex normals.
    /// </summary>
    Smooth,

    /// <summary>
    /// Triangle edges over a dim fill, with hidden edges removed.
    /// </summary>
    Wireframe,

    /// <summary>
    /// A distinct colour per texture slot, lit by the face normal.
    /// </summary>
    MaterialColors,

    /// <summary>
    /// The stage-0 texture on UV set 0, lit by the vertex normals.
    /// </summary>
    Textured,

    /// <summary>
    /// The stage-0 texture on UV set 0 as stored, without lighting.
    /// </summary>
    TexturedUnlit,
}

public static class PreviewShadingExtensions
{
    public static bool NeedsTextures(this PreviewShading shading) =>
        shading is PreviewShading.Textured or PreviewShading.TexturedUnlit;
}
