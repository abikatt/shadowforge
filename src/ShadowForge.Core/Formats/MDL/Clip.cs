namespace ShadowForge.Formats.MDL;

/// <summary>
/// A named motion from a MOTINPK or MOTION line.
/// </summary>
public readonly record struct Clip(string Name, string HMBFile);
