namespace ShadowForge.Formats.HDB.Import;

/// <summary>
/// Groups triangles by material and stage bindings, then fills batches greedily while
/// the union of their bones fits the matrix palette. Each batch gets a local vertex list
/// and a triangle strip with 0xFFFF restarts, as retail index buffers use.
/// </summary>
public static class BatchBuilder
{
    /// <summary>
    /// Matrix palette hard limit. A palette carrying more entries than this
    /// hangs the renderer, so batches must be split to stay within it.
    /// </summary>
    public const int MaxPaletteEntries = 24;

    public static List<ImportBatch> Build(ImportScene scene)
    {
        var byMaterialAndStage = scene.Triangles
            .GroupBy(t => (t.MaterialIndex, t.Stage1Index, t.Stage2Index))
            .OrderBy(g => g.Key);

        var batches = new List<ImportBatch>();
        foreach (var group in byMaterialAndStage)
            batches.AddRange(BuildForMaterial(scene, group.Key.MaterialIndex, group.Key.Stage1Index, group.Key.Stage2Index, group.ToList()));
        return batches;
    }

    private static IEnumerable<ImportBatch> BuildForMaterial(
        ImportScene scene, int material, int stage1, int stage2, List<ImportTriangle> tris)
    {
        if (!scene.Skinned)
        {
            yield return MakeBatch(scene, material, stage1, stage2, tris, Array.Empty<int>());
            yield break;
        }

        var pending = new List<ImportTriangle>();
        var bones = new HashSet<int>();
        foreach (var tri in tris)
        {
            var triBones = new HashSet<int>();
            foreach (int vi in new[] { tri.A, tri.B, tri.C })
                foreach (var inf in scene.Vertices[vi].Influences)
                    if (inf.Weight > 0f) triBones.Add(inf.BoneIndex);

            if (triBones.Count > MaxPaletteEntries)
                throw new InvalidDataException(
                    $"A single triangle references {triBones.Count} bones; cannot satisfy the 24-bone palette limit.");

            var union = new HashSet<int>(bones);
            union.UnionWith(triBones);
            if (union.Count > MaxPaletteEntries && pending.Count > 0)
            {
                yield return MakeBatch(scene, material, stage1, stage2, pending, bones.OrderBy(x => x).ToArray());
                pending = [];
                bones = triBones;
            }
            else
            {
                bones = union;
            }
            pending.Add(tri);
        }
        if (pending.Count > 0)
            yield return MakeBatch(scene, material, stage1, stage2, pending, bones.OrderBy(x => x).ToArray());
    }

    private static ImportBatch MakeBatch(
        ImportScene scene, int material, int stage1, int stage2, List<ImportTriangle> tris, int[] palette)
    {
        var batch = new ImportBatch { MaterialIndex = material, Stage1Index = stage1, Stage2Index = stage2, Palette = palette };

        var globalToLocal = new Dictionary<int, ushort>();
        var localTris = new List<(ushort A, ushort B, ushort C)>(tris.Count);
        foreach (var t in tris)
            localTris.Add((Local(t.A), Local(t.B), Local(t.C)));

        batch.StripIndices = Stripify(localTris);
        if (batch.Vertices.Count > 0xFFFE)
            throw new InvalidDataException(
                $"Batch has {batch.Vertices.Count} vertices; 16-bit indices with the 0xFFFF restart sentinel allow at most 65534.");
        return batch;

        ushort Local(int global)
        {
            if (globalToLocal.TryGetValue(global, out ushort l)) return l;
            ushort idx = (ushort)batch.Vertices.Count;
            batch.Vertices.Add(scene.Vertices[global]);
            globalToLocal[global] = idx;
            return idx;
        }
    }

    /// <summary>
    /// Greedy edge-walk stripifier. A strip grows while an unused triangle shares the walk
    /// edge in the direction its parity needs, then a 0xFFFF restart seeds the next strip.
    /// Every strip starts with a triangle in its original winding at even parity, so the
    /// GPU's alternating flip keeps every face oriented as authored.
    /// </summary>
    private static List<ushort> Stripify(List<(ushort A, ushort B, ushort C)> tris)
    {
        var edgeToTris = new Dictionary<(ushort, ushort), List<int>>(tris.Count * 3);
        for (int i = 0; i < tris.Count; i++)
        {
            var (a, b, c) = tris[i];
            Add((a, b), i); Add((b, c), i); Add((c, a), i);
        }

        var used = new bool[tris.Count];
        var strip = new List<ushort>(tris.Count * 2);

        for (int seed = 0; seed < tris.Count; seed++)
        {
            if (used[seed]) continue;
            used[seed] = true;
            if (strip.Count > 0) strip.Add(DrawTriangles.StripRestart);
            var (sa, sb, sc) = tris[seed];
            strip.Add(sa); strip.Add(sb); strip.Add(sc);

            ushort u = sb, v = sc;
            int parity = 1;
            while (true)
            {
                var key = parity == 1 ? (v, u) : (u, v);
                if (!edgeToTris.TryGetValue(key, out var candidates)) break;
                int next = -1;
                ushort tail = 0;
                foreach (int ti in candidates)
                {
                    if (used[ti]) continue;
                    var (ta, tb, tc) = tris[ti];
                    if (ta == key.Item1 && tb == key.Item2) { tail = tc; next = ti; break; }
                    if (tb == key.Item1 && tc == key.Item2) { tail = ta; next = ti; break; }
                    if (tc == key.Item1 && ta == key.Item2) { tail = tb; next = ti; break; }
                }
                if (next < 0) break;
                used[next] = true;
                strip.Add(tail);
                u = v; v = tail;
                parity ^= 1;
            }
        }
        return strip;

        void Add((ushort, ushort) key, int tri)
        {
            if (!edgeToTris.TryGetValue(key, out var list)) edgeToTris[key] = list = [];
            list.Add(tri);
        }
    }
}
