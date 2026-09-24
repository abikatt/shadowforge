namespace ShadowForge.Minimap;

/// <summary>
/// Rasterizes a stage's floor triangles into a coverage mask sized and mapped
/// to the stage's XZ footprint, for the top-down minimap texture.
///
/// A stage carries two floor sources that fail in opposite directions: its
/// render models draw scenery nobody can stand on (distant cliffs, roofs),
/// while its collision mesh extends flat base planes well past the level.
/// Keeping only the floor both agree on, walkable ground that is also drawn
/// at the same height, is what the player actually walks over.
/// </summary>
public static class Mask
{
    private const int Supersample = 4;

    public static MaskResult Rasterize(IReadOnlyList<Tri> tris) => Rasterize(tris, Array.Empty<Tri>());

    public static MaskResult Rasterize(IReadOnlyList<Tri> render, IReadOnlyList<Tri> collision)
    {
        var collisionFloor = FloorSelection.Collision(collision);
        var renderFloor = FloorSelection.Render(render, applyHeightCut: collisionFloor.Count == 0);

        WorldRect rect = WorldRect.Around(collisionFloor.Count > 0 ? collisionFloor : renderFloor);
        (float[] mask, float[] elevation) = RunPipeline(renderFloor, collisionFloor, rect);

        if (rect.CropTo(mask) is { } refinedRect)
        {
            (mask, elevation) = RunPipeline(renderFloor, collisionFloor, refinedRect);
            rect = refinedRect;
        }

        return new MaskResult
        {
            Mask = mask,
            Elevation = elevation,
            Width = rect.Width,
            Height = rect.Height,
            WorldMinX = rect.MinX,
            WorldMinZ = rect.MinZ,
            WorldScaleX = rect.ScaleX,
            WorldScaleY = rect.ScaleZ,
        };
    }

    private static (float[] Mask, float[] Elevation) RunPipeline(
        List<Tri> renderFloor, List<Tri> collisionFloor, WorldRect rect)
    {
        int ssWidth = rect.Width * Supersample;
        int ssHeight = rect.Height * Supersample;

        FloorLayer walkable;
        if (collisionFloor.Count == 0)
        {
            walkable = FloorRasterizer.Rasterize(renderFloor, rect, ssWidth, ssHeight);
        }
        else
        {
            var solid = FloorRasterizer.Rasterize(collisionFloor, rect, ssWidth, ssHeight);
            var drawn = FloorRasterizer.Rasterize(renderFloor, rect, ssWidth, ssHeight, solid.Height);
            walkable = FloorSelection.Agreeing(solid, drawn);
        }

        float[] coverage = FloorRasterizer.DownsampleCoverage(walkable.Covered, ssWidth, rect.Width, rect.Height, Supersample);
        float[] elevation = FloorRasterizer.DownsampleHeight(walkable.Height, ssWidth, rect.Width, rect.Height, Supersample);

        float[] filtered = MaskCleanup.Filter(coverage, elevation, rect.Width, rect.Height);
        float[] smoothed = MaskCleanup.Smooth(filtered, rect.Width, rect.Height);

        for (int i = 0; i < smoothed.Length; i++)
        {
            if (smoothed[i] <= 0f)
                elevation[i] = float.NaN;
        }
        MaskCleanup.FillElevationGaps(smoothed, elevation, rect.Width, rect.Height);

        return (smoothed, elevation);
    }
}
