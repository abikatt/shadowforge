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
