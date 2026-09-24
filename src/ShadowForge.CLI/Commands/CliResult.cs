using System.Text.Json.Serialization;

namespace ShadowForge.CLI.Commands;

public sealed record CliWarning(string Code, string Message);
public sealed record CliError(string Code, string Message);
public sealed record CliResult(
    bool Ok, string Verb, IReadOnlyList<string> Outputs,
    IReadOnlyList<CliWarning> Warnings, CliError? Error);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CliResult))]
public partial class CliResultJson : JsonSerializerContext
{
}
