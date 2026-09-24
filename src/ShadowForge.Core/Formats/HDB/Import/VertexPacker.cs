using System.Numerics;
using ShadowForge.IO;

namespace ShadowForge.Formats.HDB.Import;

/// <summary>
/// Encodes batch vertices in the layout the GPU declaration describes:
/// <see cref="SkinnedVertex"/> and <see cref="RigidVertex"/> offsets, FLOAT4/FLOAT3
/// positions, SHORT4N normals and tangents, D3DCOLOR, and non-normalized SHORT4
/// texcoords. The runtime hands the disk bytes to the GPU as the vertex stream. UV V is
/// written unflipped.
/// </summary>
public static class VertexPacker
{
    public static byte[] PackSkinned(ImportBatch batch, List<ImportBone> bones)
    {
        var data = new byte[batch.Vertices.Count * SkinnedVertex.Stride];
        var paletteSlot = new Dictionary<int, int>();
        for (int i = 0; i < batch.Palette.Length; i++) paletteSlot[batch.Palette[i]] = i;

        Span<(int Bone, float Weight)> inf = stackalloc (int, float)[3];
        for (int vi = 0; vi < batch.Vertices.Count; vi++)
        {
            var v = batch.Vertices[vi];
            int o = vi * SkinnedVertex.Stride;

            for (int i = 0; i < 3; i++)
                inf[i] = i < v.Influences.Length
                    ? (v.Influences[i].BoneIndex, v.Influences[i].Weight)
                    : (-1, 0f);
            if (v.Influences.Length == 0)
                inf[0] = (batch.Palette.Length > 0 ? batch.Palette[0] : 0, 1f);

            WriteInfluence(data, o + SkinnedVertex.Position, o + SkinnedVertex.Normal, v, inf[0], bones, paletteSlot);
            WriteInfluence(data, o + SkinnedVertex.Position2, o + SkinnedVertex.Normal2, v, inf[1], bones, paletteSlot);
            WriteInfluence(data, o + SkinnedVertex.Position3, o + SkinnedVertex.Normal3, v, inf[2], bones, paletteSlot);

            BigEndian.WriteUInt32(data, o + SkinnedVertex.Color, 0xFFFFFFFFu);

            WriteUV2(data, o + SkinnedVertex.UV, v.UV);
            WriteUV2(data, o + SkinnedVertex.UVEye, v.UVEye);
            WriteUV2(data, o + SkinnedVertex.UVEyelid, v.UVEyelid);
            WriteUV2(data, o + SkinnedVertex.UVDuplicate, v.UV);

            var tangentFrame = inf[0].Bone >= 0 ? bones[inf[0].Bone].InverseWorld : Matrix4x4.Identity;
            WriteShort4n(data, o + SkinnedVertex.Tangent,
                RuntimeMath.TransformNormal(tangentFrame, v.WorldTangent), 0x7FFF);
        }
        return data;
    }

    public static byte[] PackRigid(List<ImportVertex> vertices)
    {
        var data = new byte[vertices.Count * RigidVertex.Stride];
        for (int vi = 0; vi < vertices.Count; vi++)
        {
            var v = vertices[vi];
            int o = vi * RigidVertex.Stride;
            BigEndian.WriteFloat(data, o + RigidVertex.Position + 0, v.WorldPosition.X);
            BigEndian.WriteFloat(data, o + RigidVertex.Position + 4, v.WorldPosition.Y);
            BigEndian.WriteFloat(data, o + RigidVertex.Position + 8, v.WorldPosition.Z);

            WriteShort4n(data, o + RigidVertex.Normal, v.WorldNormal, 0x7FFF);
            BigEndian.WriteUInt32(data, o + RigidVertex.Color, 0xFFFFFFFFu);
            WriteUV(data, o + RigidVertex.UV, v.UV);
            WriteUV(data, o + RigidVertex.UV2, v.UV);
            WriteShort4n(data, o + RigidVertex.Tangent, v.WorldTangent, 0x7FFF);
        }
        return data;
    }

    private static void WriteInfluence(
        byte[] data, int posOff, int normOff, ImportVertex v,
        (int Bone, float Weight) inf, List<ImportBone> bones,
        Dictionary<int, int> paletteSlot)
    {
        if (inf.Bone < 0 || inf.Weight <= 0f) return;
        var bone = bones[inf.Bone];
        var local = RuntimeMath.TransformPoint(bone.InverseWorld, v.WorldPosition);
        var localNormal = RuntimeMath.TransformNormal(bone.InverseWorld, v.WorldNormal);
        BigEndian.WriteFloat(data, posOff + 0, local.X);
        BigEndian.WriteFloat(data, posOff + 4, local.Y);
        BigEndian.WriteFloat(data, posOff + 8, local.Z);
        BigEndian.WriteFloat(data, posOff + 12, inf.Weight);

        int slot = paletteSlot.TryGetValue(inf.Bone, out int s) ? s : 0;
        WriteShort4n(data, normOff, localNormal, SkinnedVertex.EncodePaletteIndex(slot));
    }

    private static void WriteUV(byte[] data, int offset, Vector2 uv)
    {
        WriteInt16(data, offset + 0, SkinnedVertex.QuantizeUV(uv.X));
        WriteInt16(data, offset + 2, SkinnedVertex.QuantizeUV(uv.Y));
        WriteInt16(data, offset + 4, 0);
        WriteInt16(data, offset + 6, 0);
    }

    private static void WriteUV2(byte[] data, int offset, Vector2 uv)
    {
        WriteInt16(data, offset + 0, SkinnedVertex.QuantizeUV(uv.X));
        WriteInt16(data, offset + 2, SkinnedVertex.QuantizeUV(uv.Y));
    }

    private static void WriteShort4n(byte[] data, int offset, Vector3 n, short w)
    {
        WriteInt16(data, offset + 0, ToSnorm(n.X));
        WriteInt16(data, offset + 2, ToSnorm(n.Y));
        WriteInt16(data, offset + 4, ToSnorm(n.Z));
        WriteInt16(data, offset + 6, w);
    }

    private static short ToSnorm(float f)
        => (short)Math.Clamp((int)MathF.Round(Math.Clamp(f, -1f, 1f) * 32767f), -32767, 32767);

    private static void WriteInt16(byte[] data, int offset, short value)
        => BigEndian.WriteUInt16(data, offset, (ushort)value);
}
