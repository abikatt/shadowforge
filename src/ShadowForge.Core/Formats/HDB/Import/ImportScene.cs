namespace ShadowForge.Formats.HDB.Import;

public sealed class ImportScene
{
    public List<ImportBone> Bones = new();
    public List<ImportVertex> Vertices = new();
    public List<ImportTriangle> Triangles = new();
    public List<ImportTexture> Textures = new();
    public bool Skinned;
}
