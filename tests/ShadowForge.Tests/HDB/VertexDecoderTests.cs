using ShadowForge.Formats.HDB;
using ShadowForge.IO;

namespace ShadowForge.Tests.HDB;

public sealed class VertexDecoderTests
{
    [Fact]
    public void Decode_AllVAs_ReturnsExpectedVertexCount()
    {
        var model = SampleModel.Cooked();

        foreach (var va in model.VertexArrays)
        {
            var vertices = VertexDecoder.Decode(va.RawVertices, va.VAType, va.VertexCount);
            Assert.Equal(va.VertexCount, vertices.Count);
        }
    }

    [Fact]
    public void Decode_Positions_AreFinite()
    {
        var model = SampleModel.Cooked();
        var va = model.VertexArrays[0];
        var vertices = VertexDecoder.Decode(va.RawVertices, va.VAType, va.VertexCount);

        Assert.All(vertices, v =>
        {
            Assert.False(float.IsNaN(v.PosX) || float.IsInfinity(v.PosX));
            Assert.False(float.IsNaN(v.PosY) || float.IsInfinity(v.PosY));
            Assert.False(float.IsNaN(v.PosZ) || float.IsInfinity(v.PosZ));
        });
    }

    [Fact]
    public void Decode_UVs_AreInReasonableRange()
    {
        var model = SampleModel.Cooked();
        var va = model.VertexArrays[0];
        var vertices = VertexDecoder.Decode(va.RawVertices, va.VAType, va.VertexCount);

        Assert.All(vertices, v =>
        {
            Assert.InRange(v.U, -64f, 64f);
            Assert.InRange(v.V, -64f, 64f);
        });
    }

    [Fact]
    public void Decode_Skinned_HasInfluences()
    {
        var model = SampleModel.Cooked();
        var skinned = model.VertexArrays.FirstOrDefault(va => va.VAType == 0x13F00000);
        if (skinned == null) return;

        var vertices = VertexDecoder.Decode(skinned.RawVertices, skinned.VAType, skinned.VertexCount);
        Assert.All(vertices, v => Assert.NotEmpty(v.Influences));
    }

    [Fact]
    public void Decode_Skinned_ReadsEyeAndEyelidUvs()
    {
        var raw = new byte[100];

        void WriteUV(int off, short rawU, short rawV)
        {
            BigEndian.WriteUInt16(raw, off, unchecked((ushort)rawU));
            BigEndian.WriteUInt16(raw, off + 2, unchecked((ushort)rawV));
        }

        WriteUV(0x1C, EncodeUVComponent(0.25f), EncodeUVComponent(0.75f));
        WriteUV(0x20, EncodeUVComponent(0.125f), EncodeUVComponent(0.875f));
        WriteUV(0x24, EncodeUVComponent(0.375f), EncodeUVComponent(0.625f));

        var vertices = VertexDecoder.Decode(raw, 0x13F00000, 1);
        var v = Assert.Single(vertices);

        Assert.Equal(0.25f, v.U, 3);
        Assert.Equal(1f - 0.75f, v.V, 3);

        Assert.Equal(0.125f, v.UEye, 3);
        Assert.Equal(1f - 0.875f, v.VEye, 3);

        Assert.Equal(0.375f, v.UEyelid, 3);
        Assert.Equal(1f - 0.625f, v.VEyelid, 3);
    }

    private static short EncodeUVComponent(float decoded) => (short)((decoded + 32f) * 512f);
}
