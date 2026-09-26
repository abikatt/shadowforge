using System.Diagnostics;
using System.Runtime.InteropServices;
using ShadowForge.Formats.XACT;

namespace ShadowForge.Workbench;

/// <summary>
/// Decoding, converting and playing waves. Nearly every retail wave is XMA, which only an
/// external decoder can read, so XMA work goes through FFmpeg; the few PCM waves need nothing.
/// </summary>
public static class AudioTools
{
    /// <summary>
    /// The FFmpeg to use: the saved path if it still exists, else ffmpeg.exe on PATH, else the
    /// common C:\ffmpeg\bin install.
    /// </summary>
    public static string? FindFfmpeg(string? saved)
    {
        if (saved is { Length: > 0 } && File.Exists(saved)) return saved;
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim(), "ffmpeg.exe");
                if (dir.Length > 0 && File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
            }
        }
        const string common = @"C:\ffmpeg\bin\ffmpeg.exe";
        return File.Exists(common) ? common : null;
    }

    /// <summary>
    /// The names the Xbox 360 SDK's XMA encoder ships under.
    /// </summary>
    public static readonly string[] XmaEncoderNames = ["xmaencode2008.exe", "xmaencode.exe"];

    /// <summary>
    /// The XMA encoder to use: the saved path if it still exists, else one beside ShadowForge
    /// or in its tools folder, else one on PATH. It is part of the Xbox 360 SDK, so it cannot
    /// ship with ShadowForge and the user points to their own copy.
    /// </summary>
    public static string? FindXmaEncoder(string? saved)
    {
        if (saved is { Length: > 0 } && File.Exists(saved)) return saved;
        var dirs = new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "tools") }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (string dir in dirs)
        {
            foreach (string name in XmaEncoderNames)
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), name);
                    if (dir.Length > 0 && File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException)
                {
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Encodes 16-bit PCM to XMA the way XACT wants it (xmaencode /S: looping, 64 KiB blocks)
    /// and returns the encoder's file.
    /// </summary>
    public static byte[] EncodeXma(WavFile wav, string encoder, int quality = DefaultXmaQuality)
    {
        string dir = Path.Combine(Path.GetTempPath(), "ShadowForge", "xmaencode-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            string input = Path.Combine(dir, "in.wav");
            string output = Path.Combine(dir, "out.xma");
            wav.WriteFile(input);

            var psi = new ProcessStartInfo(encoder)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = dir,
            };
            foreach (string a in new[] { input, "/S", "/Q", quality.ToString(), "/T", output }) psi.ArgumentList.Add(a);
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("The XMA encoder did not start.");
            var errors = process.StandardError.ReadToEndAsync();
            string messages = process.StandardOutput.ReadToEnd() + errors.Result;
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(output))
            {
                string reason = messages.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0)
                                ?? $"exit {process.ExitCode}";
                throw new InvalidDataException("XMA encoder: " + reason);
            }
            byte[] encoded = File.ReadAllBytes(output);
            XmaFile.Read(encoded, "encoded XMA");
            return encoded;
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>xmaencode's own default quality, from 1 (poor) to 100 (best).</summary>
    public const int DefaultXmaQuality = 60;

    /// <summary>
    /// Writes a wave as a 16-bit PCM .wav. PCM is written directly; XMA is decoded by FFmpeg,
    /// which must be given.
    /// </summary>
    public static void DecodeTo(WaveBankEntry entry, string wavPath, string? ffmpeg)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);
        if (entry.Format.Tag == WaveFormatTag.PCM)
        {
            WavFile.FromBigEndianPcm(entry.Data, entry.Format.Channels, entry.Format.SamplesPerSecond).WriteFile(wavPath);
            return;
        }
        if (entry.Format.Tag != WaveFormatTag.XMA)
            throw new NotSupportedException($"{entry.Format.Tag} waves cannot be decoded.");
        if (ffmpeg is null)
            throw new InvalidOperationException("Decoding XMA needs FFmpeg. Set its path in Settings.");

        string xma = Path.Combine(Path.GetTempPath(), "ShadowForge", "xma-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(xma)!);
            File.WriteAllBytes(xma, XmaRiff.Wrap(entry));
            RunFfmpeg(ffmpeg, ["-i", xma, "-c:a", "pcm_s16le", wavPath]);
        }
        finally
        {
            if (File.Exists(xma)) File.Delete(xma);
        }
    }

    /// <summary>
    /// Reads any audio file as 16-bit PCM with the given channels and sample rate, converting
    /// through FFmpeg when it is available. Without it, only a 16-bit PCM .wav is accepted as is.
    /// </summary>
    public static WavFile LoadAsPcm(string path, int channels, int sampleRate, string? ffmpeg)
    {
        if (ffmpeg is null) return WavFile.ReadFile(path);

        string converted = Path.Combine(Path.GetTempPath(), "ShadowForge", "pcm-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(converted)!);
            RunFfmpeg(ffmpeg, ["-i", path, "-ac", channels.ToString(), "-ar", sampleRate.ToString(),
                "-c:a", "pcm_s16le", "-map_metadata", "-1", "-fflags", "+bitexact", converted]);
            return WavFile.ReadFile(converted);
        }
        finally
        {
            if (File.Exists(converted)) File.Delete(converted);
        }
    }

    private static void RunFfmpeg(string ffmpeg, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        foreach (string a in new[] { "-v", "error", "-y" }.Concat(args)) psi.ArgumentList.Add(a);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg did not start.");
        string errors = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidDataException("FFmpeg: " + (errors.Trim().Split('\n').LastOrDefault() ?? $"exit {process.ExitCode}"));
    }

    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndFilename = 0x00020000;

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);

    /// <summary>
    /// Plays a .wav in the background, stopping whatever was playing.
    /// </summary>
    public static void Play(string wavPath)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Playback needs Windows.");
        if (!PlaySound(wavPath, IntPtr.Zero, SndFilename | SndAsync | SndNoDefault))
            throw new InvalidOperationException("Windows could not play the wave.");
    }

    public static void Stop()
    {
        if (OperatingSystem.IsWindows()) PlaySound(null, IntPtr.Zero, 0);
    }
}
