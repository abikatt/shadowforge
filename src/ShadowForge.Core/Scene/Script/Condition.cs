namespace ShadowForge.Scene.Script;

/// <summary>
/// One of a script block's eight activation condition slots.
/// </summary>
public readonly record struct Condition(uint Type, uint Operand, uint Op, uint Value)
{
    public bool IsEmpty => Type == 0 && Operand == 0 && Op == 0 && Value == 0;
}
