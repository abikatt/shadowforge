using System.CommandLine;
using Microsoft.Extensions.Logging;
using ShadowForge.CLI;
using ShadowForge.CLI.Commands;

var logLevelOpt = new Option<LogLevel>("--log-level")
{
    Description = "Minimum log level: Trace, Debug, Information (default), Warning, or Error.",
    DefaultValueFactory = _ => LogLevel.Information,
};
var logFileOpt = new Option<string?>("--log-file")
{
    Description = "Log file path (default: logs/sforge-{date}.log beside the executable).",
};

var root = new RootCommand("ShadowForge, the Blue Dragon modding toolkit.");
root.Options.Add(logLevelOpt);
root.Options.Add(logFileOpt);

ILoggerFactory? factory = null;
ILoggerFactory GetFactory(ParseResult parseResult) =>
    factory ??= LoggingSetup.Build(parseResult.GetValue(logLevelOpt), parseResult.GetValue(logFileOpt));

root.Subcommands.Add(RPJCommands.Create(GetFactory));
root.Subcommands.Add(IPKCommands.Create(GetFactory));
root.Subcommands.Add(HDBCommands.Create(GetFactory));
root.Subcommands.Add(MOTCommands.Create(GetFactory));
root.Subcommands.Add(EVTCommands.Create(GetFactory));
root.Subcommands.Add(DDSCommands.Create(GetFactory));
root.Subcommands.Add(XACTCommands.Create(GetFactory));
root.Subcommands.Add(MDLCommands.Create(GetFactory));
root.Subcommands.Add(MAPCommands.Create(GetFactory));
root.Subcommands.Add(MinimapCommands.Create(GetFactory));
root.Subcommands.Add(EntityCommands.Create(GetFactory));
root.Subcommands.Add(ModCommands.Create(GetFactory));

int exit = root.Parse(args).Invoke();
factory?.Dispose();
NLog.LogManager.Shutdown();
return exit;
