using System.CommandLine;
using System.Text;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using ShadowForge.Formats.DDS;
using ShadowForge.GameData;
using ShadowForge.Minimap;

namespace ShadowForge.CLI.Commands;

public static class MinimapCommands
{
    public static Command Create(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var cmd = new Command("minimap", "Generate top-down minimap textures and descriptors.");
        cmd.Subcommands.Add(BuildGen(getFactory));
        return cmd;
    }

    private static Command BuildGen(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var rootOpt = new GameRootOption();
        var outOpt = new Option<string>("--out") { Description = "Output directory for the minimap, preview and database tree.", Required = true };
        var stemOpt = new Option<string?>("--stem") { Description = "Single stage stem, e.g. bg01_01." };
        var allOpt = new Option<bool>("--all") { Description = "Process every db_bg*.map and db_bi*.map stage." };

        var cmd = new Command("gen", "Generate a minimap texture, preview PNG and .mmp descriptor for one or every stage.");
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(outOpt);
        cmd.Options.Add(stemOpt);
        cmd.Options.Add(allOpt);

        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("minimap.gen");
            string outDir = pr.GetValue(outOpt)!;
            string? stem = pr.GetValue(stemOpt);
            bool all = pr.GetValue(allOpt);

            try
            {
                if (stem is null && !all)
                {
                    log.LogError("Either --stem <stem> or --all must be specified.");
                    return 1;
                }
                if (stem is not null && all)
                {
                    log.LogError("--stem and --all are mutually exclusive.");
                    return 1;
                }

                var files = new GameFileSystem(rootOpt.Locate(pr));
                List<string> stems = all ? TownAndIndoorStems(files) : [stem!];
                if (stems.Count == 0)
                {
                    log.LogError("No db_bg*.map or db_bi*.map stages found in the game data.");
                    return 1;
                }

                int generated = 0, failed = 0, skipped = 0;
                foreach (string s in stems)
                {
                    switch (GenerateStem(files, outDir, s, log))
                    {
                        case StemOutcome.Generated: generated++; break;
                        case StemOutcome.Skipped: skipped++; break;
                        default: failed++; break;
                    }
                }

                Console.WriteLine($"generated {generated}, failed {failed}, skipped {skipped}");
                return failed > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "minimap gen failed");
                return 1;
            }
        });
        return cmd;
    }

    private static List<string> TownAndIndoorStems(GameFileSystem files) =>
        files.EnumerateVfs(@"database\map", "db_b*.map")
            .Select(f => Path.GetFileNameWithoutExtension(f)["db_".Length..])
            .Where(s => s.StartsWith("bg", StringComparison.OrdinalIgnoreCase)
                        || s.StartsWith("bi", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private enum StemOutcome { Generated, Failed, Skipped }

    private static StemOutcome GenerateStem(GameFileSystem files, string outDir, string stem, ILogger log)
    {
        StageMesh mesh;
        try
        {
            mesh = MapAssembler.AssembleStage(files, stem, line => log.LogInformation("{Stem}: {Line}", stem, line));
        }
        catch (Exception ex)
        {
            log.LogWarning("{Stem}: failed to assemble stage - {Message}", stem, ex.Message);
            return StemOutcome.Failed;
        }

        var mask = Mask.Rasterize(mesh.Render, mesh.Collision);
        if (mask.WorldScaleX <= 0f || mask.WorldScaleY <= 0f)
        {
            log.LogWarning("{Stem}: no floor triangles found, skipping", stem);
            return StemOutcome.Skipped;
        }

        string previewPath = Path.Combine(outDir, "preview", $"MM_{stem}.png");
        string ddsPath = Path.Combine(outDir, "minimap", $"MM_{stem}.dds");
        string mmpPath = Path.Combine(outDir, "database", "minimap", $"db_{stem}.mmp");

        try
        {
            using (var image = Style.Render(mask))
            {
                OutputPath.CreateParentDirectory(previewPath);
                image.SaveAsPng(previewPath);
            }

            string refVfs = mask.Height == mask.Width ? @"minimap\MM_dg01_01.dds" : @"minimap\MM_dg05_03.dds";
            if (!files.ExistsVfs(refVfs))
            {
                log.LogWarning("{Stem}: reference DDS {Ref} is missing from the game data", stem, refVfs);
                return StemOutcome.Failed;
            }

            string refPath = Path.Combine(Path.GetTempPath(), $"sforge_{Guid.NewGuid():N}.dds");
            try
            {
                File.WriteAllBytes(refPath, files.ReadVfs(refVfs));
                OutputPath.CreateParentDirectory(ddsPath);
                Importer.ImportWithReference(previewPath, refPath, ddsPath);
            }
            finally
            {
                File.Delete(refPath);
            }

            OutputPath.CreateParentDirectory(mmpPath);
            File.WriteAllBytes(mmpPath, Encoding.ASCII.GetBytes(MmpWriter.Emit(mask)));
        }
        catch (Exception ex)
        {
            log.LogWarning("{Stem}: failed to generate output - {Message}", stem, ex.Message);
            return StemOutcome.Failed;
        }

        log.LogInformation("{Stem}: generated", stem);
        return StemOutcome.Generated;
    }
}
