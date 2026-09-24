using System.Numerics;
using ShadowForge.Formats.HDB.Wire;

namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 6: the model record. See <see cref="ModelRecord"/>. Pointer values are kept as
/// read and re-emitted unchanged.
/// </summary>
public sealed class RawModelEntry : RawEntry
{
    public int RenderCommandPtr { get; set; }
    public int IndexBlockCount { get; set; }
    public int IndexTablePtr { get; set; }
    public int VertexArrayCount { get; set; }
    public int VASetupPtr { get; set; }
    public Vector3 SphereCenter { get; set; }
    public uint SphereRadiusBits { get; set; }
    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.Model;
    internal override int ComputedPayloadLength => ModelRecord.Size;
}
