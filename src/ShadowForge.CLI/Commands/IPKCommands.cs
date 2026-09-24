using System.CommandLine;
using Microsoft.Extensions.Logging;
using ShadowForge.Formats.IPK;

namespace ShadowForge.CLI.Commands;

public static class IPKCommands
{
    public static Command Create(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var cmd = new Command("ipk", "IPK archive operations.");
        cmd.Subcommands.Add(BuildListCommand(getFactory));
        cmd.Subcommands.Add(BuildExtractCommand(getFactory));
        return cmd;
    }

    private static Command BuildListCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "IPK archive file." };
        var cmd = new Command("list", "List the contents of an IPK archive.");
        cmd.Arguments.Add(fileArg);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("ipk.list");
            var file = pr.GetValue(fileArg)!;
            try
            {
                using var fs = File.OpenRead(file.FullName);
                var archive = ArchiveReader.ReadArchive(fs);

                Console.WriteLine($"{archive.FileCount} files, {archive.ArchiveSize} bytes total");
                Console.WriteLine($"Compression: {(archive.UsesZlib ? "zlib" : "LZSS")}");
                Console.WriteLine();

                foreach (var entry in archive.Entries)
                {
                    string comp = entry.IsCompressed
                        ? $"compressed ({entry.CompressedSize}/{entry.OriginalSize})"
                        : $"stored ({entry.OriginalSize})";
                    Console.WriteLine($"  {entry.Name}  {comp}");
                }
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to read IPK {File}", file.FullName);
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildExtractCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "IPK archive file." };
        var outputOpt = new Option<DirectoryInfo?>("-o") { Description = "Output directory (default: a folder named after the archive beside it)." };
        var cmd = new Command("extract", "Extract every file from an IPK archive.");
        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(outputOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("ipk.extract");
            var file = pr.GetValue(fileArg)!;
            var outputDir = pr.GetValue(outputOpt)
                ?? new DirectoryInfo(Path.Combine(
                    Path.GetDirectoryName(file.FullName) ?? ".",
                    Path.GetFileNameWithoutExtension(file.FullName)));

            try
            {
                using var fs = File.OpenRead(file.FullName);
                var reader = new ArchiveReader(fs);
                reader.ExtractAll(outputDir.FullName);
                Console.WriteLine($"Extracted {reader.Archive.FileCount} files to {outputDir.FullName}");
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to extract IPK {File}", file.FullName);
                return 1;
            }
        });
        return cmd;
    }
}
