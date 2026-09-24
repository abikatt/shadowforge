using ShadowForge.Formats.BDSL;
using ShadowForge.Scene;
using ShadowForge.Scene.Script;

namespace ShadowForge.Tests.BDSL;

public sealed class ParserTests
{
    [Fact]
    public void Parse_SceneHeader()
    {
        var bdsl = """
            scene "TestScene" {
                version = "0.26"
                flags = 1
                area = town
                messages = "mes_bg01_01.u16"
            }
            """;

        var rpj = TextScene.Read(bdsl);

        Assert.Equal("0.26", rpj.Version);
        Assert.Equal(1u, rpj.Flags);
        Assert.Equal(AreaType.Town, rpj.AreaType);
        Assert.Equal("mes_bg01_01.u16", rpj.MessagePath);
    }

    [Fact]
    public void Parse_SpawnEntry()
    {
        var bdsl = """
            spawn "NPC" id=1 {
                position = (100.0, 50.0, -200.0)
                facing = 90.0
                ref_id = 3
            }
            """;

        var rpj = TextScene.Read(bdsl);

        Assert.Single(rpj.Entries);
        var entry = rpj.Entries[0];
        Assert.Equal(1u, entry.Id);
        Assert.Equal(EntryType.Spawn, entry.Type);
        Assert.Equal(100.0f, entry.Position.X);
        Assert.Equal(50.0f, entry.Position.Y);
        Assert.Equal(-200.0f, entry.Position.Z);
        Assert.Equal(90.0f, entry.Facing);
        Assert.Equal(3u, entry.RefId);
    }

    [Fact]
    public void Parse_BoxEntry_WithExtents()
    {
        var bdsl = """
            box "Zone" id=3 {
                position = (0, 0, 0)
                extents = (10.5, 5.0, 8.0)
                yaw = 45.0
            }
            """;

        var rpj = TextScene.Read(bdsl);

        Assert.Single(rpj.Entries);
        var entry = rpj.Entries[0];
        Assert.Equal(EntryType.Box, entry.Type);
        Assert.Equal(10.5f, entry.Extents.X);
        Assert.Equal(5.0f, entry.Extents.Y);
        Assert.Equal(8.0f, entry.Extents.Z);
        Assert.Equal(45.0f, entry.TriggerRadius);
    }

    [Fact]
    public void Parse_EnemyEntry_WithAggro()
    {
        var bdsl = """
            enemy "Mob" id=10 {
                position = (0, 0, 0)
                route = (1.0, 2.0, 3.0)
                aggro_radius = 30.0
            }
            """;

        var rpj = TextScene.Read(bdsl);

        Assert.Single(rpj.Entries);
        var entry = rpj.Entries[0];
        Assert.Equal(EntryType.Enemy, entry.Type);
        Assert.Equal(1.0f, entry.Extents.X);
        Assert.Equal(2.0f, entry.Extents.Y);
        Assert.Equal(3.0f, entry.Extents.Z);
        Assert.Equal(30.0f, entry.TriggerRadius);
    }

    [Fact]
    public void Parse_WarpEntry_WithRotation()
    {
        var bdsl = """
            warp "Exit" id=7 {
                position = (0, 0, 0)
                rotation = 90.0
            }
            """;

        var rpj = TextScene.Read(bdsl);

        Assert.Single(rpj.Entries);
        var entry = rpj.Entries[0];
        Assert.Equal(EntryType.Warp, entry.Type);
        Assert.Equal(7u, entry.Id);
        Assert.Equal(90.0f, entry.TriggerRadius);
    }

    [Fact]
    public void Parse_WhenBlock_WithConditions()
    {
        var bdsl = """
            spawn "NPC" id=1 {
                position = (0, 0, 0)

                when chapter 0..59 and var[6] == 0 {
                }
            }
            """;

        var rpj = TextScene.Read(bdsl);

        var block = rpj.Entries[0].ScriptBlocks[0];
        Assert.Equal(0u, block.ChapterMin);
        Assert.Equal(59u, block.ChapterMax);
        Assert.False(block.IsUnconditional);

        var cond = block.Conditions[0];
        Assert.Equal(2u, cond.Type);
        Assert.Equal(6u, cond.Operand);
        Assert.Equal(0u, cond.Op);
        Assert.Equal(0u, cond.Value);
    }

    [Fact]
    public void Parse_WhenAll()
    {
        var bdsl = """
            spawn "NPC" id=1 {
                when all {
                }
            }
            """;

        var rpj = TextScene.Read(bdsl);

        var block = rpj.Entries[0].ScriptBlocks[0];
        Assert.True(block.IsUnconditional);
        Assert.Equal(0xFFFFFFFFu, block.ChapterMin);
    }

    [Fact]
    public void Parse_MetadataDirectives()
    {
        var bdsl = """
            spawn "NPC" id=1 {
                when all {
                    @block_type npc
                    @behavior default
                    @encounter normal
                    @interaction_radius 30.0
                    @render visible, collision
                }
            }
            """;

        var rpj = TextScene.Read(bdsl);

        var block = rpj.Entries[0].ScriptBlocks[0];
        Assert.Equal(2u, block.BlockType);
        Assert.Equal(BehaviorMode.Default, block.BehaviorMode);
        Assert.Equal(1u, block.EncounterMode);
        Assert.Equal(30.0f, block.InteractionRadius);
        Assert.Equal(0x10000001u, block.RenderFlags);
    }

    [Fact]
    public void Parse_Waypoint()
    {
        var bdsl = """
            waypoint 1 at (100.0, 50.0, -200.0) rotation=90.0 {
                targets = [2, 3, 0]
            }
            """;

        var rpj = TextScene.Read(bdsl);

        Assert.Single(rpj.Waypoints);
        var wp = rpj.Waypoints[0];
        Assert.Equal(1u, wp.Id);
        Assert.Equal(WaypointType.Normal, wp.Type);
        Assert.Equal(100.0f, wp.Position.X);
        Assert.Equal(50.0f, wp.Position.Y);
        Assert.Equal(-200.0f, wp.Position.Z);
        Assert.Equal(90.0f, wp.Rotation);
        Assert.Equal(2u, wp.LinkCount);
        Assert.Equal(3u, wp.NextWaypoint);
        Assert.Equal(0u, wp.UnkField38);
    }

    [Fact]
    public void Parse_AliasName()
    {
        var bdsl = """
            spawn "NPC" id=1 {
                when all {
                    set_variable_chest(dest=var[6], value=1)
                }
            }
            """;

        var rpj = TextScene.Read(bdsl);

        var block = rpj.Entries[0].ScriptBlocks[0];
        Assert.Single(block.Elements);

        var instr = Assert.IsType<ScriptInstruction>(block.Elements[0]);
        Assert.Equal(5097u, instr.Opcode);
        Assert.Equal(6, instr.RawParams.Length);
        Assert.Equal(6u, instr.RawParams[0]);
        Assert.Equal(1u, instr.RawParams[3]);
    }

    [Fact]
    public void Parse_StructuredIf_And_Init()
    {
        var bdsl = """
            scene "T" {
                version = "0.26"
                area = town
            }

            spawn "E" id=1 {
                when var[3362] == 0 {
                    @block_type npc
                    init {
                        animation_trigger()
                    }
                    if !overflow(item 34, +1) {
                        give_item(item=34, add=1, qty=1, popup=1)
                        var[3362] = 1
                        end
                    }
                    @3:
                    fade_transition(msg=20, param1=34, param2=1)
                    param_data 00000033
                }
            }
            """;

        var rpj = TextScene.Read(bdsl);
        var block = Assert.Single(Assert.Single(rpj.Entries).ScriptBlocks);
        Assert.Equal(2u, block.BlockType);
        Assert.Equal(0xD22u, block.Conditions[0].Operand);
        var ops = block.Elements.OfType<ScriptInstruction>().Select(i => i.Opcode).ToArray();
        Assert.Equal(new uint[] { 5057, 5061, 5058, 5042, 5004, 5003, 5023, 5000, 5031 }, ops);
        Assert.Equal(3u, ((ScriptInstruction)block.Elements[7]).RawParams[0]);
        Assert.Equal(new byte[] { 0, 0, 0, 0x33 }, block.ParamData);
    }

    [Fact]
    public void Parse_BadBlockCondition_ReportsLine()
    {
        var bdsl = """
            spawn "E" id=1 {
                when nonsense(1) {
                    end
                }
            }
            """;

        var ex = Assert.Throws<FormatException>(() => TextScene.Read(bdsl));
        Assert.Contains("BDSL line 2", ex.Message);
        Assert.Contains("nonsense(1)", ex.Message);
    }

    [Fact]
    public void Parse_BadParamData_ReportsLine()
    {
        var bdsl = """
            spawn "E" id=1 {
                when all {
                    end
                    param_data ZZZZ
                }
            }
            """;

        var ex = Assert.Throws<FormatException>(() => TextScene.Read(bdsl));
        Assert.Contains("BDSL line 4", ex.Message);
    }

    [Fact]
    public void Parse_TooManyBlockConditions_ReportsLine()
    {
        var slots = string.Join(" and ", Enumerable.Repeat("savebit(1)", 9));
        var bdsl = "spawn \"E\" id=1 {\n    when " + slots + " {\n        end\n    }\n}";

        var ex = Assert.Throws<FormatException>(() => TextScene.Read(bdsl));
        Assert.Contains("BDSL line 2", ex.Message);
        Assert.Contains("more than 8 block conditions", ex.Message);
    }
}
