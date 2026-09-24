using System.Numerics;
using ShadowForge.Formats.HDB.Import;
using ShadowForge.Formats.HDB;

namespace ShadowForge.Tests.HDB;

/// <summary>
/// ModelValidator reports a material word whose stage-1 or stage-2 texture index is past
/// the texture table as an error, because the runtime samples garbage. A stage above 2,
/// which no retail file uses, is also an error.
/// </summary>
public sealed class ModelValidatorEyeTests
{
    /// <summary>
    /// A rigid one-triangle scene with one batch whose stage-1 bind is
    /// <paramref name="stage1Index"/>, and <paramref name="textureCount"/> textures.
    /// BatchBuilder is skipped so the test controls the stage word FileBuilder emits.
    /// </summary>
    private static byte[] BuildStagedFile(int textureCount, int stage1Index)
    {
        var scene = new ImportScene
        {
            Skinned = false,
            Bones = new List<ImportBone>
            {
                new() { Index = 0, Name = "root", ParentIndex = -1 },
            },
            Textures = Enumerable.Range(0, textureCount)
                .Select(i => new ImportTexture { Name = $"tex{i}" })
                .ToList(),
        };

        var batch = new ImportBatch
        {
            Vertices = new List<ImportVertex>
            {
                new() { WorldPosition = Vector3.Zero, WorldNormal = Vector3.UnitY, UV = Vector2.Zero },
                new() { WorldPosition = Vector3.One, WorldNormal = Vector3.UnitY, UV = Vector2.One },
                new() { WorldPosition = new Vector3(1, 0, 1), WorldNormal = Vector3.UnitY, UV = new Vector2(1, 0) },
            },
            StripIndices = new List<ushort> { 0, 1, 2 },
            MaterialIndex = 0,
            Stage1Index = stage1Index,
        };

        return FileBuilder.Build(scene, new List<ImportBatch> { batch });
    }

    [Fact]
    public void ValidateRCStream_Stage1WordOutOfBounds_ReportsErrorNamingWordAndIndex()
    {
        byte[] bytes = BuildStagedFile(textureCount: 3, stage1Index: 4);
        Assert.Equal((ushort)0x6104, new MaterialWord(1, 4).Encode());

        var result = ModelValidator.Run(bytes);

        Assert.Contains(result.Findings, f =>
            f.Level == ModelValidator.Level.Error &&
            f.Message.Contains("0x6104") &&
            f.Message.Contains("texture 4 of 3"));
    }

    [Fact]
    public void ValidateRCStream_Stage1WordInBounds_NoErrorOrWarning()
    {
        byte[] bytes = BuildStagedFile(textureCount: 5, stage1Index: 4);

        var result = ModelValidator.Run(bytes);

        Assert.DoesNotContain(result.Findings, f =>
            f.Level != ModelValidator.Level.Info && f.Message.Contains("0x6104"));
    }

    [Fact]
    public void ValidateRCStream_Stage3PlusWord_ReportsErrorMentioningUnobservedStage()
    {
        byte[] bytes = BuildStagedFile(textureCount: 8, stage1Index: 5);
        byte[] needle = { 0x61, 0x05 };
        int at = IndexOf(bytes, needle);
        Assert.True(at >= 0, "expected to find the 0x6105 stage-1 word in the built file");

        var patched = (byte[])bytes.Clone();
        patched[at] = 0x63;

        var result = ModelValidator.Run(patched);

        Assert.Contains(result.Findings, f =>
            f.Level == ModelValidator.Level.Error &&
            f.Message.Contains("0x6305") &&
            f.Message.Contains("stage 3"));
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
