using System.CommandLine;
using Microsoft.Extensions.Logging;
using ShadowForge.GameData.Entities;
using ShadowForge.GameData.Mods;

namespace ShadowForge.CLI.Commands;

/// <summary>
/// Packaging edited entity files as a mod under mods\&lt;name&gt;.
/// </summary>
internal static class EntityModCommands
{
    public static Command BuildPack(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var idArg = EntityCommands.IdArgument();
        var fromOpt = new Option<string?>("--from") { Description = "Directory of edited assets, required." };
        var mod = new ModOptions();
        var rootOpt = new GameRootOption();
        var overlayOpt = new OverlayOption();
        var jsonOpt = CliOutput.CreateJsonOption();
        var cmd = new Command("pack", "Deploy edited assets as a mod override tree under mods\\<name>.");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(fromOpt);
        cmd.Options.Add(mod.Name);
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(overlayOpt);
        cmd.Options.Add(mod.Author);
        cmd.Options.Add(mod.Version);
        cmd.Options.Add(mod.Description);
        cmd.Options.Add(mod.Enable);
        cmd.Options.Add(jsonOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("entity.pack");
            bool json = pr.GetValue(jsonOpt);
            string? from = pr.GetValue(fromOpt);
            string? modName = pr.GetValue(mod.Name);
            if (string.IsNullOrWhiteSpace(from))
            {
                log.LogError("--from <dir> is required.");
                return 1;
            }
            if (string.IsNullOrWhiteSpace(modName))
            {
                log.LogError("--mod <name> is required.");
                return 1;
            }
            try
            {
                var install = rootOpt.Locate(pr, overlayOpt);
                var plan = new EntityPacker(install).BuildPlan(pr.GetValue(idArg)!, from);
                var result = new ModDeployer(install).Deploy(plan, modName, mod.Metadata(pr, modName));
                if (!json)
                    Console.WriteLine($"Deployed {result.FilesWritten} file(s) for {plan.EntityId} to {result.ModDir}");
                var warnings = SkipWarnings(plan, json);
                bool sharedRig = !plan.RigId.Equals(plan.EntityId, StringComparison.OrdinalIgnoreCase)
                    && plan.Entries.Any(e => e.OverrideRelPath.StartsWith(@"chara\", StringComparison.OrdinalIgnoreCase));
                if (sharedRig && !json)
                    Console.WriteLine(
                        $"warning: {plan.EntityId} shares rig '{plan.RigId}', so rig assets under " +
                        $"chara\\...\\{plan.RigId}\\ override that rig for every entity that uses it, not only {plan.EntityId}");
                if (pr.GetValue(mod.Enable))
                    ModOrderEdit.Apply(install, modName, enable: true, quiet: json);
                return json ? CliOutput.Success(json, "entity.pack", [result.ModDir], warnings) : 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "pack failed");
                return CliOutput.Failure(json, "entity.pack", ex.Message);
            }
        });
        return cmd;
    }

    public static Command BuildDeploy(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var idArg = EntityCommands.IdArgument();
        var fromOpt = new Option<string?>("--from") { Description = "Directory holding the edited <id>.glb, required." };
        var mod = new ModOptions();
        var jsonOpt = CliOutput.CreateJsonOption();
        var rootOpt = new GameRootOption();
        var overlayOpt = new OverlayOption();
        var cmd = new Command("deploy", "Cook, pack, and deploy an edited entity as a mod in one step.");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(fromOpt);
        cmd.Options.Add(mod.Name);
        cmd.Options.Add(mod.Enable);
        cmd.Options.Add(jsonOpt);
        cmd.Options.Add(mod.Author);
        cmd.Options.Add(mod.Version);
        cmd.Options.Add(mod.Description);
        cmd.Options.Add(rootOpt);
        cmd.Options.Add(overlayOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("entity.deploy");
            bool json = pr.GetValue(jsonOpt);
            try
            {
                string idOrPath = pr.GetValue(idArg)!;
                string from = EntityCommands.RequireFromDir(pr.GetValue(fromOpt));
                string modName = pr.GetValue(mod.Name) is { } n && !string.IsNullOrWhiteSpace(n)
                    ? n
                    : throw new ArgumentException("--mod <name> is required.");

                var install = rootOpt.Locate(pr, overlayOpt);
                var entity = new EntityResolver(install).Resolve(idOrPath);
                string glb = EntityCooker.FindEditedModel(from, entity.Id);

                using var cooked = new TempDir("cook");
                var cookWarnings = new EntityCooker(install).Cook(entity, glb, cooked.FullName, log).Warnings;
                var plan = new EntityPacker(install).BuildPlan(idOrPath, cooked.FullName);
                var skipWarnings = SkipWarnings(plan, json);
                var deploy = new ModDeployer(install).Deploy(plan, modName, mod.Metadata(pr, modName));
                if (pr.GetValue(mod.Enable))
                    ModOrderEdit.Apply(install, modName, enable: true, quiet: json);

                var warnings = cookWarnings.Select(w => new CliWarning("deploy", w)).Concat(skipWarnings).ToList();
                if (json)
                    return CliOutput.Success(json, "entity.deploy", [deploy.ModDir], warnings);
                Console.WriteLine($"Deployed {deploy.FilesWritten} file(s) for {entity.Id} to {deploy.ModDir}");
                foreach (string w in cookWarnings) Console.WriteLine("warning: " + w);
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "deploy failed");
                return CliOutput.Failure(json, "entity.deploy", ex.Message);
            }
        });
        return cmd;
    }

    /// <summary>
    /// Plan entries that are not game files, printed unless <paramref name="json"/> is set.
    /// </summary>
    private static List<CliWarning> SkipWarnings(PackPlan plan, bool json)
    {
        if (!json)
        {
            foreach (string s in plan.Skipped)
                Console.WriteLine($"skipped (not a game file): {s}");
        }
        return plan.Skipped.Select(s => new CliWarning("skipped", $"not a game file: {s}")).ToList();
    }

    private sealed class ModOptions
    {
        public Option<string?> Name { get; } = new("--mod") { Description = "Mod name, the folder under mods\\, required." };
        public Option<string?> Author { get; } = new("--author") { Description = "mod.toml author." };
        public Option<string?> Version { get; } = new("--mod-version") { Description = "mod.toml version." };
        public Option<string?> Description { get; } = new("--desc") { Description = "mod.toml description." };
        public Option<bool> Enable { get; } = new("--enable") { Description = "Add the mod to mod_order.txt after the deploy." };

        public ModMetadata Metadata(ParseResult pr, string modName) =>
            new(modName, pr.GetValue(Author), pr.GetValue(Version), pr.GetValue(Description));
    }
}
