namespace ShadowForge.Minimap;

/// <summary>
/// A rasterized floor-coverage mask and the world rect it maps to. Pixel rows
/// run along world Z, so the Y-named scale is the rect's Z extent.
/// </summary>
public sealed class MaskResult
{
    /// <summary>
    /// Coverage in [0, 1], row-major, <see cref="Width"/> by <see cref="Height"/>.
    /// </summary>
    public required float[] Mask;
    public required int Width;
    public required int Height;
    public required float WorldMinX;
    public required float WorldMinZ;
    public required float WorldScaleX;
    public required float WorldScaleY;

    /// <summary>
    /// Topmost floor height per pixel in world units, NaN where uncovered.
    /// </summary>
    public required float[] Elevation;
}
