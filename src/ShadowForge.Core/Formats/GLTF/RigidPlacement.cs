using ShadowForge.Formats.HDB;

namespace ShadowForge.Formats.GLTF;

/// <summary>
/// One rigid model placed at identity in a combined scene export. GroupName is a
/// slash-separated node path.
/// </summary>
public sealed record RigidPlacement(string Name, ModelFile Model, string? GroupName = null);
