using System.CommandLine;
using Microsoft.Extensions.Logging;
using ShadowForge.Formats.EVT;

namespace ShadowForge.CLI.Commands;

public static class EVTCommands
{
    public static Command Create(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var cmd = new Command("evt", "EVT event scene operations.");
        cmd.Subcommands.Add(BuildDecompileCommand(getFactory));
        return cmd;
    }

    private static Command BuildDecompileCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "EVT event scene file." };
        var trackOpt = new Option<string?>("--track")
            { Description = "List only tracks whose model name or label contains this text." };
        var rawOpt = new Option<bool>("--raw")
            { Description = "Show each key as payload words instead of decoded text." };
        var outputOpt = new Option<FileInfo?>("-o") { Description = "Write the listing to a file instead of stdout." };
        var cmd = new Command("decompile", "List every track, entry frame, and key of an event scene.");
        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(trackOpt);
        cmd.Options.Add(rawOpt);
        cmd.Options.Add(outputOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("evt.decompile");
            var file = pr.GetValue(fileArg)!;
            try
            {
                var scene = EventSceneReader.Read(File.ReadAllBytes(file.FullName), Path.GetFileNameWithoutExtension(file.Name));
                string text = EventSceneDecompiler.Decompile(scene, pr.GetValue(trackOpt), pr.GetValue(rawOpt));
                var output = pr.GetValue(outputOpt);
                if (output is null)
                {
                    Console.Write(text);
                }
                else
                {
                    OutputPath.CreateParentDirectory(output.FullName);
                    File.WriteAllText(output.FullName, text);
                    Console.WriteLine($"wrote {output.FullName}");
                }
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to decompile {File}", file.FullName);
                return 1;
            }
        });
        return cmd;
    }
}
