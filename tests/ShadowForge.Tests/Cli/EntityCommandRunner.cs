using Microsoft.Extensions.Logging.Abstractions;
using ShadowForge.CLI.Commands;

namespace ShadowForge.Tests.CLI;

internal static class EntityCommandRunner
{
    /// <summary>
    /// Runs an entity subcommand in-process and returns its exit code and stdout.
    /// </summary>
    public static (int Code, string Out) Run(params string[] args)
    {
        var cmd = EntityCommands.Create(_ => NullLoggerFactory.Instance);
        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            int code = cmd.Parse(args).Invoke();
            return (code, sw.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    public static string MissingPath(string prefix) =>
        Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
}
