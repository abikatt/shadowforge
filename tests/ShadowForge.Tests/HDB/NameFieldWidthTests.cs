using System.Numerics;
using ShadowForge.Formats.HDB.Import;
using ShadowForge.Formats.HDB.Raw;

namespace ShadowForge.Tests.HDB;

/// <summary>
/// The two name fields differ in width. The texture record name runs 20 bytes up to
/// FloatParam at 0x14: in all 34780 retail texture records, a non-zero byte at 0x10 is
/// printable ASCII continuing a name that fills byte 0x0F, and only the 20-byte reading
/// matches .dds files on disk (np109, np114 and np132 eyelids). The bone name field is
/// 16 bytes, from NameOffset 0x40 to ExtraEulerAOffset 0x50.
/// </summary>
public sealed class NameFieldWidthTests
{
    private static ImportScene MakeMinimalScene(string textureName, string boneName)
    {
        var scene = new ImportScene
        {
            Skinned = false,
            Bones = new List<ImportBone>
            {
                new() { Index = 0, Name = boneName, ParentIndex = -1 },
            },
            Textures = new List<ImportTexture>
            {
                new() { Name = textureName },
            },
            Vertices = new List<ImportVertex>
            {
                new() { WorldPosition = Vector3.Zero, WorldNormal = Vector3.UnitY, UV = Vector2.Zero },
                new() { WorldPosition = Vector3.One, WorldNormal = Vector3.UnitY, UV = Vector2.One },
                new() { WorldPosition = new Vector3(1, 0, 1), WorldNormal = Vector3.UnitY, UV = new Vector2(1, 0) },
            },
            Triangles = new List<ImportTriangle>
            {
                new() { A = 0, B = 1, C = 2, MaterialIndex = 0 },
            },
        };
        return scene;
    }

    [Fact]
    public void Build_16CharTextureName_RoundTripsIntactNotTruncatedTo15()
    {
        const string name = "np114_eyelid_l_0";
        Assert.Equal(16, name.Length);

        var scene = MakeMinimalScene(name, "root");
        var batches = BatchBuilder.Build(scene);
        byte[] bytes = FileBuilder.Build(scene, batches);

        var raw = ShadowForge.Formats.HDB.ModelReader.Read(bytes);
        var texTable = raw.FirstTable.OfType<RawTextureTableEntry>().Single();
        Assert.Single(texTable.Records);
        Assert.Equal(name, texTable.Records[0].Name);
    }

    [Fact]
    public void Build_17CharTextureName_SurvivesPastByte16()
    {
        const string name = "np114_eyelid_l_01";
        Assert.Equal(17, name.Length);

        var scene = MakeMinimalScene(name, "root");
        var batches = BatchBuilder.Build(scene);
        byte[] bytes = FileBuilder.Build(scene, batches);

        var raw = ShadowForge.Formats.HDB.ModelReader.Read(bytes);
        var texTable = raw.FirstTable.OfType<RawTextureTableEntry>().Single();
        Assert.Equal(name, texTable.Records[0].Name);
    }

    [Fact]
    public void Build_20CharTextureName_RoundTripsIntact()
    {
        const string name = "np114_eyelid_l_01_xy";
        Assert.Equal(20, name.Length);

        var scene = MakeMinimalScene(name, "root");
        var batches = BatchBuilder.Build(scene);
        byte[] bytes = FileBuilder.Build(scene, batches);

        var raw = ShadowForge.Formats.HDB.ModelReader.Read(bytes);
        var texTable = raw.FirstTable.OfType<RawTextureTableEntry>().Single();
        Assert.Equal(name, texTable.Records[0].Name);
    }

    [Fact]
    public void Build_21CharTextureName_HardTruncatesAt20()
    {
        const string name = "np114_eyelid_l_01_xyz";
        Assert.Equal(21, name.Length);

        var scene = MakeMinimalScene(name, "root");
        var batches = BatchBuilder.Build(scene);
        byte[] bytes = FileBuilder.Build(scene, batches);

        var raw = ShadowForge.Formats.HDB.ModelReader.Read(bytes);
        var texTable = raw.FirstTable.OfType<RawTextureTableEntry>().Single();
        Assert.Equal("np114_eyelid_l_01_xy", texTable.Records[0].Name);
    }

    [Fact]
    public void Build_16CharBoneName_RoundTripsIntactNotTruncatedTo15()
    {
        const string name = "root_bind_pose01";
        Assert.Equal(16, name.Length);

        var scene = MakeMinimalScene("tex", name);
        var batches = BatchBuilder.Build(scene);
        byte[] bytes = FileBuilder.Build(scene, batches);

        var raw = ShadowForge.Formats.HDB.ModelReader.Read(bytes);
        var bone = raw.FirstTable.OfType<RawBoneEntry>().Single();
        Assert.Equal(name, bone.Name);
    }
}
