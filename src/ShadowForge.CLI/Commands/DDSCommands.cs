using System.CommandLine;
using Microsoft.Extensions.Logging;
using ShadowForge.Formats.DDS;

namespace ShadowForge.CLI.Commands;

public static class DDSCommands
{
    public static Command Create(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var cmd = new Command("dds", "Xbox 360 DDS texture operations.");
        cmd.Subcommands.Add(BuildExportCommand(getFactory));
        cmd.Subcommands.Add(BuildBatchExportCommand(getFactory));
        cmd.Subcommands.Add(BuildImportCommand(getFactory));
        cmd.Subcommands.Add(BuildBatchImportCommand(getFactory));
        cmd.Subcommands.Add(BuildImportVolumeCommand(getFactory));
        return cmd;
    }

    private static Option<bool> NoSwapOption() =>
        new("--no-swap") { Description = "Disable the global 2-byte endian swap." };

    private static Command BuildExportCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<string>("file-or-id") { Description = "Xbox 360 DDS file, or an entity id (pc01) for all its textures." };
        var outputOpt = new Option<FileInfo?>("-o") { Description = "Output .png file, or output directory for an entity id." };
        var noSwapOpt = NoSwapOption();
        var rootOpt = new GameRootOption();
        var cmd = new Command("export", "Export an Xbox 360 DDS texture to PNG.");
        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(outputOpt);
        cmd.Options.Add(noSwapOpt);
        cmd.Options.Add(rootOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("dds.export");
            string arg = pr.GetValue(fileArg)!;
            var outOpt = pr.GetValue(outputOpt);
            bool swap = !pr.GetValue(noSwapOpt);
            try
            {
                using var tex = EntityArg.Textures(pr.GetValue(rootOpt), arg);
                if (!tex.FromEntity)
                {
                    string output = OutputPath.ForFile(outOpt, tex.Path, ".png");
                    log.LogInformation("Reading and converting {File}...", Path.GetFileName(tex.Path));
                    int slicesSaved = Converter.ConvertAndSave(tex.Path, output, swap);
                    Console.WriteLine(slicesSaved == 1
                        ? $"Done. Saved to {Path.GetFileName(output)}."
                        : $"Done. Saved {slicesSaved} slices based on {Path.GetFileName(output)}.");
                    return 0;
                }

                string outDir = outOpt?.FullName ?? Environment.CurrentDirectory;
                if (File.Exists(outDir))
                    throw new IOException($"-o must be a directory when exporting all of {tex.Stem}'s textures, '{outDir}' is a file.");
                Directory.CreateDirectory(outDir);
                return BatchConvert.Run(tex.Paths, $"exported to {outDir}", log, src =>
                {
                    Converter.ConvertAndSave(src, Path.Combine(outDir, Path.ChangeExtension(Path.GetFileName(src), ".png")), swap);
                    return null;
                });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Export failed for {File}", arg);
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildBatchExportCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var dirArg = new Argument<DirectoryInfo>("directory") { Description = "Directory containing .dds and .36t files." };
        var outputOpt = new Option<DirectoryInfo?>("-o") { Description = "Output directory." };
        var noSwapOpt = NoSwapOption();
        var cmd = new Command("batch-export", "Export every Xbox 360 DDS file in a directory to PNG.");
        cmd.Arguments.Add(dirArg);
        cmd.Options.Add(outputOpt);
        cmd.Options.Add(noSwapOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("dds.batch-export");
            var dir = pr.GetValue(dirArg)!;
            var outputDir = pr.GetValue(outputOpt) ?? dir;
            bool swap = !pr.GetValue(noSwapOpt);
            return BatchConvert.Run(BatchConvert.Find(dir, "*.dds", "*.36t"), "exported", log, file =>
            {
                Converter.ConvertAndSave(file, BatchConvert.MirrorPath(file, dir, outputDir, ".png"), swap);
                return null;
            });
        });
        return cmd;
    }

    private static Command BuildImportCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "Input PNG file." };
        var refOpt = new Option<FileInfo?>("-r") { Description = "Reference DDS file whose header is kept." };
        var outputOpt = new Option<FileInfo?>("-o") { Description = "Output .dds file." };
        var formatOpt = new Option<string?>("--format") { Description = "Texture format (DXT1, DXT3, DXT5, DxN, A8R8G8B8, and others)." };
        var cmd = new Command("import", "Import a PNG into an Xbox 360 DDS texture.");
        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(refOpt);
        cmd.Options.Add(outputOpt);
        cmd.Options.Add(formatOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("dds.import");
            var file = pr.GetValue(fileArg)!;
            var reference = pr.GetValue(refOpt);
            string? formatStr = pr.GetValue(formatOpt);

            GraphicFormat? formatOverride = null;
            if (formatStr is not null)
            {
                if (!GraphicFormatParser.TryParse(formatStr, out var parsed))
                {
                    log.LogError("Unknown format: {Format}", formatStr);
                    return 1;
                }
                formatOverride = parsed;
            }
            if (reference is null && formatOverride is null)
            {
                log.LogError("Pass -r <reference.dds> or --format.");
                return 1;
            }

            try
            {
                string output = OutputPath.ForFile(pr.GetValue(outputOpt), file.FullName, ".dds");
                if (reference is not null)
                {
                    log.LogInformation("Importing {File} using reference {Ref}...", file.Name, reference.Name);
                    Importer.ImportWithReference(file.FullName, reference.FullName, output, formatOverride);
                }
                else
                {
                    log.LogInformation("Importing {File} as {Format}...", file.Name, formatStr);
                    Importer.Import(file.FullName, output, formatOverride!.Value);
                }
                Console.WriteLine($"Done. Saved to {Path.GetFileName(output)}.");
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Import failed for {File}", file.FullName);
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildBatchImportCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var dirArg = new Argument<DirectoryInfo>("directory") { Description = "Directory containing .png files." };
        var refDirOpt = new Option<DirectoryInfo?>("-r") { Description = "Directory containing reference .dds files, required." };
        var outputOpt = new Option<DirectoryInfo?>("-o") { Description = "Output directory." };
        var cmd = new Command("batch-import", "Import every PNG file in a directory to Xbox 360 DDS.");
        cmd.Arguments.Add(dirArg);
        cmd.Options.Add(refDirOpt);
        cmd.Options.Add(outputOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("dds.batch-import");
            var dir = pr.GetValue(dirArg)!;
            var refDir = pr.GetValue(refDirOpt);
            if (refDir is null)
            {
                log.LogError("-r <reference dir> is required.");
                return 1;
            }
            var outputDir = pr.GetValue(outputOpt) ?? dir;
            return BatchConvert.Run(BatchConvert.Find(dir, "*.png"), "imported", log, png =>
            {
                string refPath = Path.Combine(refDir.FullName, Path.GetFileNameWithoutExtension(png) + ".dds");
                if (!File.Exists(refPath))
                    return "no matching reference DDS";
                Importer.ImportWithReference(png, refPath, BatchConvert.MirrorPath(png, dir, outputDir, ".dds"));
                return null;
            });
        });
        return cmd;
    }

    private static Command BuildImportVolumeCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var refArg = new Argument<FileInfo>("reference") { Description = "Reference .36t file that supplies header, format, and dimensions." };
        var slicesOpt = new Option<DirectoryInfo>("-s", "--slices") { Description = "Directory containing the PNG slices (default: current directory)." };
        var outputOpt = new Option<FileInfo?>("-o") { Description = "Output .36t file." };
        var prefixOpt = new Option<string?>("--prefix") { Description = "Slice file prefix, 'tex' for tex_slice_000.png (default: reference file name)." };
        var cmd = new Command("import-volume", "Import PNG slices into an Xbox 360 .36t volume texture.");
        cmd.Arguments.Add(refArg);
        cmd.Options.Add(slicesOpt);
        cmd.Options.Add(outputOpt);
        cmd.Options.Add(prefixOpt);
        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("dds.import-volume");
            var reference = pr.GetValue(refArg)!;
            var slicesDir = pr.GetValue(slicesOpt) ?? new DirectoryInfo(Environment.CurrentDirectory);
            string prefix = pr.GetValue(prefixOpt) ?? Path.GetFileNameWithoutExtension(reference.Name);
            try
            {
                if (!reference.Exists)
                {
                    log.LogError("Reference file {Ref} does not exist.", reference.FullName);
                    return 1;
                }
                if (!slicesDir.Exists)
                {
                    log.LogError("Slices directory {Dir} does not exist.", slicesDir.FullName);
                    return 1;
                }

                var sliceFiles = slicesDir.GetFiles($"{prefix}_slice_*.png")
                    .Where(f => !f.Name.EndsWith("_RGB.png", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.Name)
                    .Select(f => f.FullName)
                    .ToArray();
                if (sliceFiles.Length == 0)
                {
                    log.LogError("No slices matching prefix '{Prefix}' in '{Dir}'.", prefix, slicesDir.FullName);
                    return 1;
                }

                string output = OutputPath.ForFile(pr.GetValue(outputOpt), reference.FullName, ".new.36t");
                log.LogInformation("Importing {Count} slices using reference {Ref}...", sliceFiles.Length, reference.Name);
                Importer.ImportVolumeWithReference(sliceFiles, reference.FullName, output);
                Console.WriteLine($"Done. Saved to {Path.GetFileName(output)}.");
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Volume import failed for {File}", reference.FullName);
                return 1;
            }
        });
        return cmd;
    }
}
