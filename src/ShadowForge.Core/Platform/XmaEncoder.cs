using System.Diagnostics;
using ShadowForge.Formats.XACT;

namespace ShadowForge.Platform;

/// <summary>
/// The Xbox 360 SDK's XMA encoder (xmaencode.exe). It is part of the SDK, so it cannot ship
/// with ShadowForge and each user points to their own copy.
/// </summary>
public static class XmaEncoder
{
    /// <summary>
    /// Environment variable naming the encoder, checked after an explicit path.
    /// </summary>
    public const string EnvironmentVariable = "SHADOWFORGE_XMA_ENCODER";

    /// <summary>
    /// The names the encoder ships under.
    /// </summary>
    public static readonly string[] FileNames = ["xmaencode2008.exe", "xmaencode.exe"];

    /// <summary>xmaencode's own default quality, from 1 (poor) to 100 (best).</summary>
    public const int DefaultQuality = 60;

    /// <summary>
    /// The encoder to use: the given path if it exists, else SHADOWFORGE_XMA_ENCODER, else one
    /// beside the running program or in its tools folder, else one on PATH. Null when none exists.
    /// </summary>
    public static string? Find(string? preferred)
    {
        if (preferred is { Length: > 0 } && File.Exists(preferred)) return preferred;
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } fromEnv && File.Exists(fromEnv))
            return fromEnv;

        var dirs = new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "tools") }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (string dir in dirs)
        {
            foreach (string name in FileNames)
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
    /// and returns the encoder's file, checked to parse as XMA.
    /// </summary>
    public static byte[] Encode(WavFile wav, string encoder, int quality = DefaultQuality)
    {
        if (quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "XMA quality must be 1-100.");

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
}
