namespace ShadowForge.Formats.HDB;

/// <summary>
/// The decoded vertices of one vertex array and the draws that use it.
/// </summary>
public sealed record VertexArrayDraws(int VAIndex, List<Vertex> Vertices, List<(MeshGroup Group, IndexArray Indices)> Draws)
{
    /// <summary>
    /// Groups the model's draws by vertex array in first-use order. Draws naming a missing
    /// vertex or index array, and arrays that decode to no vertices, are skipped.
    /// </summary>
    public static IEnumerable<VertexArrayDraws> Collect(ModelFile model)
    {
        foreach (var vaGroup in model.MeshGroups.GroupBy(g => g.VAIndex))
        {
            int vaIdx = vaGroup.Key;
            if (vaIdx < 0 || vaIdx >= model.VertexArrays.Count) continue;

            var va = model.VertexArrays[vaIdx];
            var vertices = VertexDecoder.Decode(va.RawVertices, va.VAType, va.VertexCount);
            if (vertices.Count == 0) continue;

            var draws = vaGroup
                .Where(g => g.IAIndex >= 0 && g.IAIndex < model.IndexArrays.Count)
                .Select(g => (g, model.IndexArrays[g.IAIndex]))
                .ToList();
            yield return new VertexArrayDraws(vaIdx, vertices, draws);
        }
    }
}
