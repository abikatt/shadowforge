namespace ShadowForge.Formats.MDL;

/// <summary>
/// An OBJECTOPT overlay: slot number and overlay .hdb, such as em028's blinking eye.
/// </summary>
public readonly record struct ObjectOpt(int Slot, string HDBFile);
