using System.Numerics;
using ShadowForge.Formats.BDSL;
using ShadowForge.Scene;
using ShadowForge.Scene.Script;
using ShadowForge.Scene.Script.Lift;

namespace ShadowForge.Tests.BDSL;

public sealed class TextSceneWriterTests
{
    private static SceneFile MinimalScene(string sceneName = "TestScene", AreaType area = AreaType.Town) =>
        new() { Version = "0.26", SceneName = sceneName, AreaType = area };

    private static SceneEntry MakeEntry(uint id, EntryType type, string name = "TestEntry") =>
        new() { Id = id, Type = type, Name = name };

    private static ScriptBlock MakeUnconditionalBlock() => new() { ChapterMin = 0xFFFFFFFF, ChapterMax = 0 };

    private static ScriptBlock MakeConditionalBlock(uint chapterMin, uint chapterMax) =>
        new() { ChapterMin = chapterMin, ChapterMax = chapterMax };

    [Fact]
    public void Write_SceneHeader_ContainsSceneBlock()
    {
        var rpj = MinimalScene("TestTown", AreaType.Town);
        var output = TextScene.Write(rpj);

        Assert.Contains("scene \"TestTown\" {", output);
        Assert.Contains("version = \"0.26\"", output);
        Assert.Contains("area = town", output);
        Assert.Contains("}", output);
    }

    [Fact]
    public void Write_SceneHeader_DungeonArea()
    {
        var rpj = MinimalScene("DungeonMap", AreaType.Dungeon);
        var output = TextScene.Write(rpj);

        Assert.Contains("area = dungeon", output);
    }

    [Fact]
    public void Write_SceneHeader_OmitsFlagsWhenZero()
    {
        var rpj = MinimalScene();
        var output = TextScene.Write(rpj);

        Assert.DoesNotContain("flags =", output);
    }

    [Fact]
    public void Write_SceneHeader_EmitsFlagsWhenNonZero()
    {
        var rpj = MinimalScene();
        rpj.Flags = 1;
        var output = TextScene.Write(rpj);

        Assert.Contains("flags = 1", output);
    }

    [Fact]
    public void Write_SceneHeader_EmitsBuildPaths()
    {
        var rpj = MinimalScene();
        rpj.BuildPath = "D:\\BD_PLAN_VSS\\map\\";
        var output = TextScene.Write(rpj);

        Assert.Contains("build_path =", output);
    }

    [Fact]
    public void Write_EntryBlock_SpawnType()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(18, EntryType.Spawn, "SpawnEntry");
        entry.Position = new Vector3(-285.83398f, 295.6238f, -901.97534f);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("spawn \"SpawnEntry\" id=18", output);
        Assert.Contains("position =", output);
        Assert.Contains("-285.83398", output);
    }

    [Fact]
    public void Write_EntryBlock_CorrectKeywords()
    {
        EntryType[] types = { EntryType.Spawn, EntryType.Box, EntryType.Zone, EntryType.Enemy, EntryType.Link, EntryType.Entity, EntryType.Warp };
        string[] expected = { "spawn", "box", "zone", "enemy", "link", "entity", "warp" };

        for (int i = 0; i < types.Length; i++)
        {
            var rpj = MinimalScene();
            var entry = MakeEntry((uint)(i + 1), types[i], "E");
            rpj.Entries.Add(entry);
            var output = TextScene.Write(rpj);
            Assert.Contains(expected[i], output);
        }
    }

    [Fact]
    public void Write_EntryBlock_OmitsZeroPosition()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "NoPos");
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.DoesNotContain("position =", output);
    }

    [Fact]
    public void Write_EntryBlock_BoxType_WithExtents()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(5, EntryType.Box, "BoxEntry");
        entry.Extents = new Vector3(10.0f, 5.0f, 10.0f);
        entry.TriggerRadius = 45.0f;
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("box \"BoxEntry\" id=5", output);
        Assert.Contains("extents = (10", output);
        Assert.Contains("yaw = 45", output);
    }

    [Fact]
    public void Write_EntryBlock_EnemyType_WithRouteAndAggroRadius()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(7, EntryType.Enemy, "EnemyEntry");
        entry.Extents = new Vector3(100.0f, 0.0f, 200.0f);
        entry.TriggerRadius = 50.0f;
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("enemy \"EnemyEntry\" id=7", output);
        Assert.Contains("route = (100", output);
        Assert.Contains("aggro_radius = 50", output);
    }

    [Fact]
    public void Write_EntryBlock_WarpType_WithRotation()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(9, EntryType.Warp, "WarpEntry");
        entry.TriggerRadius = 90.0f;
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("warp \"WarpEntry\" id=9", output);
        Assert.Contains("rotation = 90", output);
    }

    [Fact]
    public void Write_ScriptBlock_WhenAll()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.Elements.Add(new ScriptInstruction { Opcode = 5023, Size = 16, RawParams = [0, 0] });
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("when {", output);
    }

    [Fact]
    public void Write_ScriptBlock_WhenSyntax_ChapterRange()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeConditionalBlock(0, 59);
        block.Elements.Add(new ScriptInstruction { Opcode = 5023, Size = 16, RawParams = [0, 0] });
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("when chapter 0..59 {", output);
    }

    [Fact]
    public void Write_ScriptBlock_WhenSyntax_WithCondition()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeConditionalBlock(0, 59);
        block.Conditions[0] = new Condition(2, 6, 0, 0);
        block.Elements.Add(new ScriptInstruction { Opcode = 5023, Size = 16, RawParams = [0, 0] });
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("when chapter 0..59 and var[6] == 0 {", output);
    }

    [Fact]
    public void Write_ScriptBlock_MetadataFields_BlockType()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.BlockType = 2;
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("@block_type npc", output);
    }

    [Fact]
    public void Write_ScriptBlock_MetadataFields_RenderFlags()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.RenderFlags = (1u << 0) | (1u << 28);
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("@render visible, collision", output);
    }

    [Fact]
    public void Write_ScriptBlock_MetadataFields_Behavior()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.BehaviorMode = BehaviorMode.Default;
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("@behavior default", output);
    }

    [Fact]
    public void Write_ScriptBlock_MetadataFields_OmittedWhenZero()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.DoesNotContain("@block_type", output);
        Assert.DoesNotContain("@render", output);
        Assert.DoesNotContain("@behavior", output);
    }

    [Fact]
    public void Write_ScriptBlock_MetadataFields_EncounterMode()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.EncounterMode = 1;
        block.EncounterRange = 15.5f;
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("@encounter normal", output);
        Assert.Contains("@encounter_range 15.5", output);
    }

    [Fact]
    public void Write_Instruction_KnownOpcode()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.Elements.Add(new ScriptInstruction
        {
            Opcode = 5001,
            Size = 32,
            RawParams = new uint[] { 0x154, 0, 0, 0, 0, 0 }
        });
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("show_message(msg=340)", output);
    }

    [Fact]
    public void Write_Instruction_Alias_UsesDistinctName()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.Elements.Add(new ScriptInstruction
        {
            Opcode = 5067,
            Size = 32,
            RawParams = new uint[] { 1, 0, 0, 0, 0, 0 }
        });
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("show_message_b(msg=1)", output);
        Assert.DoesNotContain("# op=", output);
    }

    [Fact]
    public void Write_Instruction_SetVariable_IsAssignment()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.Elements.Add(new ScriptInstruction
        {
            Opcode = 5003,
            Size = 32,
            RawParams = new uint[] { 0x6, 0x0, 0x0, 0x1, 0x0, 0x0 }
        });
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("var[6] = 1", output);
    }

    [Fact]
    public void Write_ScriptBlock_InitSection_And_Comment()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.Elements.Add(new ScriptInstruction { Opcode = 5057, Size = 16, RawParams = [0, 0] });
        block.Elements.Add(new ScriptInstruction { Opcode = 5061, Size = 32, RawParams = [0, 0, 1, 0, 0, 0] });
        block.Elements.Add(new ScriptInstruction { Opcode = 5058, Size = 16, RawParams = [0, 0] });
        block.Elements.Add(new ScriptInstruction { Opcode = 5050, Size = 64, RawParams = CommentText.Encode("hi") });
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj).Replace("\r\n", "\n");

        Assert.Contains("        init {\n            animation_trigger(p2=1)\n        }\n        // hi\n", output);
    }

    [Fact]
    public void Write_Instruction_RawData_EmitsRawKeyword()
    {
        var rpj = MinimalScene();
        var entry = MakeEntry(1, EntryType.Spawn, "E");
        var block = MakeUnconditionalBlock();
        block.Elements.Add(new ScriptData { Bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF } });
        entry.ScriptBlocks.Add(block);
        rpj.Entries.Add(entry);

        var output = TextScene.Write(rpj);

        Assert.Contains("raw DEADBEEF", output);
    }

    [Fact]
    public void Write_Waypoint_Basic()
    {
        var rpj = MinimalScene();
        var wp = new Waypoint
        {
            Id = 1,
            Type = WaypointType.Normal,
            Position = new Vector3(100.0f, 50.0f, -200.0f),
            Rotation = 90.0f,
        };
        rpj.Waypoints.Add(wp);

        var output = TextScene.Write(rpj);

        Assert.Contains("waypoint 1 at (100", output);
        Assert.Contains("-200", output);
        Assert.Contains("rotation=90", output);
    }

    [Fact]
    public void Write_Waypoint_WithTargets()
    {
        var rpj = MinimalScene();
        var wp = new Waypoint
        {
            Id = 3,
            LinkCount = 2,
            NextWaypoint = 3,
            UnkField38 = 0,
        };
        rpj.Waypoints.Add(wp);

        var output = TextScene.Write(rpj);

        Assert.Contains("targets = [2, 3, 0]", output);
    }

    [Fact]
    public void Write_Waypoint_OmitsRotationWhenZero()
    {
        var rpj = MinimalScene();
        var wp = new Waypoint { Id = 5 };
        rpj.Waypoints.Add(wp);

        var output = TextScene.Write(rpj);

        Assert.Contains("waypoint 5 at", output);
        Assert.DoesNotContain("rotation=", output);
    }

    [Fact]
    public void Write_Waypoint_OmitsTargetsWhenAllZero()
    {
        var rpj = MinimalScene();
        var wp = new Waypoint { Id = 2 };
        rpj.Waypoints.Add(wp);

        var output = TextScene.Write(rpj);

        Assert.DoesNotContain("targets =", output);
    }
}
