using System.Numerics;
using ShadowForge.Formats.RPJ;
using ShadowForge.Formats.BDSL;
using ShadowForge.Scene;
using ShadowForge.Scene.Script;

namespace ShadowForge.Tests.BDSL;

public sealed class RoundTripTests
{
    [Fact]
    public void RoundTrip_Synthetic_EmptyScene()
    {
        var rpj = new SceneFile
        {
            Version = "0.26",
            Flags = 1,
            SceneName = "test",
            Filename = "test.rpj",
            AreaType = AreaType.Town,
        };

        var original = BinaryScene.Write(rpj);
        var rpj2 = BinaryScene.Read(original);
        var bdslText = TextScene.Write(rpj2);
        var rpj3 = TextScene.Read(bdslText);
        var compiled = BinaryScene.Write(rpj3);

        Assert.Equal(original.Length, compiled.Length);
    }

    [Fact]
    public void RoundTrip_Synthetic_WithEntry()
    {
        var rpj = new SceneFile
        {
            Version = "0.26",
            Flags = 1,
            SceneName = "test",
            AreaType = AreaType.Town,
        };

        var entry = new SceneEntry
        {
            Id = 1,
            Type = EntryType.Spawn,
            Position = new Vector3(100f, 50f, -200f),
            Name = "NPC",
        };
        rpj.Entries.Add(entry);

        var block = new ScriptBlock { ChapterMin = 0xFFFFFFFF };
        block.Elements.Add(new ScriptInstruction { Opcode = 5023, Size = 16, RawParams = [0, 0] });
        entry.ScriptBlocks.Add(block);

        var bdslText = TextScene.Write(rpj);
        var rpjBack = TextScene.Read(bdslText);

        Assert.Single(rpjBack.Entries);
        Assert.Equal(1u, rpjBack.Entries[0].Id);
        Assert.Equal(EntryType.Spawn, rpjBack.Entries[0].Type);
        Assert.Equal(100f, rpjBack.Entries[0].Position.X);
        Assert.Single(rpjBack.Entries[0].ScriptBlocks);
        Assert.True(rpjBack.Entries[0].ScriptBlocks[0].IsUnconditional);
        Assert.Equal(block.ChapterMin, rpjBack.Entries[0].ScriptBlocks[0].ChapterMin);
        Assert.Equal(block.ChapterMax, rpjBack.Entries[0].ScriptBlocks[0].ChapterMax);
    }

    [Fact]
    public void RoundTrip_Synthetic_WithStructuredBlock()
    {
        var rpj = new SceneFile { Version = "0.26", Flags = 1, SceneName = "test", AreaType = AreaType.Town };
        var entry = new SceneEntry { Id = 1, Type = EntryType.Spawn, Name = "E" };
        var block = new ScriptBlock { ChapterMin = 0xFFFFFFFF, ChapterMax = 0xFFFFFFFF };
        block.Conditions[0] = new Condition(2, 0xD22, 0, 0);
        uint[][] code =
        [
            [5057, 0, 0], [5061, 0, 0, 1, 0, 0, 0], [5058, 0, 0],
            [5042, 0, 34, 1, 3, 0, 0], [5004, 34, 1, 1, 1, 0, 0], [5003, 0xD22, 0, 0, 1, 0, 0], [5023, 0, 0],
            [5000, 3, 0], [5031, 20, 34, 1, 0, 0, 0],
        ];
        foreach (var c in code)
            block.Elements.Add(new ScriptInstruction { Opcode = c[0], Size = (uint)(8 + 4 * (c.Length - 1)), RawParams = c[1..] });
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var original = BinaryScene.Write(rpj);
        var text = TextScene.Write(BinaryScene.Read(original));
        var compiled = BinaryScene.Write(TextScene.Read(text));

        Assert.Equal(original, compiled);
    }
}
