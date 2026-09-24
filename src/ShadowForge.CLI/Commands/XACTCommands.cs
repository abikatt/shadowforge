using System.CommandLine;
using Microsoft.Extensions.Logging;
using ShadowForge.Formats.XACT;

namespace ShadowForge.CLI.Commands;

public static class XACTCommands
{
    public static Command Create(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var cmd = new Command("xact", "Xbox 360 XACT sound bank (.xsb) and wave bank (.xwb) operations.");
        cmd.Subcommands.Add(BuildCuesCommand(getFactory));
        cmd.Subcommands.Add(BuildWavesCommand(getFactory));
        cmd.Subcommands.Add(BuildExtractCommand(getFactory));
        cmd.Subcommands.Add(BuildReplaceCommand(getFactory));
        cmd.Subcommands.Add(BuildSeCommand(getFactory));
        return cmd;
    }

    private static Command BuildCuesCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "Sound bank (.xsb)." };

        var cmd = new Command("cues", "List the cues in a sound bank.");
        cmd.Arguments.Add(fileArg);

        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("xact.cues");
            var file = pr.GetValue(fileArg)!;
            try
            {
                var bank = SoundBank.ReadFile(file.FullName);
                Console.WriteLine($"{bank.Name}: {bank.Cues.Count} cues, tool version {bank.ToolVersion}");
                Console.WriteLine($"Wave banks: {string.Join(", ", bank.WaveBankNames)}");
                if (bank.ComplexCueCount != 0)
                    Console.WriteLine($"warning: {bank.ComplexCueCount} complex cues are not decoded");
                Console.WriteLine();
                Console.WriteLine("  idx  cue                   wave  bank");
                foreach (var cue in bank.Cues)
                    Console.WriteLine($"  {cue.Index,3}  {cue.Name,-20}  {cue.WaveIndex,4}  {cue.WaveBankIndex,4}");
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to read sound bank {File}", file.FullName);
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildWavesCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "Wave bank (.xwb)." };

        var cmd = new Command("waves", "List the wave entries in a wave bank.");
        cmd.Arguments.Add(fileArg);

        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("xact.waves");
            var file = pr.GetValue(fileArg)!;
            try
            {
                var bank = WaveBank.ReadFile(file.FullName);
                Console.WriteLine(
                    $"{bank.Name}: {bank.Entries.Count} entries, version {bank.Version}, alignment {bank.Alignment}");
                Console.WriteLine();
                Console.WriteLine("  idx  codec  ch     rate    samples      bytes");
                for (int i = 0; i < bank.Entries.Count; i++)
                {
                    var entry = bank.Entries[i];
                    Console.WriteLine(
                        $"  {i,3}  {entry.Format.Tag,-5}  {entry.Format.Channels,2}  {entry.Format.SamplesPerSecond,7}" +
                        $"  {entry.DurationSamples,9}  {entry.Data.Length,9}");
                }
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to read wave bank {File}", file.FullName);
                return 1;
            }
        });
        return cmd;
    }

    private static Command BuildExtractCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "Wave bank (.xwb)." };
        var indexOpt = new Option<int?>("--index") { Description = "Wave index to extract (default: every entry)." };
        var outputOpt = new Option<string?>("-o") { Description = "Output .wav file for a single index, or output directory." };
        var jsonOpt = CliOutput.CreateJsonOption();

        var cmd = new Command("extract", "Extract waves from a wave bank to .wav (PCM) or raw .xma (XMA).");
        cmd.Arguments.Add(fileArg);
        cmd.Options.Add(indexOpt);
        cmd.Options.Add(outputOpt);
        cmd.Options.Add(jsonOpt);

        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("xact.extract");
            var file = pr.GetValue(fileArg)!;
            int? index = pr.GetValue(indexOpt);
            string? outValue = pr.GetValue(outputOpt);
            bool json = pr.GetValue(jsonOpt);

            var outputs = new List<string>();
            var warnings = new List<CliWarning>();
            try
            {
                var bank = WaveBank.ReadFile(file.FullName);
                if (index is { } single && (single < 0 || single >= bank.Entries.Count))
                    throw new ArgumentOutOfRangeException(nameof(index), single,
                        $"Wave index must be 0-{bank.Entries.Count - 1}.");

                string stem = Path.GetFileNameWithoutExtension(file.Name);
                string sourceDir = Path.GetDirectoryName(file.FullName) ?? ".";
                bool singleFileOut = index is not null && outValue is { Length: > 0 } && !OutputPath.NamesDirectory(outValue);

                for (int i = 0; i < bank.Entries.Count; i++)
                {
                    if (index is { } only && i != only) continue;
                    var entry = bank.Entries[i];
                    bool pcm = entry.Format.Tag == WaveFormatTag.PCM;
                    string extension = pcm ? ".wav" : ".xma";

                    string outPath = singleFileOut
                        ? Path.ChangeExtension(outValue!, extension)
                        : Path.Combine(outValue ?? sourceDir, $"{stem}_{i:D3}{extension}");

                    OutputPath.CreateParentDirectory(outPath);
                    if (pcm)
                    {
                        var wav = WavFile.FromBigEndianPcm(
                            entry.Data, entry.Format.Channels, entry.Format.SamplesPerSecond);
                        wav.WriteFile(outPath);
                    }
                    else
                    {
                        File.WriteAllBytes(outPath, entry.Data);
                        string message =
                            $"entry {i} is {entry.Format.Tag}, written raw to {Path.GetFileName(outPath)} without decoding";
                        warnings.Add(new CliWarning("not-decoded", message));
                        if (!json) Console.WriteLine($"  warning: {message}");
                    }
                    outputs.Add(outPath);
                    if (!json) Console.WriteLine($"  OK: {Path.GetFileName(outPath)}");
                }

                return CliOutput.Success(json, "xact.extract", outputs, json ? warnings : null,
                    $"Done. {outputs.Count} wave(s) extracted.");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Extract failed for {File}", file.FullName);
                return CliOutput.Failure(json, "xact.extract", ex.Message, outputs, warnings);
            }
        });
        return cmd;
    }

    private static Command BuildReplaceCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "Wave bank (.xwb)." };
        var wavArg = new Argument<FileInfo>("wav") { Description = "Replacement 16-bit PCM .wav file." };
        var indexOpt = new Option<int?>("--index") { Description = "Wave index to replace." };
        var cueOpt = new Option<string?>("--cue") { Description = "Cue name to replace, looked up in the sound bank." };
        var soundBankOpt = new Option<FileInfo?>("--sound-bank")
            { Description = "Sound bank (.xsb) for --cue (default: the .xsb beside the wave bank)." };
        var outputOpt = new Option<string?>("-o") { Description = "Output .xwb file (default: <file>.new.xwb)." };
        var jsonOpt = CliOutput.CreateJsonOption();

        var cmd = new Command("replace", "Replace one wave in a wave bank with 16-bit PCM audio.");
        cmd.Arguments.Add(fileArg);
        cmd.Arguments.Add(wavArg);
        cmd.Options.Add(indexOpt);
        cmd.Options.Add(cueOpt);
        cmd.Options.Add(soundBankOpt);
        cmd.Options.Add(outputOpt);
        cmd.Options.Add(jsonOpt);

        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("xact.replace");
            var file = pr.GetValue(fileArg)!;
            var wavFile = pr.GetValue(wavArg)!;
            int? index = pr.GetValue(indexOpt);
            string? cueName = pr.GetValue(cueOpt);
            var soundBankFile = pr.GetValue(soundBankOpt);
            bool json = pr.GetValue(jsonOpt);

            string output = pr.GetValue(outputOpt) is { Length: > 0 } o
                ? OutputPath.Resolve(o, Path.GetFileNameWithoutExtension(file.Name), ".xwb")
                : Path.ChangeExtension(file.FullName, ".new.xwb");

            var warnings = new List<CliWarning>();
            try
            {
                if ((index is null) == (cueName is null))
                    throw new ArgumentException("Give exactly one of --index or --cue.");

                var bank = WaveBank.ReadFile(file.FullName);

                int target;
                if (index is { } explicitIndex)
                {
                    target = explicitIndex;
                }
                else
                {
                    string xsbPath = soundBankFile?.FullName ?? Path.ChangeExtension(file.FullName, ".xsb");
                    if (!File.Exists(xsbPath))
                        throw new FileNotFoundException(
                            "No sound bank found for --cue, pass --sound-bank.", xsbPath);
                    var soundBank = SoundBank.ReadFile(xsbPath);
                    var cue = soundBank.FindCue(cueName!)
                        ?? throw new ArgumentException($"No cue named '{cueName}' in {Path.GetFileName(xsbPath)}.");
                    if (cue.WaveBankIndex != 0)
                        warnings.Add(new CliWarning("wave-bank",
                            $"cue '{cue.Name}' names wave bank {cue.WaveBankIndex}, not the one being written"));
                    target = cue.WaveIndex;
                }

                if (target < 0 || target >= bank.Entries.Count)
                    throw new ArgumentOutOfRangeException(nameof(index), target,
                        $"Wave index must be 0-{bank.Entries.Count - 1}.");

                var replaced = bank.Entries[target];
                if (replaced.Format.Tag != WaveFormatTag.PCM)
                    warnings.Add(new CliWarning("codec-change",
                        $"entry {target} was {replaced.Format.Tag} and becomes PCM, its seek table entry is unchanged"));

                var wav = WavFile.ReadFile(wavFile.FullName);
                bank.ReplaceEntry(target, wav);

                OutputPath.CreateParentDirectory(output);
                bank.WriteFile(output);

                return CliOutput.Success(json, "xact.replace", [output], warnings,
                    $"Replaced wave {target} with {wav.FrameCount} samples " +
                    $"({wav.Channels}ch {wav.SampleRate}Hz). Saved to {Path.GetFileName(output)}.");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Replace failed for {File}", file.FullName);
                return CliOutput.Failure(json, "xact.replace", ex.Message, warnings: warnings);
            }
        });
        return cmd;
    }

    private static Command BuildSeCommand(Func<ParseResult, ILoggerFactory> getFactory)
    {
        var seArg = new Argument<string>("id-or-name") { Description = "SE id (947) or cue name (se_preb094)." };
        var selistOpt = new Option<FileInfo?>("--selist") { Description = "selist.csv path (default: the one under the game-data root)." };
        var rootOpt = new GameRootOption();

        var cmd = new Command("se", "Look up a sound effect in selist.csv.");
        cmd.Arguments.Add(seArg);
        cmd.Options.Add(selistOpt);
        cmd.Options.Add(rootOpt);

        cmd.SetAction(pr =>
        {
            var log = getFactory(pr).CreateLogger("xact.se");
            string query = pr.GetValue(seArg)!;
            var selistFile = pr.GetValue(selistOpt);
            try
            {
                string? gameRoot = null;
                string selistPath;
                if (selistFile is not null)
                {
                    selistPath = selistFile.FullName;
                }
                else
                {
                    gameRoot = rootOpt.Locate(pr).GameDataRoot;
                    selistPath = SeList.DefaultPath(gameRoot);
                }

                var list = SeList.ReadFile(selistPath);
                IReadOnlyList<SeEntry> matches;
                if (int.TryParse(query, out int id))
                {
                    var found = list.Find(id);
                    matches = found is null ? [] : [found];
                }
                else
                {
                    matches = list.FindByCue(query);
                }

                if (matches.Count == 0)
                {
                    Console.WriteLine($"No sound effect matching '{query}' in {Path.GetFileName(selistPath)}.");
                    return 1;
                }

                foreach (var entry in matches)
                {
                    Console.WriteLine($"SE {entry.Id}  {entry.Cue}");
                    Console.WriteLine($"  bank       {entry.Bank}");
                    Console.WriteLine($"  residency  {(entry.IsStreaming ? "STREAMING" : "INMEMORY")}");
                    Console.WriteLine($"  streaming  {entry.IsStreaming}");
                    Console.WriteLine($"  reverb     {(entry.Reverb.Length == 0 ? "(none)" : entry.Reverb)}");
                    Console.WriteLine($"  sound bank {entry.SoundBankRelativePath}");
                    Console.WriteLine($"  wave bank  {entry.WaveBankRelativePath}");
                    if (gameRoot is not null)
                    {
                        string? xsb = entry.ResolveBankPath(gameRoot, ".xsb");
                        string? xwb = entry.ResolveBankPath(gameRoot, ".xwb");
                        Console.WriteLine($"  resolved   {xsb ?? "(not installed)"}");
                        Console.WriteLine($"             {xwb ?? "(not installed)"}");
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "SE lookup failed for {Query}", query);
                return 1;
            }
        });
        return cmd;
    }
}
