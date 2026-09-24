using ShadowForge.IO;

namespace ShadowForge.Formats.HDB;

public static class VertexDecoder
{
    /// <summary>
    /// VA block format word of a rigid 48-byte array: the declaration word that
    /// <see cref="VertexFormat.Rigid"/> resolves to. Any other value decodes as the
    /// 100-byte skinned layout.
    /// </summary>
    public const uint VATypeRigid48 = 0x102F5000;

    /// <summary>
    /// Decodes big-endian vertex records. V is returned flipped (1 - v), and the glTF
    /// exporter flips it back.
    /// </summary>
    public static List<Vertex> Decode(byte[] rawData, uint vaType, int vertexCount)
    {
        if (vertexCount <= 0) return new List<Vertex>();

        bool rigid = vaType == VATypeRigid48;
        int stride = rigid ? RigidVertex.Stride : SkinnedVertex.Stride;
        var vertices = new List<Vertex>(vertexCount);

        for (int i = 0; i < vertexCount; i++)
        {
            int off = i * stride;
            if (off + stride > rawData.Length) break;
            vertices.Add(rigid ? DecodeRigid(rawData, off) : DecodeSkinned(rawData, off));
        }

        return vertices;
    }

    private static Vertex DecodeSkinned(byte[] data, int off)
    {
        var v = new Vertex();

        var inf1 = ReadInfluence(data, off + SkinnedVertex.Position, off + SkinnedVertex.Normal);
        v.PosX = inf1.PosX; v.PosY = inf1.PosY; v.PosZ = inf1.PosZ;
        v.NormalX = inf1.NormalX; v.NormalY = inf1.NormalY; v.NormalZ = inf1.NormalZ;
        v.Influences.Add(inf1);

        (v.U, v.V) = ReadUV(data, off + SkinnedVertex.UV);
        (v.UEye, v.VEye) = ReadUV(data, off + SkinnedVertex.UVEye);
        (v.UEyelid, v.VEyelid) = ReadUV(data, off + SkinnedVertex.UVEyelid);

        var inf2 = ReadInfluence(data, off + SkinnedVertex.Position2, off + SkinnedVertex.Normal2);
        if (inf2.Weight != 0f) v.Influences.Add(inf2);

        var inf3 = ReadInfluence(data, off + SkinnedVertex.Position3, off + SkinnedVertex.Normal3);
        if (inf3.Weight != 0f) v.Influences.Add(inf3);

        return v;
    }

    /// <summary>
    /// Position and weight form a float4. The palette index is the high byte of the
    /// normal's fourth short, halved.
    /// </summary>
    private static BoneInfluence ReadInfluence(byte[] data, int posOff, int normOff) => new()
    {
        PosX = BigEndian.ReadFloat(data, posOff),
        PosY = BigEndian.ReadFloat(data, posOff + 4),
        PosZ = BigEndian.ReadFloat(data, posOff + 8),
        Weight = BigEndian.ReadFloat(data, posOff + 12),
        NormalX = BigEndian.ReadInt16(data, normOff),
        NormalY = BigEndian.ReadInt16(data, normOff + 2),
        NormalZ = BigEndian.ReadInt16(data, normOff + 4),
        PaletteIndex = data[normOff + SkinnedVertex.PaletteIndexInNormal] / 2,
    };

    /// <summary>
    /// A fourth normal short of 0x7FFF marks an unskinned vertex, bound to palette slot 0.
    /// </summary>
    private static Vertex DecodeRigid(byte[] data, int off)
    {
        int normOff = off + RigidVertex.Normal;
        var v = new Vertex
        {
            PosX = BigEndian.ReadFloat(data, off + RigidVertex.Position),
            PosY = BigEndian.ReadFloat(data, off + RigidVertex.Position + 4),
            PosZ = BigEndian.ReadFloat(data, off + RigidVertex.Position + 8),
            NormalX = BigEndian.ReadInt16(data, normOff),
            NormalY = BigEndian.ReadInt16(data, normOff + 2),
            NormalZ = BigEndian.ReadInt16(data, normOff + 4),
        };

        int palOff = normOff + SkinnedVertex.PaletteIndexInNormal;
        int boneIdx = BigEndian.ReadInt16(data, palOff) == 0x7FFF ? 0 : data[palOff] / 2;
        v.Influences.Add(new BoneInfluence { PaletteIndex = boneIdx, Weight = 1f, PosX = v.PosX, PosY = v.PosY, PosZ = v.PosZ });

        (v.U, v.V) = ReadUV(data, off + RigidVertex.UV);
        return v;
    }

    private static (float U, float V) ReadUV(byte[] data, int offset)
        => (SkinnedVertex.DequantizeUV(BigEndian.ReadInt16(data, offset)),
            1f - SkinnedVertex.DequantizeUV(BigEndian.ReadInt16(data, offset + 2)));
}
