using System.Numerics;
using ShadowForge.Formats.DDS;
using ShadowForge.Formats.HDB;
using ShadowForge.IO;

namespace ShadowForge.Tests.GLTF;

internal static class ExportFixtures
{
    /// <summary>
    /// Writes one 100-byte skinned vertex: influence 0 at full weight on palette slot 0
    /// with normal +Z, and the given UVs. UV slots left null stay zero.
    /// </summary>
    public static void EncodeSkinnedVertex(byte[] buf, int off, Vector3 pos, Vector2 uv0,
        Vector2? uvEye = null, Vector2? uvEyelid = null)
    {
        BigEndian.WriteFloat(buf, off + SkinnedVertex.Position, pos.X);
        BigEndian.WriteFloat(buf, off + SkinnedVertex.Position + 4, pos.Y);
        BigEndian.WriteFloat(buf, off + SkinnedVertex.Position + 8, pos.Z);
        BigEndian.WriteFloat(buf, off + SkinnedVertex.Position + 12, 1f);
        BigEndian.WriteUInt16(buf, off + SkinnedVertex.Normal + 4, 32767);
        WriteUV(buf, off + SkinnedVertex.UV, uv0);
        if (uvEye is { } eye) WriteUV(buf, off + SkinnedVertex.UVEye, eye);
        if (uvEyelid is { } lid) WriteUV(buf, off + SkinnedVertex.UVEyelid, lid);
    }

    /// <summary>
    /// Writes a 4x4 single-color DXT1 texture named <paramref name="name"/>.dds.
    /// </summary>
    public static void WriteSolidDDS(string dir, string name, byte r, byte g, byte b)
    {
        var rgba = new byte[4 * 4 * 4];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = 255;
        }
        byte[] dds = Importer.ImportFromRgba(rgba, 4, 4, GraphicFormat.TextureFormatDXT1);
        File.WriteAllBytes(Path.Combine(dir, name + ".dds"), dds);
    }

    private static void WriteUV(byte[] buf, int offset, Vector2 uv)
    {
        BigEndian.WriteUInt16(buf, offset, (ushort)SkinnedVertex.QuantizeUV(uv.X));
        BigEndian.WriteUInt16(buf, offset + 2, (ushort)SkinnedVertex.QuantizeUV(uv.Y));
    }
}
