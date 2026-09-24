using ShadowForge.Marshal;
using ShadowForge.Scene;
using ShadowForge.Scene.Script;

namespace ShadowForge.Formats.RPJ.Wire;

[BigEndian, StructSize(0x100)]
public partial struct ScriptBlockData
{
    public uint ChapterMin;
    public uint ChapterMax;
    public ConditionData Condition0;
    public ConditionData Condition1;
    public ConditionData Condition2;
    public ConditionData Condition3;
    public ConditionData Condition4;
    public ConditionData Condition5;
    public ConditionData Condition6;
    public ConditionData Condition7;
    public uint UnkField88;
    public uint UnkField8C;
    public uint AutoRunFlags;
    public uint EncounterMode;
    public float EncounterRange;
    public uint AutoSetVarFlag;
    public uint AutoSetVarIndex;
    public uint UnkFieldA4;
    public uint UnkFieldA8;
    public uint UnkFieldAC;
    public uint UnkFieldB0;
    public uint UnkFieldB4;
    public uint UnkFieldB8;
    public uint UnkFieldBC;
    public uint UnkFieldC0;
    public uint LinkedEntryId;
    public uint SpawnFlags;
    public float SpawnAngle;
    public float SpawnScale;
    public uint RenderFlags;
    public int BehaviorMode;
    public float InteractionRadius;
    public uint BlockType;
    public uint TypeData;
    public uint SpawnMode;
    [Computed] public uint BytecodeSize;
    [Computed] public uint ParamDataSize;
    [Computed] public uint BytecodeRelOffset;
    [Computed] public uint ParamRelOffset;
    [Computed] public uint NextBlock;

    public ScriptBlock ToModel()
    {
        return new ScriptBlock
        {
            ChapterMin = ChapterMin,
            ChapterMax = ChapterMax,
            UnkField88 = UnkField88,
            UnkField8C = UnkField8C,
            AutoRunFlags = AutoRunFlags,
            EncounterMode = EncounterMode,
            EncounterRange = EncounterRange,
            AutoSetVarFlag = AutoSetVarFlag,
            AutoSetVarIndex = AutoSetVarIndex,
            UnkFieldA4 = UnkFieldA4,
            UnkFieldA8 = UnkFieldA8,
            UnkFieldAC = UnkFieldAC,
            UnkFieldB0 = UnkFieldB0,
            UnkFieldB4 = UnkFieldB4,
            UnkFieldB8 = UnkFieldB8,
            UnkFieldBC = UnkFieldBC,
            UnkFieldC0 = UnkFieldC0,
            LinkedEntryId = LinkedEntryId,
            SpawnFlags = SpawnFlags,
            SpawnAngle = SpawnAngle,
            SpawnScale = SpawnScale,
            RenderFlags = RenderFlags,
            BehaviorMode = (BehaviorMode)BehaviorMode,
            InteractionRadius = InteractionRadius,
            BlockType = BlockType,
            TypeData = TypeData,
            SpawnMode = SpawnMode,
            Conditions =
            [
                Condition0.ToCondition(), Condition1.ToCondition(), Condition2.ToCondition(), Condition3.ToCondition(),
                Condition4.ToCondition(), Condition5.ToCondition(), Condition6.ToCondition(), Condition7.ToCondition(),
            ],
        };
    }

    public static ScriptBlockData FromModel(ScriptBlock model)
    {
        return new ScriptBlockData
        {
            ChapterMin = model.ChapterMin,
            ChapterMax = model.ChapterMax,
            Condition0 = ConditionData.FromCondition(model.Conditions[0]),
            Condition1 = ConditionData.FromCondition(model.Conditions[1]),
            Condition2 = ConditionData.FromCondition(model.Conditions[2]),
            Condition3 = ConditionData.FromCondition(model.Conditions[3]),
            Condition4 = ConditionData.FromCondition(model.Conditions[4]),
            Condition5 = ConditionData.FromCondition(model.Conditions[5]),
            Condition6 = ConditionData.FromCondition(model.Conditions[6]),
            Condition7 = ConditionData.FromCondition(model.Conditions[7]),
            UnkField88 = model.UnkField88,
            UnkField8C = model.UnkField8C,
            AutoRunFlags = model.AutoRunFlags,
            EncounterMode = model.EncounterMode,
            EncounterRange = model.EncounterRange,
            AutoSetVarFlag = model.AutoSetVarFlag,
            AutoSetVarIndex = model.AutoSetVarIndex,
            UnkFieldA4 = model.UnkFieldA4,
            UnkFieldA8 = model.UnkFieldA8,
            UnkFieldAC = model.UnkFieldAC,
            UnkFieldB0 = model.UnkFieldB0,
            UnkFieldB4 = model.UnkFieldB4,
            UnkFieldB8 = model.UnkFieldB8,
            UnkFieldBC = model.UnkFieldBC,
            UnkFieldC0 = model.UnkFieldC0,
            LinkedEntryId = model.LinkedEntryId,
            SpawnFlags = model.SpawnFlags,
            SpawnAngle = model.SpawnAngle,
            SpawnScale = model.SpawnScale,
            RenderFlags = model.RenderFlags,
            BehaviorMode = (int)model.BehaviorMode,
            InteractionRadius = model.InteractionRadius,
            BlockType = model.BlockType,
            TypeData = model.TypeData,
            SpawnMode = model.SpawnMode,
        };
    }
}
