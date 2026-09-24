using ShadowForge.Formats.BDSL;
using ShadowForge.Formats.RPJ;
using ShadowForge.Scene.Script.Lift;

namespace ShadowForge.Tests.BDSL;

public sealed class RetailScriptRoundTripTests
{
    private static IEnumerable<(string Name, byte[] Bytes)> ShippedScripts()
    {
        var files = RetailData.Files;
        var paths = files.EnumerateVfs("script", "*.rpj");
        Skip.If(paths.Count == 0, "no .rpj scripts in the located game data");
        return paths.Select(p => (Path.GetFileName(p), files.ReadVfs(p)));
    }

    [SkippableFact]
    public void EveryShippedScript_LiftsAndLowersToTheSameBytecode()
    {
        var failures = new List<string>();
        foreach (var (name, bytes) in ShippedScripts())
        {
            var scene = BinaryScene.Read(bytes);
            foreach (var entry in scene.Entries)
            {
                foreach (var block in entry.ScriptBlocks)
                {
                    var lowered = ScriptLowering.Lower(ScriptLifter.Lift(block.Elements));
                    if (Bytecode.Describe(lowered) != Bytecode.Describe(block.Elements))
                        failures.Add($"{name} entry {entry.Id}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [SkippableFact]
    public void EveryShippedScript_RecompilesByteIdentical()
    {
        var failures = new List<string>();
        foreach (var (name, original) in ShippedScripts())
        {
            var baseline = BinaryScene.Write(BinaryScene.Read(original));
            byte[] compiled;
            try
            {
                compiled = BinaryScene.Write(TextScene.Read(TextScene.Write(BinaryScene.Read(original))));
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: {ex.Message}");
                continue;
            }

            if (!baseline.AsSpan().SequenceEqual(compiled))
                failures.Add($"{name}: {baseline.Length} vs {compiled.Length} bytes");
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// Pins the one shipped file the RPJ binary layer cannot reproduce: sc02_1707_02.rpj is
    /// the only version 0.18 scene and uses a 0x54 entry stride and 0xF0 script block header
    /// instead of the 0x70 and 0x100 of every later version. Fails once version 0.18 support
    /// exists, and fails if any second file regresses.
    /// </summary>
    [SkippableFact]
    public void BinaryLayerRoundTrip_HasOneKnownVersion018Gap()
    {
        var differing = new List<string>();
        foreach (var (name, original) in ShippedScripts())
        {
            var baseline = BinaryScene.Write(BinaryScene.Read(original));
            if (!original.AsSpan().SequenceEqual(baseline))
                differing.Add(name);
        }

        Assert.Equal(["sc02_1707_02.rpj"], differing);
    }

    [SkippableFact]
    public void EveryShippedScript_TextIsStableUnderReformat()
    {
        var failures = new List<string>();
        foreach (var (name, bytes) in ShippedScripts())
        {
            var text = TextScene.Write(BinaryScene.Read(bytes));
            var again = TextScene.Write(TextScene.Read(text));
            if (text != again)
                failures.Add(name);
        }

        Assert.Empty(failures);
    }
}
