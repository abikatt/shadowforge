namespace ShadowForge.Minimap;

/// <summary>
/// Per-pixel floor coverage and height, with height negative infinity where
/// nothing was drawn.
/// </summary>
internal readonly record struct FloorLayer(bool[] Covered, float[] Height);
