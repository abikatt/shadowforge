namespace ShadowForge.Formats.HDB.Import;

/// <summary>
/// One draw: its own vertex list, a strip index list with 0xFFFF restarts, and a
/// palette of at most <see cref="BatchBuilder.MaxPaletteEntries"/> bone positions.
/// </summary>
public sealed class ImportBatch
{
    public List<ImportVertex> Vertices = new();
    public List<ushort> StripIndices = new();
    public int[] Palette = [];
    public int MaterialIndex;

    /// <summary>
    /// <see cref="ImportTriangle.Stage1Index"/> shared by every triangle of the batch.
    /// </summary>
    public int Stage1Index = -1;

    /// <summary>
    /// <see cref="ImportTriangle.Stage2Index"/> shared by every triangle of the batch.
    /// </summary>
    public int Stage2Index = -1;
}
