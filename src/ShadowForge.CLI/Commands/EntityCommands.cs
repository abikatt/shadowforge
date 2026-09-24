using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShadowForge.GameData;
using ShadowForge.GameData.Entities;
using ShadowForge.Manifests;

namespace ShadowForge.CLI.Commands;

public static class EntityCommands
{
    public static Command Create(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var cmd = new Command("entity", "Resolve, mount, validate, and mod a game entity by id or path.");
        cmd.Subcommands.Add(BuildResolve(getFactory));
        cmd.Subcommands.Add(BuildList(getFactory));
        cmd.Subcommands.Add(BuildMount(getFactory));
        cmd.Subcommands.Add(BuildValidate(getFactory));
        cmd.Subcommands.Add(EntityModCommands.BuildPack(getFactory));
        cmd.Subcommands.Add(EntityModelCommands.BuildExport(getFactory));
        cmd.Subcommands.Add(EntityModelCommands.BuildImport(getFactory));
        cmd.Subcommands.Add(EntityModCommands.BuildDeploy(getFactory));
        cmd.Subcommands.Add(BuildDerive(getFactory));
        return cmd;
    }

    internal static Argument<string> IdArgument() =>
        new("id-or-path") { Description = "Entity id (pc01) or model_<id>.mdl path." };

    internal static string RequireFromDir(string? fromDir) =>
        !string.IsNullOrWhiteSpace(fromDir) && Directory.Exists(fromDir)
            ? fromDir
            : throw new DirectoryNotFoundException("--from <dir> is required and must exist.");

    private static Command BuildResolve(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var idArg = IdArgument();
        var rootOpt = new GameRootOption();
        var overlayOpt = new OverlayOption();
        var cmd = new Command("resolve", "Print an entity's resolved file manifest as JSON.");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(overlayOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("entity.resolve");
            try
            {
                var install = rootOpt.Locate(pr, overlayOpt);
                var entity = new EntityResolver(install).Resolve(pr.GetValue(idArg)!);
                var dto = ManifestBuilder.Resolve(entity, install);
                Console.WriteLine(JsonSerializer.Serialize(dto, ManifestJson.Default.ResolvedEntityDto));
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "resolve failed");
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildList(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var rootOpt = new GameRootOption();
        var overlayOpt = new OverlayOption();
        var categoryOpt = new Option<string?>("--category") { Description = "Category to list, chara is the only one." };
        var cmd = new Command("list", "List browsable entities (id, category, class) as JSON.");
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(overlayOpt);
        cmd.Options.Add(categoryOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("entity.list");
            try
            {
                string category = pr.GetValue(categoryOpt) ?? "chara";
                if (!string.Equals(category, "chara", StringComparison.OrdinalIgnoreCase))
                {
                    log.LogError("Unsupported --category '{Category}', use chara.", category);
                    return 1;
                }
                var entries = new EntityCatalog(rootOpt.Locate(pr, overlayOpt)).ListChara()
                    .Select(e => new CatalogEntryDto(e.Id, e.Category, e.Class, e.ModelDefRelPath, e.DisplayName))
                    .ToList();
                Console.WriteLine(JsonSerializer.Serialize(new EntityListDto(entries), ManifestJson.Default.EntityListDto));
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "list failed");
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildMount(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var idArg = IdArgument();
        var outOpt = new Option<DirectoryInfo?>("-o") { Description = "Output directory, required." };
        var rootOpt = new GameRootOption();
        var overlayOpt = new OverlayOption();
        var cmd = new Command("mount", "Extract an entity's files into a working directory.");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(outOpt);
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(overlayOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("entity.mount");
            var outDir = pr.GetValue(outOpt);
            if (outDir is null)
            {
                log.LogError("-o <dir> is required.");
                return 1;
            }
            try
            {
                var install = rootOpt.Locate(pr, overlayOpt);
                var entity = new EntityResolver(install).Resolve(pr.GetValue(idArg)!);
                int count = new GameFileSystem(install).Mount(entity, outDir.FullName);
                Console.WriteLine($"Mounted {count} file(s) for {entity.Id} into {outDir.FullName}");
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "mount failed");
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildValidate(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var idArg = IdArgument();
        var rootOpt = new GameRootOption();
        var overlayOpt = new OverlayOption();
        var cmd = new Command("validate", "Check that an entity's required files exist.");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(overlayOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("entity.validate");
            try
            {
                var entity = new EntityResolver(rootOpt.Locate(pr, overlayOpt)).Resolve(pr.GetValue(idArg)!);
                int missing = 0;
                foreach (var f in entity.Files)
                {
                    bool required = f.Role is FileRole.ModelDef or FileRole.Skeleton or FileRole.Motion;
                    string mark = f.Exists ? "ok  " : (required ? "MISS" : "opt ");
                    Console.WriteLine($"[{mark}] {f.Role,-9} {f.VfsPath}");
                    if (required && !f.Exists) missing++;
                }
                Console.WriteLine(missing == 0 ? $"{entity.Id}: OK" : $"{entity.Id}: {missing} required file(s) MISSING");
                return missing == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "validate failed");
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildDerive(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var srcArg = new Argument<string>("source-id") { Description = "Entity id to copy from (np109, pc02)." };
        var asOpt = new Option<string>("--as") { Description = "New entity id (pc11).", Required = true };
        var classOpt = new Option<string?>("--class") { Description = "Asset class folder (default: derived from the new id)." };
        var outOpt = new Option<string>("--out") { Description = "Output entity directory.", Required = true };
        var rootOpt = new GameRootOption();
        var overlayOpt = new OverlayOption();
        var jsonOpt = CliOutput.CreateJsonOption();
        var cmd = new Command("derive", "Copy an existing entity under a new id the game does not ship.");
        cmd.Arguments.Add(srcArg);
        cmd.Options.Add(asOpt);
        cmd.Options.Add(classOpt);
        cmd.Options.Add(outOpt);
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(overlayOpt);
        cmd.Options.Add(jsonOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("entity.derive");
            bool json = pr.GetValue(jsonOpt);
            try
            {
                string newId = pr.GetValue(asOpt)!;
                string cls = pr.GetValue(classOpt)
                    ?? EntityId.ClassFor(newId)
                    ?? throw new ArgumentException($"Unknown entity class for id '{newId}', pass --class.");
                var result = new EntityDeriver(rootOpt.Locate(pr, overlayOpt))
                    .Derive(pr.GetValue(srcArg)!, newId, cls, pr.GetValue(outOpt)!);
                log.LogInformation("derived {New} from {Src}: {Count} file(s)", result.Id, result.SourceId, result.Files.Count);
                return CliOutput.Success(json, "entity.derive", result.Files);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "derive failed");
                return CliOutput.Failure(json, "entity.derive", ex.Message);
            }
        });
        return cmd;
    }
}
