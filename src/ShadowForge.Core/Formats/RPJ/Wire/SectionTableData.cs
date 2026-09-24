using ShadowForge.Marshal;
using ShadowForge.Scene;

namespace ShadowForge.Formats.RPJ.Wire;

[BigEndian, StructSize(0x48)]
public partial struct SectionTableData
{
    public uint SceneVersion;
    public AreaType AreaType;
    public uint StageId;
    public float AreaOriginX;
    public float AreaOriginY;
    public float AreaOriginZ;
    public uint AreaOriginW;
    public uint AreaMetadata;
    public uint AreaConfig0;
    public uint AreaConfig1;
    public uint AreaConfig2;
    public uint AreaConfig3;
    public uint AreaConfig4;
    [Computed] public uint EntryPoolSize;
    [Computed] public uint ScriptSectionSize;
    [Computed] public uint DataBaseOffset;
    [Computed] public uint WaypointSectionSize;
    [Computed] public uint WaypointOffset;
}
