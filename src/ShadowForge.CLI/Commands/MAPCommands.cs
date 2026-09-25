using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShadowForge.Formats.MAP;
using ShadowForge.GameData.Maps;
using ShadowForge.Manifests;

namespace ShadowForge.CLI.Commands;

public static class MAPCommands
{
    public static Command Create(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var cmd = new Command("map", "Scene .map operations.");
        cmd.Subcommands.Add(BuildInspect(getFactory));
        cmd.Subcommands.Add(BuildList(getFactory));
        cmd.Subcommands.Add(BuildExport(getFactory));
        return cmd;
    }

    private static Command BuildInspect(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "A .map file." };
        var cmd = new Command("inspect", "List the models and parts a .map references.");
        cmd.Arguments.Add(fileArg);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("map.inspect");
            var file = pr.GetValue(fileArg)!;
            try
            {
                var map = StageDef.ReadFile(file.FullName);
                Console.WriteLine($"models {map.Models.Count}, parts {map.Parts.Count}");
                foreach (var m in map.Models)
                    Console.WriteLine($"  MODEL {m.Name} -> {m.ObjectHDB}");
                foreach (var p in map.Parts)
                    Console.WriteLine($"  PART {p.Kind} -> {p.Path}");
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to inspect {File}", file.FullName);
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildList(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var rootOpt = new GameRootOption();
        var cmd = new Command("list", "List map stages (id, category, region pack) as JSON.");
        cmd.Options.Add(rootOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("map.list");
            try
            {
                var result = new MapCatalog(rootOpt.Locate(pr)).List();
                foreach (string w in result.Warnings) log.LogWarning("{Warning}", w);
                var dto = new MapListDto(result.Stages
                    .Select(s => new MapStageDto(
                        s.StageId, "map", s.Category, s.DisplayName, s.RegionIPK, s.RegionAvailable, s.ModelCount))
                    .ToList());
                Console.WriteLine(JsonSerializer.Serialize(dto, ManifestJson.Default.MapListDto));
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "map list failed");
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildExport(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var stageArg = new Argument<string>("stage") { Description = "Stage id (bg01_01) or db_<stage>.map." };
        var outOpt = new Option<string?>("-o") { Description = "Output directory (default: current directory)." };
        var noTexOpt = new Option<bool>("--no-textures") { Description = "Skip textures and use flat material colors." };
        var jsonOpt = CliOutput.CreateJsonOption();
        var rootOpt = new GameRootOption();
        var cmd = new Command("export", "Export a map stage to <stage>.glb and <stage>.sfmap.json.");
        cmd.Arguments.Add(stageArg);
        cmd.Options.Add(outOpt);
        cmd.Options.Add(noTexOpt);
        cmd.Options.Add(jsonOpt);
        cmd.Options.Add(rootOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("map.export");
            bool json = pr.GetValue(jsonOpt);
            string stage = pr.GetValue(stageArg)!;
            try
            {
                string outDir = pr.GetValue(outOpt) ?? Directory.GetCurrentDirectory();
                var result = new MapExporter(rootOpt.Locate(pr))
                    .Export(stage, outDir, includeTextures: !pr.GetValue(noTexOpt),
                            progress: line => log.LogInformation("{Progress}", line));
                var warnings = result.Warnings.Select(w => new CliWarning("export", w)).ToList();
                return CliOutput.Success(json, "map.export", [result.GlbPath, result.ManifestPath], warnings,
                    $"Exported {stage} -> {result.GlbPath}");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "map export failed");
                return CliOutput.Failure(json, "map.export", ex.Message);
            }
        });
        return cmd;
    }
}
