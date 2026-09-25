using System.Numerics;

namespace ShadowForge.Formats.HDB;

/// <summary>
/// Bind-pose triangles with their normals, UVs and texture slots, plus the bounding sphere,
/// built once by <see cref="PreviewMeshBuilder"/> and rendered from any camera. Slots are
/// named after their stage-0 texture and shared by name across the models in the mesh.
/// </summary>
public sealed class PreviewMesh
{
    internal IReadOnlyList<PreviewRenderer.Triangle> Triangles { get; }
    public Vector3 Center { get; }
    public float Radius { get; }
    public int TriangleCount => Triangles.Count;

    /// <summary>
    /// The texture name of each material slot, indexed like a triangle's Material.
    /// </summary>
    public IReadOnlyList<string> MaterialNames { get; }

    /// <summary>
    /// One entry per material slot, null where no texture was found. Null until
    /// <see cref="WithTextures"/> has run.
    /// </summary>
    internal IReadOnlyList<PreviewTexture?>? Textures { get; }

    public bool HasTextures => Textures is not null;
    public int TexturesFound => Textures?.Count(t => t is not null) ?? 0;

    internal PreviewMesh(IReadOnlyList<PreviewRenderer.Triangle> triangles, IReadOnlyList<string>? materialNames = null)
    {
        Triangles = triangles;
        MaterialNames = materialNames ?? [];
        if (triangles.Count == 0)
        {
            Radius = 1f;
            return;
        }

        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var t in triangles)
        {
            min = Vector3.Min(min, Vector3.Min(t.A, Vector3.Min(t.B, t.C)));
            max = Vector3.Max(max, Vector3.Max(t.A, Vector3.Max(t.B, t.C)));
        }
        Center = (min + max) * 0.5f;

        float r2 = 0f;
        foreach (var t in triangles)
        {
            r2 = MathF.Max(r2, Vector3.DistanceSquared(t.A, Center));
            r2 = MathF.Max(r2, Vector3.DistanceSquared(t.B, Center));
            r2 = MathF.Max(r2, Vector3.DistanceSquared(t.C, Center));
        }
        Radius = MathF.Max(MathF.Sqrt(r2), 1e-6f);
    }

    private PreviewMesh(PreviewMesh source, IReadOnlyList<PreviewTexture?> textures)
    {
        Triangles = source.Triangles;
        MaterialNames = source.MaterialNames;
        Center = source.Center;
        Radius = source.Radius;
        Textures = textures;
    }

    /// <summary>
    /// A copy of this mesh with each slot's texture read from <paramref name="textureDir"/> or
    /// its subfolders: {name}.dds, else the first slice of a {name}.36t volume. A texture that
    /// is missing or fails to decode leaves its slot null.
    /// </summary>
    public PreviewMesh WithTextures(string textureDir, int maxSize = 512)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pattern in new[] { "*.dds", "*.36t" })
            foreach (string file in Directory.EnumerateFiles(textureDir, pattern, SearchOption.AllDirectories))
                files.TryAdd(Path.GetFileName(file), file);

        var textures = new PreviewTexture?[MaterialNames.Count];
        for (int i = 0; i < textures.Length; i++)
        {
            string name = MaterialNames[i];
            try
            {
                if (files.TryGetValue(name + ".dds", out string? dds))
                    textures[i] = PreviewTexture.Decode(File.ReadAllBytes(dds), isVolume: false, maxSize);
                else if (files.TryGetValue(name + ".36t", out string? volume))
                    textures[i] = PreviewTexture.Decode(File.ReadAllBytes(volume), isVolume: true, maxSize);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException
                                           or ArgumentException or IndexOutOfRangeException)
            {
                textures[i] = null;
            }
        }
        return new PreviewMesh(this, textures);
    }
}

/// <summary>
/// Collects placed models into one <see cref="PreviewMesh"/>. Draws use their stage-0 texture
/// on UV set 0. Stage 1 and 2 layers are not previewed.
/// </summary>
public sealed class PreviewMeshBuilder
{
    private readonly List<PreviewRenderer.Triangle> _triangles = [];
    private readonly List<string> _materialNames = [];
    private readonly Dictionary<string, int> _materialByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a model's bind-pose triangles, moved by <paramref name="transform"/>.
    /// <paramref name="textureNames"/> overrides the model's texture names by slot ordinal.
    /// </summary>
    public PreviewMeshBuilder Add(ModelFile model, Matrix4x4 transform, IReadOnlyList<string>? textureNames = null)
    {
        int slotCount = Math.Max(model.TextureCount, 1);
        var slots = new int[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            string name = textureNames is not null && i < textureNames.Count ? textureNames[i]
                : i < model.Textures.Count ? model.Textures[i].Name : $"material_{i}";
            if (!_materialByName.TryGetValue(name, out int global))
            {
                global = _materialNames.Count;
                _materialNames.Add(name);
                _materialByName[name] = global;
            }
            slots[i] = global;
        }

        var boneGlobals = BindPose.ComputeGlobals(model.Bones);
        foreach (var va in VertexArrayDraws.Collect(model))
        {
            foreach (var (group, ia) in va.Draws)
            {
                var positions = new Vector3[va.Vertices.Count];
                var normals = new Vector3[va.Vertices.Count];
                var uvs = new Vector2[va.Vertices.Count];
                for (int i = 0; i < positions.Length; i++)
                {
                    var v = va.Vertices[i];
                    var (pos, normal) = BindPose.Anchor(v, group.BonePalette, boneGlobals);
                    positions[i] = Vector3.Transform(pos, transform);
                    var n = Vector3.TransformNormal(normal, transform);
                    normals[i] = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.Zero;
                    uvs[i] = new Vector2(v.U, 1f - v.V);
                }

                int material = slots[Math.Clamp(group.MaterialIndex, 0, slotCount - 1)];
                foreach (var (a, b, c) in DrawTriangles.Enumerate(ia.Indices, group.Topology, positions.Length))
                    _triangles.Add(new PreviewRenderer.Triangle(
                        positions[a], positions[b], positions[c],
                        normals[a], normals[b], normals[c],
                        uvs[a], uvs[b], uvs[c], material));
            }
        }
        return this;
    }

    public PreviewMesh Build() => new(_triangles.ToList(), _materialNames.ToList());
}

/// <summary>
/// An orbit camera for <see cref="PreviewRenderer.RenderInto(PreviewMesh, PreviewCamera, PreviewShading, Span{SixLabors.ImageSharp.PixelFormats.Rgba32}, Span{float}, int, int)"/>. Zoom 1 fits the bounding
/// sphere to the view. PanX and PanY shift the image by a fraction of the shorter side.
/// Start from <see cref="Default"/>: default(PreviewCamera) has Zoom 0 and draws nothing.
/// </summary>
public readonly record struct PreviewCamera(float YawDeg, float PitchDeg, float Zoom, float PanX, float PanY)
{
    /// <summary>
    /// The three-quarter view <see cref="PreviewRenderer.Render"/> uses, fitted to the view.
    /// </summary>
    public static PreviewCamera Default { get; } = new(30f, 22f, 1f, 0f, 0f);
}
