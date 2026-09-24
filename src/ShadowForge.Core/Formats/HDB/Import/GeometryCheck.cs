using System.Numerics;
using System.Text;
using ShadowForge.IO;
using ShadowForge.Formats.HDB.Raw;
using ShadowForge.Formats.HDB.Wire;

namespace ShadowForge.Formats.HDB.Import;

/// <summary>
/// Composes every bone's world matrix with <see cref="RuntimeMath"/>, then skins each
/// 100-byte vertex through each of its influences separately. In a well-formed file all
/// influences of a vertex give nearly the same world position. On retail files this
/// checks the euler convention and inverse-bind math. On importer output it checks that
/// the bytes reproduce the source geometry under the runtime transform order.
/// </summary>
public static class GeometryCheck
{
    public sealed class Result
    {
        public int SkinnedVertices;
        public int MultiInfluenceVertices;
        public double MaxDivergence;
        public double MeanDivergence;
        public Vector3 WorldMin = new(float.PositiveInfinity);
        public Vector3 WorldMax = new(float.NegativeInfinity);
        public List<string> Problems = new();
        public StringBuilder Log = new();
    }

    public static Result Run(byte[] d)
    {
        var res = new Result();

        int p = RawHeader.FirstTableStart(
            BigEndian.ReadUInt32(d, RawHeader.FirstTableOffsetField));
        int ftCount = (int)BigEndian.ReadUInt32(d, p);
        int q = p + 4 + BigEndian.ReadInt32(d, p + 4);
        int slotCount = (int)BigEndian.ReadUInt32(d, q);

        var rebased = new Dictionary<int, int>();
        for (int i = 0; i < slotCount; i++)
        {
            int slotAddr = q + 8 + i * 4;
            int cell = slotAddr + BigEndian.ReadInt32(d, slotAddr);
            if (cell < 0 || cell + 4 > d.Length) continue;
            int v = BigEndian.ReadInt32(d, cell);
            if (v != 0) rebased[cell] = cell + v;
        }
        int? Ptr(int cell)
            => cell >= 0 && cell + 4 <= d.Length && BigEndian.ReadInt32(d, cell) != 0
               && rebased.TryGetValue(cell, out int t) ? t : null;

        int type9 = -1;
        for (int i = 0; i < ftCount; i++)
        {
            int e = p + RawLayout.FirstTableHeaderSize + i * FirstTableEntry.Size;
            if (BigEndian.ReadUInt32(d, e) == 9) { type9 = rebased.TryGetValue(e + RawEntry.DataOffsetField, out int t) ? t : -1; break; }
        }
        if (type9 < 0) { res.Problems.Add("no type-9 entry"); return res; }

        int? root = Ptr(type9 + 16);
        if (root is not int rootOff) { res.Problems.Add("no bone root"); return res; }

        var worldBySlot = new List<Matrix4x4>();
        var slotByIndex = new Dictionary<ushort, int>();
        var geometry = new List<(int Node, int Type6, int Slot)>();

        void Walk(int node, Matrix4x4 parent)
        {
            while (true)
            {
                var flags = (BoneFlags)BigEndian.ReadUInt32(d, node + BoneData.FlagsOffset);
                var local = Matrix4x4.Identity;
                if (flags.HasFlag(BoneFlags.Position))
                    local = Matrix4x4.CreateTranslation(ReadVec3(d, node + BoneData.PositionOffset)) * local;
                if (flags.HasFlag(BoneFlags.ExtraEulerA))
                    local = RuntimeMath.EulerZyxToMatrix(ReadVec3(d, node + BoneData.ExtraEulerAOffset)) * local;
                if (flags.HasFlag(BoneFlags.Euler))
                    local = RuntimeMath.EulerZyxToMatrix(ReadVec3(d, node + BoneData.EulerOffset)) * local;
                if (flags.HasFlag(BoneFlags.ExtraEulerB))
                    local = RuntimeMath.EulerZyxToMatrix(ReadVec3(d, node + BoneData.ExtraEulerBOffset)) * local;
                if (flags.HasFlag(BoneFlags.Scale))
                    local = Matrix4x4.CreateScale(ReadVec3(d, node + BoneData.ScaleOffset)) * local;

                var world = local * parent;
                int slot = worldBySlot.Count;
                worldBySlot.Add(world);
                uint index = BigEndian.ReadUInt32(d, node + BoneData.IndexOffset);
                if (index <= 0xFFFF) slotByIndex.TryAdd((ushort)index, slot);
                if (flags.HasFlag(BoneFlags.Model) && Ptr(node + BoneData.ModelPtrOffset) is int t6)
                    geometry.Add((node, t6, slot));

                if (Ptr(node + BoneData.ChildPtrOffset) is int child) Walk(child, world);
                if (Ptr(node + BoneData.SiblingPtrOffset) is not int sib) return;
                node = sib;
            }
        }
        Walk(rootOff, Matrix4x4.Identity);

        int cursor = RawLayout.DataRegionStart(type9 + RawUnknownEntry9.RecordSize);
        var ibDataByGeo = new List<List<(int Count, int Off)>>();
        foreach (var (_, t6, _) in geometry)
        {
            var list = new List<(int Count, int Off)>();
            int ibCount = (int)BigEndian.ReadUInt32(d, t6 + ModelRecord.IndexBlockCountOffset);
            int? table = Ptr(t6 + ModelRecord.IndexTablePtrOffset);
            for (int i = 0; i < ibCount && table is int tb; i++)
            {
                cursor = Align.Up(cursor, 4);
                int size = (int)BigEndian.ReadUInt32(d, cursor);
                list.Add(((int)BigEndian.ReadUInt32(d, tb + i * 8), cursor + IndexBlockHeader.Size));
                cursor += IndexBlockHeader.Size + size;
            }
            ibDataByGeo.Add(list);
        }
        var vaByGeo = new List<List<(int VertCount, uint Fmt, int Off, int Stride)>>();
        foreach (var (_, t6, _) in geometry)
        {
            var list = new List<(int VertCount, uint Fmt, int Off, int Stride)>();
            int vaCount = (int)BigEndian.ReadUInt32(d, t6 + ModelRecord.VertexArrayCountOffset);
            int? setup = Ptr(t6 + ModelRecord.VASetupPtrOffset);
            for (int i = 0; i < vaCount && setup is int vs; i++)
            {
                int rec = vs + 4 + i * VASetupRecord.Size;
                int vertCount = (int)BigEndian.ReadUInt32(d, rec);
                uint fmt = BigEndian.ReadUInt32(d, rec + 4);
                var (stride, _) = VertexFormat.ComputeStrideAndDecl(fmt);
                cursor = Align.Up(cursor, 4);
                int size = (int)BigEndian.ReadUInt32(d, cursor);
                list.Add((vertCount, fmt, cursor + VAHeaderData.Size, stride));
                cursor += VAHeaderData.Size + size;
            }
            vaByGeo.Add(list);
        }

        double sum = 0;
        long samples = 0;
        for (int g = 0; g < geometry.Count; g++)
        {
            int? rc = Ptr(geometry[g].Type6 + ModelRecord.RenderCommandPtrOffset);
            if (rc is not int pos) continue;
            var palette = new List<int>();
            int boundVA = -1;
            var vas = vaByGeo[g];
            var checkedVas = new HashSet<int>();

            while (true)
            {
                ushort w = BigEndian.ReadUInt16(d, pos); pos += 2;
                if (w == 0) continue;
                if (w == 0x00FF) break;
                uint group = (uint)(w & 0xF000);
                if (group is 0x1000 or 0x2000 or 0x3000)
                {
                    pos += 4;
                    if ((w & 0xF00) == 0x400) pos += 2;
                    if (boundVA >= 0 && boundVA < vas.Count && checkedVas.Add(boundVA))
                        CheckVA(d, vas[boundVA], palette, worldBySlot, res, ref sum, ref samples);
                }
                else if (group == 0x4000)
                {
                    boundVA = BigEndian.ReadUInt16(d, pos); pos += 2;
                    if ((w & 0xF00) == 0x400) pos += 2;
                }
                else if (group == 0x9000)
                {
                    pos += 2;
                    if ((w & 0xF00) == 0x400) pos += 2;
                }
                else if (group == 0 && (w & 0xF00) == 0x200)
                {
                    palette = [];
                    int count = w & 0xFF;
                    for (int i = 0; i < count; i++)
                    {
                        ushort idx = BigEndian.ReadUInt16(d, pos); pos += 2;
                        palette.Add(slotByIndex.TryGetValue(idx, out int s) ? s : -1);
                    }
                }
            }
        }

        res.MeanDivergence = samples > 0 ? sum / samples : 0;
        res.Log.AppendLine($"skinned vertices: {res.SkinnedVertices}, multi-influence: {res.MultiInfluenceVertices}");
        res.Log.AppendLine($"divergence: mean {res.MeanDivergence:G4}, max {res.MaxDivergence:G4}");
        res.Log.AppendLine($"world bounds: min {res.WorldMin}, max {res.WorldMax}");
        return res;
    }

    private static void CheckVA(
        byte[] d,
        (int VertCount, uint Fmt, int Off, int Stride) va,
        List<int> palette,
        List<Matrix4x4> worldBySlot,
        Result res,
        ref double sum,
        ref long samples)
    {
        if (va.Stride != SkinnedVertex.Stride) return;
        for (int i = 0; i < va.VertCount; i++)
        {
            int o = va.Off + i * va.Stride;
            var positions = new List<Vector3>();
            var blended = Vector3.Zero;
            float weightSum = 0;
            foreach (var (posOff, normOff) in new[]
            {
                (SkinnedVertex.Position, SkinnedVertex.Normal),
                (SkinnedVertex.Position2, SkinnedVertex.Normal2),
                (SkinnedVertex.Position3, SkinnedVertex.Normal3),
            })
            {
                float weight = BigEndian.ReadFloat(d, o + posOff + 12);
                if (weight <= 0f) continue;
                var local = ReadVec3(d, o + posOff);
                int palLocal = d[o + normOff + SkinnedVertex.PaletteIndexInNormal] / 2;
                if (palLocal >= palette.Count || palette[palLocal] < 0)
                {
                    res.Problems.Add($"vertex {i}@0x{o:X}: palette slot {palLocal} unresolved (palette size {palette.Count})");
                    if (res.Problems.Count > 20) return;
                    continue;
                }
                var world = Vector3.Transform(local, worldBySlot[palette[palLocal]]);
                positions.Add(world);
                blended += world * weight;
                weightSum += weight;
            }
            if (positions.Count == 0) continue;
            res.SkinnedVertices++;
            if (weightSum > 0) blended /= weightSum;
            res.WorldMin = Vector3.Min(res.WorldMin, blended);
            res.WorldMax = Vector3.Max(res.WorldMax, blended);
            if (positions.Count > 1)
            {
                res.MultiInfluenceVertices++;
                for (int a = 1; a < positions.Count; a++)
                {
                    double dist = Vector3.Distance(positions[0], positions[a]);
                    sum += dist;
                    samples++;
                    if (dist > res.MaxDivergence) res.MaxDivergence = dist;
                }
            }
        }
    }

    private static Vector3 ReadVec3(byte[] d, int offset) => new(
        BigEndian.ReadFloat(d, offset),
        BigEndian.ReadFloat(d, offset + 4),
        BigEndian.ReadFloat(d, offset + 8));
}
