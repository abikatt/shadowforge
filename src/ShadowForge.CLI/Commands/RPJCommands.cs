using System.CommandLine;
using Microsoft.Extensions.Logging;
using ShadowForge.Formats.BDSL;
using ShadowForge.Formats.RPJ;

namespace ShadowForge.CLI.Commands;

public static class RPJCommands
{
    public static Command Create(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var cmd = new Command("rpj", "RPJ scene script operations.");
        cmd.Subcommands.Add(BuildSingleCommand(getFactory, "decompile", "Decompile an RPJ binary to BDSL text.",
            "RPJ file to decompile.", ".bdsl", Decompile));
        cmd.Subcommands.Add(BuildSingleCommand(getFactory, "compile", "Compile BDSL text to an RPJ binary.",
            "BDSL file to compile.", ".rpj", Compile));
        cmd.Subcommands.Add(BuildBatchCommand(getFactory, "batch-decompile", "Decompile every RPJ file in a directory.",
            "Directory containing RPJ files.", "*.rpj", ".bdsl", "decompiled", Decompile));
        cmd.Subcommands.Add(BuildBatchCommand(getFactory, "batch-compile", "Compile every BDSL file in a directory.",
            "Directory containing BDSL files.", "*.bdsl", ".rpj", "compiled", Compile));
        return cmd;
    }

    /// <summary>
    /// Returns a summary line for the single-file command.
    /// </summary>
    private static string Decompile(string input, string output)
    {
        var rpj = BinaryScene.Read(input);
        TextScene.Write(rpj, output);
        int scriptBlocks = rpj.Entries.SelectMany(e => e.ScriptBlocks).Count();
        return $"  {rpj.Entries.Count} entries, {scriptBlocks} script blocks, {rpj.Waypoints.Count} waypoints";
    }

    private static string? Compile(string input, string output)
    {
        BinaryScene.Write(TextScene.ReadFile(input), output);
        return null;
    }

    private static Command BuildSingleCommand(
        Func<ParseResult, ILoggerFactory> getFactory, string name, string description,
        string fileDescription, string outputExtension, Func<string, string, string?> convert)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = fileDescription };
        var outputOpt = new Option<FileInfo?>("-o") { Description = $"Output {outputExtension} file." };
        var cmd = new Command(name, description);
        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(outputOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("rpj." + name);
            var file = pr.GetValue(fileArg)!;
            try
            {
                string output = OutputPath.ForFile(pr.GetValue(outputOpt), file.FullName, outputExtension);
                string? summary = convert(file.FullName, output);
                Console.WriteLine($"Written to {output}");
                if (summary is not null)
                    Console.WriteLine(summary);
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to {Verb} {File}", name, file.FullName);
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildBatchCommand(
        Func<ParseResult, ILoggerFactory> getFactory, string name, string description, string dirDescription,
        string pattern, string outputExtension, string doneVerb, Func<string, string, string?> convert)
    {
        var dirArg = new Argument<DirectoryInfo>("directory") { Description = dirDescription };
        var outputOpt = new Option<DirectoryInfo?>("-o") { Description = "Output directory." };
        var cmd = new Command(name, description);
        cmd.Arguments.Add(dirArg);
        cmd.Options.Add(outputOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("rpj." + name);
            var dir = pr.GetValue(dirArg)!;
            var outputDir = pr.GetValue(outputOpt) ?? dir;
            return BatchConvert.Run(BatchConvert.Find(dir, pattern), doneVerb, log, file =>
            {
                convert(file, BatchConvert.MirrorPath(file, dir, outputDir, outputExtension));
                return null;
            });
        });
        return cmd;
    }
}
