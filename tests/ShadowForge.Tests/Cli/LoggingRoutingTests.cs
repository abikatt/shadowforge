using NLog;
using NLog.Targets;
using ShadowForge.CLI;
using MelLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ShadowForge.Tests.CLI;

/// <summary>
/// Checks the NLog configuration instead of capturing Console.Out, because
/// ColoredConsoleTarget can write to the OS console handle and bypass Console.SetOut.
/// </summary>
public sealed class LoggingRoutingTests
{
    [Fact]
    public void Build_AnyLogLevel_AllConsoleTargetsRouteToStdErr()
    {
        using var factory = LoggingSetup.Build(MelLevel.Trace, null);

        var config = LogManager.Configuration;
        Assert.NotNull(config);

        var consoleTargets = config!.AllTargets
            .Where(t => t is ColoredConsoleTarget or ConsoleTarget)
            .ToList();
        Assert.NotEmpty(consoleTargets);

        foreach (var target in consoleTargets)
        {
            bool stdErr = target switch
            {
                ColoredConsoleTarget colored => colored.StdErr,
                ConsoleTarget plain => plain.StdErr,
                _ => throw new InvalidOperationException($"unhandled console target type: {target.GetType()}"),
            };
            Assert.True(stdErr,
                $"console target '{target.Name}' writes to stdout, which is reserved for command results and --json envelopes");
        }
    }
}
