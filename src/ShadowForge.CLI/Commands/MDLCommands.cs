using System.CommandLine;
using Microsoft.Extensions.Logging;
using ShadowForge.Formats.MDL;
using ShadowForge.GameData;

namespace ShadowForge.CLI.Commands;

public static class MDLCommands
{
    public static Command Create(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var cmd = new Command("mdl", "Object-binding .mdl operations.");
        cmd.Subcommands.Add(BuildInspect(getFactory));
        return cmd;
    }

    private static Command BuildInspect(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<string>("file-or-id") { Description = "A .mdl file, or an entity id (pc01)." };
        var rootOpt = new GameRootOption();
        var cmd = new Command("inspect", "Print the bindings a .mdl declares.");
        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(rootOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("mdl.inspect");
            string arg = pr.GetValue(fileArg)!;
            try
            {
                using var input = EntityArg.File(
                    pr.GetValue(rootOpt), arg, FileRole.ModelDef, withSiblings: false);
                var mdl = ModelDef.ReadFile(input.Path);
                Console.WriteLine($"PATH    {mdl.Path}");
                Console.WriteLine($"OBJECT  {mdl.ObjectHDB}");
                Console.WriteLine($"OBJECTL0 {mdl.ObjectL0HDB}");
                Console.WriteLine($"MOTPACK {mdl.MotPack}");
                Console.WriteLine($"clips   {mdl.Clips.Count}");
                foreach (var c in mdl.Clips)
                    Console.WriteLine($"  {c.Name} -> {c.HMBFile}");
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to inspect {File}", arg);
                return 1;
            }
        });
        return cmd;
    }
}
