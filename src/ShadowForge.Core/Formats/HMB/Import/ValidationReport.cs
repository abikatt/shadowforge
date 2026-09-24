namespace ShadowForge.Formats.HMB.Import;

public sealed record ValidationReport(
    string ClipName,
    float MaxTranslationError,
    float MaxRotationErrorRadians,
    float MaxScaleError,
    IReadOnlyList<string> Messages,
    bool Passed);
