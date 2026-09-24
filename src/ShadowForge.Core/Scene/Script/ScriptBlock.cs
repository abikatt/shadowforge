namespace ShadowForge.Scene.Script;

/// <summary>
/// One activation block of an entry: chapter range, condition slots, metadata and bytecode.
/// </summary>
public sealed class ScriptBlock
{
    /// <summary>
    /// 0xFFFFFFFF makes the block unconditional on chapter.
    /// </summary>
    public uint ChapterMin { get; set; }

    /// <summary>
    /// Inclusive.
    /// </summary>
    public uint ChapterMax { get; set; }

    public Condition[] Conditions { get; set; } = new Condition[8];

    public uint UnkField88 { get; set; }

    public uint UnkField8C { get; set; }

    public uint AutoRunFlags { get; set; }

    /// <summary>
    /// 1 normal, 2 boss.
    /// </summary>
    public uint EncounterMode { get; set; }

    public float EncounterRange { get; set; }

    public uint AutoSetVarFlag { get; set; }

    public uint AutoSetVarIndex { get; set; }

    public uint UnkFieldA4 { get; set; }

    public uint UnkFieldA8 { get; set; }

    public uint UnkFieldAC { get; set; }

    public uint UnkFieldB0 { get; set; }

    public uint UnkFieldB4 { get; set; }

    public uint UnkFieldB8 { get; set; }

    public uint UnkFieldBC { get; set; }

    public uint UnkFieldC0 { get; set; }

    public uint LinkedEntryId { get; set; }

    public uint SpawnFlags { get; set; }

    public float SpawnAngle { get; set; }

    public float SpawnScale { get; set; }

    public uint RenderFlags { get; set; }

    public BehaviorMode BehaviorMode { get; set; }

    public float InteractionRadius { get; set; }

    /// <summary>
    /// 1 event, 2 npc, 3 auto_run, 4 link, 5 cube.
    /// </summary>
    public uint BlockType { get; set; }

    public uint TypeData { get; set; }

    public uint SpawnMode { get; set; }

    public List<ScriptElement> Elements { get; set; } = [];

    public byte[] ParamData { get; set; } = [];

    public bool IsUnconditional => ChapterMin == 0xFFFFFFFF;

    /// <summary>
    /// True when every slot has Type 0. Unlike <see cref="Condition.IsEmpty"/>, the other three words are ignored.
    /// </summary>
    public bool HasNoConditions
    {
        get
        {
            for (int i = 0; i < Conditions.Length; i++)
                if (Conditions[i].Type != 0) return false;
            return true;
        }
    }

    /// <summary>
    /// True when every UnkField word is zero.
    /// </summary>
    public bool ReservedRegionsAreZero =>
        UnkField88 == 0 && UnkField8C == 0 &&
        UnkFieldA4 == 0 && UnkFieldA8 == 0 && UnkFieldAC == 0 && UnkFieldB0 == 0 &&
        UnkFieldB4 == 0 && UnkFieldB8 == 0 && UnkFieldBC == 0 && UnkFieldC0 == 0;

    public bool HasAnyMetadata =>
        AutoRunFlags != 0 || EncounterMode != 0 || EncounterRange != 0f ||
        AutoSetVarFlag != 0 || AutoSetVarIndex != 0 || LinkedEntryId != 0 ||
        SpawnFlags != 0 || SpawnAngle != 0f || SpawnScale != 0f ||
        RenderFlags != 0 || BehaviorMode != BehaviorMode.None || InteractionRadius != 0f ||
        BlockType != 0 || TypeData != 0 || SpawnMode != 0;
}
