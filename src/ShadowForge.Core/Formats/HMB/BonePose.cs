using System.Numerics;

namespace ShadowForge.Formats.HMB;

/// <summary>
/// A sampled track. A component is null when the track has no channel for it.
/// </summary>
public readonly record struct BonePose(Vector3? Translation, Quaternion? Rotation, Vector3? Scale);
