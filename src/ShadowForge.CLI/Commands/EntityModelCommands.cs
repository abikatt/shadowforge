using System.CommandLine;
using Microsoft.Extensions.Logging;
using ShadowForge.GameData.Entities;

namespace ShadowForge.CLI.Commands;

/// <summary>
/// The entity GLTF round trip: export for editing, import to cook the edit back.
/// </summary>
internal static class EntityModelCommands
{
    public static Command BuildExport(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var idArg = EntityCommands.IdArgument();
        var outOpt = new Option<string?>("-o") { Description = "Output directory (default: current directory)." };
        var fmtOpt = new Option<string?>("--format")
            { Description = "glb (default, self-contained) or gltf (external textures)." };
        var jsonOpt = CliOutput.CreateJsonOption();
        var noAnimsOpt = new Option<bool>("--no-anims") { Description = "Skip animation clips for a faster, smaller export." };
        var noTexOpt = new Option<bool>("--no-textures") { Description = "Skip textures and use flat material colors." };
        var rootOpt = new GameRootOption();
        var overlayOpt = new OverlayOption();
        var cmd = new Command("export", "Export an entity to <id>.glb and <id>.sfmod.json for editing.");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(outOpt);
        cmd.Options.Add(fmtOpt);
        cmd.Options.Add(jsonOpt);
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(overlayOpt);
        cmd.Options.Add(noAnimsOpt);
        cmd.Options.Add(noTexOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("entity.export");
            bool json = pr.GetValue(jsonOpt);
            try
            {
                string outDir = pr.GetValue(outOpt) ?? Directory.GetCurrentDirectory();
                string ext = GLTFFormat.Resolve(pr.GetValue(fmtOpt))
                    ?? throw new ArgumentException($"Unknown --format '{pr.GetValue(fmtOpt)}'. {GLTFFormat.Supported}");

                var result = new EntityExporter(rootOpt.Locate(pr, overlayOpt)).Export(pr.GetValue(idArg)!, outDir,
                    embed: ext == ".glb", includeAnimations: !pr.GetValue(noAnimsOpt),
                    includeTextures: !pr.GetValue(noTexOpt), progress: m => log.LogInformation("{Message}", m));

                var warnings = result.Warnings.Select(w => new CliWarning("export", w)).ToList();
                return CliOutput.Success(json, "entity.export", [result.ModelPath, result.ManifestPath], warnings,
                    $"Exported {result.Id} -> {result.ModelPath} (+ {Path.GetFileName(result.ManifestPath)}), {result.ClipCount} clip(s)");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "export failed");
                return CliOutput.Failure(json, "entity.export", ex.Message);
            }
        });
        return cmd;
    }

    public static Command BuildImport(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var idArg = EntityCommands.IdArgument();
        var fromOpt = new Option<string?>("--from") { Description = "Directory holding the edited <id>.glb, required." };
        var outOpt = new Option<string?>("-o") { Description = "Cooked output directory (default: <from>/cooked)." };
        var jsonOpt = CliOutput.CreateJsonOption();
        var rootOpt = new GameRootOption();
        var overlayOpt = new OverlayOption();
        var cmd = new Command("import", "Cook an edited glb back into <id>_obj.hdb, <id>_mot.mpk, and textures.");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(fromOpt);
        cmd.Options.Add(outOpt);
        cmd.Options.Add(jsonOpt);
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(overlayOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("entity.import");
            bool json = pr.GetValue(jsonOpt);
            try
            {
                string from = EntityCommands.RequireFromDir(pr.GetValue(fromOpt));
                var install = rootOpt.Locate(pr, overlayOpt);
                var entity = new EntityResolver(install).Resolve(pr.GetValue(idArg)!);
                string glb = EntityCooker.FindEditedModel(from, entity.Id);
                string cooked = pr.GetValue(outOpt) ?? Path.Combine(from, "cooked");

                var result = new EntityCooker(install).Cook(entity, glb, cooked, log);
                var warnings = result.Warnings.Select(w => new CliWarning("import", w)).ToList();
                return CliOutput.Success(json, "entity.import", result.Outputs, warnings,
                    $"Cooked {entity.Id} -> {cooked} ({result.Outputs.Count} file(s))");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "import failed");
                return CliOutput.Failure(json, "entity.import", ex.Message);
            }
        });
        return cmd;
    }
}
