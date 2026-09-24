using System.Numerics;
using ShadowForge.Formats.HDB;

namespace ShadowForge.Minimap;

public static class Mesh
{
    /// <summary>
    /// Appends every draw's bind-posed triangles, transformed by <paramref name="transform"/>,
    /// to <paramref name="into"/>. Triangles with coincident corners are dropped.
    /// </summary>
    public static void CollectTriangles(ModelFile model, Matrix4x4 transform, List<Tri> into)
    {
        var boneGlobals = BindPose.ComputeGlobals(model.Bones);

        foreach (var va in VertexArrayDraws.Collect(model))
        {
            foreach (var (group, ia) in va.Draws)
            {
                var positions = va.Vertices
                    .Select(v => Vector3.Transform(BindPose.Anchor(v, group.BonePalette, boneGlobals).worldPos, transform))
                    .ToArray();
                foreach (var (a, b, c) in DrawTriangles.Enumerate(ia.Indices, group.Topology, positions.Length))
                {
                    Vector3 pa = positions[a], pb = positions[b], pc = positions[c];
                    if (pa != pb && pb != pc && pa != pc)
                        into.Add(new Tri(pa, pb, pc));
                }
            }
        }
    }
}
