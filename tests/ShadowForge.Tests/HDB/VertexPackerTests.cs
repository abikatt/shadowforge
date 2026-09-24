using System.Numerics;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Import;

namespace ShadowForge.Tests.HDB;

/// <summary>
/// The export path applies two V transforms: VertexDecoder gives V = 1 - (raw/512 - 32)
/// and SceneExporter writes glTF V = 1 - V, so glTF V = raw/512 - 32 with no net flip.
/// The importer must invert that whole path, not VertexDecoder alone.
/// </summary>
public sealed class VertexPackerTests
{
    private static float ExportPathV(float decodedV) => 1f - decodedV;

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.75f)]
    [InlineData(1.0f, 0.125f)]
    public void PackRigid_UV_RoundTripsThroughFullExportPath(float u, float v)
    {
        var vertices = new List<ImportVertex>
        {
            new() { WorldPosition = Vector3.One, WorldNormal = Vector3.UnitY, UV = new Vector2(u, v) },
        };

        byte[] raw = VertexPacker.PackRigid(vertices);
        var decoded = VertexDecoder.Decode(raw, VertexDecoder.VATypeRigid48, 1);

        Assert.Equal(u, decoded[0].U, 3f / 512f);
        Assert.Equal(v, ExportPathV(decoded[0].V), 3f / 512f);
    }

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.75f)]
    [InlineData(1.0f, 0.125f)]
    public void PackSkinned_UV_RoundTripsThroughFullExportPath(float u, float v)
    {
        var bone = new ImportBone { Index = 0, Name = "root" };
        var batch = new ImportBatch
        {
            Vertices = new List<ImportVertex>
            {
                new()
                {
                    WorldPosition = Vector3.One,
                    WorldNormal = Vector3.UnitY,
                    UV = new Vector2(u, v),
                    Influences = new[] { new ImportInfluence { BoneIndex = 0, Weight = 1f } },
                },
            },
            Palette = new[] { 0 },
        };

        byte[] raw = VertexPacker.PackSkinned(batch, new List<ImportBone> { bone });
        var decoded = VertexDecoder.Decode(raw, 0x13F00000, 1);

        Assert.Equal(u, decoded[0].U, 3f / 512f);
        Assert.Equal(v, ExportPathV(decoded[0].V), 3f / 512f);
    }

    [Fact]
    public void PackSkinned_EyeAndEyelidUV_RoundTripThroughFullExportPath_AndDupSlotMatchesUV0()
    {
        var bone = new ImportBone { Index = 0, Name = "root" };
        var uv0 = new Vector2(0.1f, 0.2f);
        var uvEye = new Vector2(0.4f, 0.6f);
        var uvEyelid = new Vector2(0.8f, 0.9f);
        var batch = new ImportBatch
        {
            Vertices = new List<ImportVertex>
            {
                new()
                {
                    WorldPosition = Vector3.One,
                    WorldNormal = Vector3.UnitY,
                    UV = uv0,
                    UVEye = uvEye,
                    UVEyelid = uvEyelid,
                    Influences = new[] { new ImportInfluence { BoneIndex = 0, Weight = 1f } },
                },
            },
            Palette = new[] { 0 },
        };

        byte[] raw = VertexPacker.PackSkinned(batch, new List<ImportBone> { bone });
        var decoded = VertexDecoder.Decode(raw, 0x13F00000, 1);

        Assert.Equal(uv0.X, decoded[0].U, 3f / 512f);
        Assert.Equal(uv0.Y, ExportPathV(decoded[0].V), 3f / 512f);
        Assert.Equal(uvEye.X, decoded[0].UEye, 3f / 512f);
        Assert.Equal(uvEye.Y, ExportPathV(decoded[0].VEye), 3f / 512f);
        Assert.Equal(uvEyelid.X, decoded[0].UEyelid, 3f / 512f);
        Assert.Equal(uvEyelid.Y, ExportPathV(decoded[0].VEyelid), 3f / 512f);

        short rawU0 = ShadowForge.IO.BigEndian.ReadInt16(raw, SkinnedVertex.UV);
        short rawV0 = ShadowForge.IO.BigEndian.ReadInt16(raw, SkinnedVertex.UV + 2);
        short rawU0Dup = ShadowForge.IO.BigEndian.ReadInt16(raw, SkinnedVertex.UVDuplicate);
        short rawV0Dup = ShadowForge.IO.BigEndian.ReadInt16(raw, SkinnedVertex.UVDuplicate + 2);
        Assert.Equal(rawU0, rawU0Dup);
        Assert.Equal(rawV0, rawV0Dup);
    }

    [Fact]
    public void PackRigid_ByteOutput_UnchangedFromCurrentQuantizationFormula()
    {
        var v = new ImportVertex
        {
            WorldPosition = new Vector3(1f, 2f, 3f),
            WorldNormal = Vector3.UnitY,
            WorldTangent = Vector3.UnitX,
            UV = new Vector2(0.25f, 0.75f),
        };

        byte[] raw = VertexPacker.PackRigid(new List<ImportVertex> { v });

        static short Quantize(float value)
            => (short)Math.Clamp((int)MathF.Round((value + 32f) * 512f), short.MinValue, short.MaxValue);
        static short Snorm(float f)
            => (short)Math.Clamp((int)MathF.Round(Math.Clamp(f, -1f, 1f) * 32767f), -32767, 32767);

        var expected = new byte[RigidVertex.Stride];
        void WriteI16(int offset, short value)
        {
            expected[offset] = (byte)(value >> 8);
            expected[offset + 1] = (byte)value;
        }
        void WriteF32(int offset, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            Array.Copy(bytes, 0, expected, offset, 4);
        }

        WriteF32(RigidVertex.Position + 0, v.WorldPosition.X);
        WriteF32(RigidVertex.Position + 4, v.WorldPosition.Y);
        WriteF32(RigidVertex.Position + 8, v.WorldPosition.Z);
        WriteI16(RigidVertex.Normal + 0, Snorm(v.WorldNormal.X));
        WriteI16(RigidVertex.Normal + 2, Snorm(v.WorldNormal.Y));
        WriteI16(RigidVertex.Normal + 4, Snorm(v.WorldNormal.Z));
        WriteI16(RigidVertex.Normal + 6, 0x7FFF);
        expected[RigidVertex.Color + 0] = 0xFF;
        expected[RigidVertex.Color + 1] = 0xFF;
        expected[RigidVertex.Color + 2] = 0xFF;
        expected[RigidVertex.Color + 3] = 0xFF;
        WriteI16(RigidVertex.UV + 0, Quantize(v.UV.X));
        WriteI16(RigidVertex.UV + 2, Quantize(v.UV.Y));
        WriteI16(RigidVertex.UV + 4, 0);
        WriteI16(RigidVertex.UV + 6, 0);
        WriteI16(RigidVertex.UV2 + 0, Quantize(v.UV.X));
        WriteI16(RigidVertex.UV2 + 2, Quantize(v.UV.Y));
        WriteI16(RigidVertex.UV2 + 4, 0);
        WriteI16(RigidVertex.UV2 + 6, 0);
        WriteI16(RigidVertex.Tangent + 0, Snorm(v.WorldTangent.X));
        WriteI16(RigidVertex.Tangent + 2, Snorm(v.WorldTangent.Y));
        WriteI16(RigidVertex.Tangent + 4, Snorm(v.WorldTangent.Z));
        WriteI16(RigidVertex.Tangent + 6, 0x7FFF);

        Assert.Equal(expected, raw);
    }
}
