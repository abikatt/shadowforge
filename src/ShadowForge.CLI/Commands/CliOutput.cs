using System.CommandLine;
using System.Text.Json;

namespace ShadowForge.CLI.Commands;

/// <summary>
/// Final result reporting for commands that take --json. With --json, stdout carries exactly
/// one <see cref="CliResult"/> envelope and nothing else.
/// </summary>
internal static class CliOutput
{
    public static Option<bool> CreateJsonOption() =>
        new("--json") { Description = "Print a machine-readable JSON result on stdout." };

    /// <summary>
    /// Prints the success envelope, or <paramref name="summary"/> and one line per warning
    /// without --json. Returns exit code 0.
    /// </summary>
    public static int Success(
        bool json, string verb, IReadOnlyList<string> outputs,
        IReadOnlyList<CliWarning>? warnings = null, string? summary = null)
    {
        warnings ??= [];
        if (json)
            return Emit(new CliResult(true, verb, outputs, warnings, null));
        if (summary is not null)
            Console.WriteLine(summary);
        foreach (var w in warnings)
            Console.WriteLine("warning: " + w.Message);
        return 0;
    }

    /// <summary>
    /// Prints the failure envelope with --json. The error code is the last segment of
    /// <paramref name="verb"/>. Returns exit code 1.
    /// </summary>
    public static int Failure(
        bool json, string verb, string message,
        IReadOnlyList<string>? outputs = null, IReadOnlyList<CliWarning>? warnings = null)
    {
        if (!json)
            return 1;
        string code = verb[(verb.LastIndexOf('.') + 1)..];
        return Emit(new CliResult(false, verb, outputs ?? [], warnings ?? [], new CliError(code, message)));
    }

    private static int Emit(CliResult result)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, CliResultJson.Default.CliResult));
        return result.Ok ? 0 : 1;
    }
}
