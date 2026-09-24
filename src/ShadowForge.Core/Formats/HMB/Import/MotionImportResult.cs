namespace ShadowForge.Formats.HMB.Import;

/// <summary>
/// The repacked archive, non-fatal warnings, and how many template entries were replaced,
/// appended or left untouched.
/// </summary>
public sealed record MotionImportResult(
    byte[] MPKBytes,
    IReadOnlyList<string> Warnings,
    int Replaced,
    int Appended,
    int Untouched);
