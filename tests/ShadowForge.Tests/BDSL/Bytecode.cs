using ShadowForge.Scene.Script;

namespace ShadowForge.Tests.BDSL;

internal static class Bytecode
{
    public static ScriptInstruction I(uint opcode, params uint[] args) =>
        new() { Opcode = opcode, Size = (uint)(8 + 4 * args.Length), RawParams = args };

    public static string Describe(IEnumerable<ScriptElement> elements) =>
        string.Join("\n", elements.Select(e => e switch
        {
            ScriptInstruction i => $"{i.Opcode:X8}:{i.Size}:" + string.Join(",", i.RawParams.Select(p => p.ToString("X"))),
            ScriptData d => "raw:" + Convert.ToHexString(d.Bytes),
            _ => "?",
        }));
}
