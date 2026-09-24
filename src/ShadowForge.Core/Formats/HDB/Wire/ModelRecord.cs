using System.Numerics;
using ShadowForge.Marshal;

namespace ShadowForge.Formats.HDB.Wire;

/// <summary>
/// The type-6 model record: pointers to this model's render-command stream,
/// index table and vertex-array setup, plus its bounding sphere. Each *Ptr
/// field holds a signed offset from the field itself until the rebase table
/// turns it into an absolute address.
/// </summary>
[BigEndian, StructSize(0x24)]
public partial struct ModelRecord
{
    public const int RenderCommandPtrOffset = 0x00;
    public const int IndexBlockCountOffset = 0x04;
    public const int IndexTablePtrOffset = 0x08;
    public const int VertexArrayCountOffset = 0x0C;
    public const int VASetupPtrOffset = 0x10;
    public const int SphereCenterOffset = 0x14;
    public const int SphereRadiusOffset = 0x20;

    public int RenderCommandPtr;
    public int IndexBlockCount;
    public int IndexTablePtr;
    public int VertexArrayCount;
    public int VASetupPtr;
    public Vector3 SphereCenter;
    /// <summary>
    /// Radius as float32 bits. A radius that is not positive culls the model every frame.
    /// </summary>
    public uint SphereRadiusBits;
}
