using Microsoft.Extensions.Logging;

namespace ShadowForge.CLI.Commands;

internal static class BatchConvert
{
    /// <summary>
    /// Every file under <paramref name="inputDir"/> matching any pattern, recursively, sorted.
    /// </summary>
    public static IReadOnlyList<string> Find(DirectoryInfo inputDir, params string[] patterns) =>
        patterns
            .SelectMany(p => Directory.EnumerateFiles(inputDir.FullName, p, SearchOption.AllDirectories))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Maps <paramref name="input"/> under <paramref name="inputDir"/> to the same relative
    /// path under <paramref name="outputDir"/> with <paramref name="extension"/>, creating
    /// its directory.
    /// </summary>
    public static string MirrorPath(string input, DirectoryInfo inputDir, DirectoryInfo outputDir, string extension)
    {
        string rel = Path.GetRelativePath(inputDir.FullName, input);
        string outPath = Path.Combine(outputDir.FullName, Path.ChangeExtension(rel, extension));
        OutputPath.CreateParentDirectory(outPath);
        return outPath;
    }

    /// <summary>
    /// Runs <paramref name="convert"/> on each input, printing one OK, SKIP or FAIL line per
    /// file and a summary. <paramref name="convert"/> returns a skip reason, or null when it
    /// converted the file. Returns exit code 1 when any file failed.
    /// </summary>
    public static int Run(IReadOnlyList<string> inputs, string doneVerb, ILogger log, Func<string, string?> convert)
    {
        Console.WriteLine($"Found {inputs.Count} file(s).");
        int ok = 0, fail = 0, skipped = 0;
        foreach (string input in inputs)
        {
            string name = Path.GetFileName(input);
            try
            {
                if (convert(input) is { } reason)
                {
                    Console.WriteLine($"  SKIP: {name} - {reason}");
                    skipped++;
                    continue;
                }
                Console.WriteLine($"  OK: {name}");
                ok++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: {name} - {ex.Message}");
                log.LogWarning(ex, "Failed to convert {File}", input);
                fail++;
            }
        }
        string skipText = skipped > 0 ? $", {skipped} skipped" : "";
        Console.WriteLine($"Done. {ok} {doneVerb}, {fail} failed{skipText}.");
        return fail > 0 ? 1 : 0;
    }
}
