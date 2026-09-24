namespace ShadowForge.Formats.MAP;

/// <summary>
/// A PARTS line: sidecar kind (EFC, PST, CAM, DLT, FOG, LOC, OCT) and its path.
/// </summary>
public readonly record struct PartRef(string Kind, string Path);
