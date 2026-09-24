using ShadowForge.Marshal;
using ShadowForge.Scene.Script;

namespace ShadowForge.Formats.RPJ.Wire;

[BigEndian, StructSize(0x10)]
public partial struct ConditionData
{
    public uint Type;
    public uint Operand;
    public uint Op;
    public uint Value;

    public Condition ToCondition()
        => new(Type, Operand, Op, Value);

    public static ConditionData FromCondition(Condition cond)
        => new() { Type = cond.Type, Operand = cond.Operand, Op = cond.Op, Value = cond.Value };
}
