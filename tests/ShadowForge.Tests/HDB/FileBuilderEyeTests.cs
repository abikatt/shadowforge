using ShadowForge.Formats.HDB.Import;
using ShadowForge.Formats.HDB;

namespace ShadowForge.Tests.HDB;

/// <summary>
/// FileBuilder.BuildRenderCommands emits [0x0103, 0x61NN, 0x62NN, 0x05FF] for a staged
/// batch (Stage1Index or Stage2Index >= 0) and a stage-0 rebind (0x60NN) on the next
/// batch even when its material index is unchanged.
/// </summary>
public sealed class FileBuilderEyeTests
{
    private static ImportBatch MakeBatch(int material, int stage1 = -1, int stage2 = -1)
        => new()
        {
            MaterialIndex = material,
            Stage1Index = stage1,
            Stage2Index = stage2,
            Palette = new[] { 0 },
            StripIndices = new List<ushort> { 0, 1, 2 },
        };

    private static List<ushort> ParseWords(byte[] bytes)
    {
        Assert.Equal(0, bytes.Length % 2);
        var words = new List<ushort>(bytes.Length / 2);
        for (int i = 0; i < bytes.Length; i += 2)
            words.Add((ushort)((bytes[i] << 8) | bytes[i + 1]));
        return words;
    }

    [Fact]
    public void BuildRenderCommands_EyeBatchBetweenBodyBatches_EmitsBracketAndForcesRebind()
    {
        var batches = new List<ImportBatch>
        {
            MakeBatch(0),
            MakeBatch(0, stage1: 1, stage2: 2),
            MakeBatch(0),
        };

        byte[] bytes = FileBuilder.BuildRenderCommands(batches, skinned: true);
        var words = ParseWords(bytes);

        var expected = new ushort[]
        {
            0x5000,

            0x4000, 0x0000, 0x0400, 0x6000, 0x0201, 0x0000, 0x2000, 0x0001, 0x0000,

            0x4000, 0x0001, 0x0103, 0x6101, 0x6202, 0x05FF, 0x0201, 0x0000, 0x2000, 0x0001, 0x0003,

            0x4000, 0x0002, 0x6000, 0x0201, 0x0000, 0x2000, 0x0001, 0x0006,
            0x00FF,
        };
        Assert.Equal(expected, words);
    }

    [Fact]
    public void BuildRenderCommands_StagedBatchWithoutStage2_EmitsNo62Word()
    {
        var batches = new List<ImportBatch> { MakeBatch(0, stage1: 3) };

        byte[] bytes = FileBuilder.BuildRenderCommands(batches, skinned: true);
        var words = ParseWords(bytes);

        Assert.Contains(RenderCommandStream.StagedStateOpen, words);
        Assert.Contains(new MaterialWord(1, 3).Encode(), words);
        Assert.Contains(RenderCommandStream.StagedStateClose, words);
        Assert.DoesNotContain(words, w => (w & 0xFF00) == 0x6200);
    }
}
