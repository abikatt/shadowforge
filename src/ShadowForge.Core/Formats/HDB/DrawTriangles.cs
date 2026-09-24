namespace ShadowForge.Formats.HDB;

/// <summary>
/// Turns a draw's index list into triangles. The IA-select topologies (0x10, 0x20, 0x30)
/// are strips split at 0xFFFF restarts, and a 0x10 strip starts with reversed winding.
/// Any other topology reads as a triangle list. Triangles that repeat an index or name
/// a vertex at or past <c>vertexCount</c> are skipped.
/// </summary>
public static class DrawTriangles
{
    public const ushort StripRestart = 0xFFFF;

    public static IEnumerable<(int A, int B, int C)> Enumerate(ushort[] indices, int topology, int vertexCount)
    {
        if (!RenderCommandStream.IsIndexSelect(topology))
        {
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                if (indices[i] < vertexCount && indices[i + 1] < vertexCount && indices[i + 2] < vertexCount)
                    yield return (indices[i], indices[i + 1], indices[i + 2]);
            }
            yield break;
        }

        bool startReversed = topology == 0x10;
        int stripStart = 0;
        for (int end = 0; end <= indices.Length; end++)
        {
            if (end != indices.Length && indices[end] != StripRestart) continue;
            for (int i = 0; i < end - stripStart - 2; i++)
            {
                int a = indices[stripStart + i];
                int b = indices[stripStart + i + 1];
                int c = indices[stripStart + i + 2];
                if (a == b || b == c || a == c) continue;
                if (a >= vertexCount || b >= vertexCount || c >= vertexCount) continue;

                bool defaultWinding = (i % 2 == 0) != startReversed;
                yield return defaultWinding ? (a, b, c) : (b, a, c);
            }
            stripStart = end + 1;
        }
    }
}
