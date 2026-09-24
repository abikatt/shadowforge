namespace ShadowForge.Scene.Script;

/// <summary>
/// Name, fixed word count and argument specs of one opcode.
/// </summary>
public sealed record OpcodeSpec(uint Opcode, string Name, int Words, IReadOnlyList<ArgSpec> Args)
{
    /// <summary>
    /// Spec of the argument at the index, or a positional pN Int spec past the named ones.
    /// </summary>
    public ArgSpec ArgAt(int index) =>
        index < Args.Count ? Args[index] : new ArgSpec($"p{index}", ArgKind.Int);

    /// <summary>
    /// Index of a named argument, accepting any pN below MaxWords, or -1.
    /// </summary>
    public int IndexOfArg(string name)
    {
        for (int i = 0; i < Args.Count; i++)
            if (Args[i].Name == name) return i;
        if (name.Length > 1 && name[0] == 'p' && int.TryParse(name.AsSpan(1), out int n)
            && n >= 0 && n < OpcodeTable.MaxWords)
            return n;
        return -1;
    }
}
