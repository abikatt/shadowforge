namespace ShadowForge.Minimap;

/// <summary>
/// Writes db_*.mmp minimap descriptors: tab-separated, CRLF, integer values.
/// </summary>
public static class MmpWriter
{
    private const float TargetWorldUnitsPerPixel = 3.5f;
    private const int DispSizeMin = 70;
    private const int DispSizeMax = 200;

    /// <summary>
    /// Format:
    ///   TEXSIZE  {Width}       {Height}
    ///   MAPSCALE {WorldScaleX} {WorldScaleY}
    ///   DISPSIZE {DispSize}    {DispSize}
    ///   OFFSET   {-WorldMinX}  {-WorldMinZ}
    ///
    /// The game's minimap widget samples +/-DISPSIZE texels around the player
    /// into a fixed 256px quad, so world units per screen pixel is
    /// DISPSIZE*MAPSCALE/(128*TEXSIZE). The shipped maps' median is about 3.5
    /// with DISPSIZE hand-tuned between 70 and 200, so DISPSIZE is solved for
    /// that target on the X axis and clamped to the shipped range. MAPSCALE
    /// and TEXSIZE share an aspect, so both axes get the same value.
    /// </summary>
    public static string Emit(MaskResult mask)
    {
        int offsetX = (int)MathF.Round(-mask.WorldMinX);
        int offsetZ = (int)MathF.Round(-mask.WorldMinZ);
        int mapscaleX = (int)MathF.Round(mask.WorldScaleX);
        int mapscaleY = (int)MathF.Round(mask.WorldScaleY);

        int dispSize = (int)MathF.Round(TargetWorldUnitsPerPixel * 128 * mask.Width / mapscaleX);
        dispSize = Math.Clamp(dispSize, DispSizeMin, DispSizeMax);

        return $"TEXSIZE\t{mask.Width}\t{mask.Height}\r\n" +
               $"MAPSCALE\t{mapscaleX}\t{mapscaleY}\r\n" +
               $"DISPSIZE\t{dispSize}\t{dispSize}\r\n" +
               $"OFFSET\t{offsetX}\t{offsetZ}\r\n";
    }
}
