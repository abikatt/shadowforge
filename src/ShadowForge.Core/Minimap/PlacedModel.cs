using System.Numerics;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.MAP;

namespace ShadowForge.Minimap;

/// <summary>
/// One MODEL block of a stage: its cooked .hdb and the transform its POSITION, ROTATE and
/// SCALE keys give.
/// </summary>
public sealed record PlacedModel(ModelRef Ref, ModelFile Model, Matrix4x4 Transform);
