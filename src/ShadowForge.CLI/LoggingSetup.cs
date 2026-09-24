using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLogLevel = NLog.LogLevel;
using MelLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ShadowForge.CLI;

/// <summary>
/// NLog-backed logging. Every console target writes to stderr, because stdout carries command
/// results such as the --json envelope. The file target rolls daily at
/// logs/sforge-{shortdate}.log beside the executable.
/// </summary>
public static class LoggingSetup
{
    public static ILoggerFactory Build(MelLevel minLevel, string? logFileOverride)
    {
        var config = new LoggingConfiguration();

        var infoConsole = new ColoredConsoleTarget("console-info")
        {
            Layout = "${message}${onexception:inner= ${exception:format=tostring}}",
            StdErr = true,
        };
        var warnConsole = new ColoredConsoleTarget("console-warn")
        {
            Layout = "${level:uppercase=true:padding=-5} ${message}"
                   + "${onexception:inner= ${exception:format=tostring}}",
            StdErr = true,
        };

        config.AddRule(ToNLog(minLevel), NLogLevel.Info,  infoConsole);
        config.AddRule(NLogLevel.Warn,   NLogLevel.Fatal, warnConsole);

        var file = new FileTarget("file")
        {
            FileName = logFileOverride
                ?? "${basedir}/logs/sforge-${shortdate}.log",
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 14,
            ArchiveAboveSize = 10 * 1024 * 1024,
            Layout = "${longdate} ${level:uppercase=true:padding=-5} "
                   + "${logger:shortName=true} ${message}"
                   + "${onexception:inner= ${exception:format=tostring}}",
        };
        config.AddRule(ToNLog(minLevel), NLogLevel.Fatal, file);

        LogManager.Configuration = config;

        return LoggerFactory.Create(b => b
            .SetMinimumLevel(minLevel)
            .AddNLog());
    }

    private static NLogLevel ToNLog(MelLevel level) => level switch
    {
        MelLevel.Trace       => NLogLevel.Trace,
        MelLevel.Debug       => NLogLevel.Debug,
        MelLevel.Information => NLogLevel.Info,
        MelLevel.Warning     => NLogLevel.Warn,
        MelLevel.Error       => NLogLevel.Error,
        MelLevel.Critical    => NLogLevel.Fatal,
        MelLevel.None        => NLogLevel.Off,
        _                    => NLogLevel.Info,
    };
}
