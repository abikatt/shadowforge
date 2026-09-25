using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShadowForge.Formats.HDB;

/// <summary>
/// Renders a cooked <see cref="ModelFile"/> to a flat-shaded PNG thumbnail with a
/// software rasterizer and Z-buffer, so it needs no GPU.
/// </summary>
public static class PreviewRenderer
{
    private static readonly Rgba32 Background = new(0x2A, 0x2A, 0x2C, 0xFF);
    private const float AmbientTerm = 0.25f;
    private const float DiffuseTerm = 0.75f;
    private static readonly Vector3 LightDir = Vector3.Normalize(new Vector3(0.5f, 1.0f, 0.7f));
    private static readonly Vector3 BaseColor = new(0.78f, 0.80f, 0.86f);

    public static byte[] Render(ModelFile model, int width = 384, int height = 384)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));

        var triangles = CollectTriangles(model);

        var pixels = new Rgba32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Background;

        if (triangles.Count > 0)
        {
            var view = BuildViewMatrix(yawDeg: 30f, pitchDeg: 22f);
            var viewed = TransformTriangles(triangles, view);

            var (min, max) = ComputeBounds(viewed);
            var (scale, ox, oy) = FitToScreen(min, max, width, height);

            var zBuffer = new float[width * height];
            for (int i = 0; i < zBuffer.Length; i++) zBuffer[i] = float.PositiveInfinity;

            foreach (var tri in viewed)
                Rasterize(tri, scale, ox, oy, width, height, pixels, zBuffer);
        }

        using var image = Image.LoadPixelData<Rgba32>(pixels, width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Collects a model's bind-pose triangles once, for repeated <see cref="RenderInto(PreviewMesh, PreviewCamera, PreviewShading, Span{Rgba32}, Span{float}, int, int)"/> calls.
    /// </summary>
    public static PreviewMesh Prepare(ModelFile model) =>
        new PreviewMeshBuilder().Add(model, Matrix4x4.Identity).Build();

    /// <summary>
    /// Renders a prepared mesh into caller-owned buffers of width * height. Unlike
    /// <see cref="Render"/>, the framing comes from the mesh's bounding sphere, not the
    /// rotated bounds, so the model keeps its size as the camera orbits.
    /// </summary>
    public static void RenderInto(PreviewMesh mesh, PreviewCamera camera,
        Span<Rgba32> pixels, Span<float> zBuffer, int width, int height) =>
        RenderInto(mesh, camera, PreviewShading.Flat, pixels, zBuffer, width, height);

    /// <summary>
    /// Renders with the given shading. The textured modes draw a slot whose texture is
    /// missing, or every slot before <see cref="PreviewMesh.WithTextures"/>, in smooth grey,
    /// and skip texels whose alpha is under half so cut-out foliage and hair show through.
    /// </summary>
    public static void RenderInto(PreviewMesh mesh, PreviewCamera camera, PreviewShading shading,
        Span<Rgba32> pixels, Span<float> zBuffer, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        int count = width * height;
        if (pixels.Length < count || zBuffer.Length < count)
            throw new ArgumentException("Buffers are smaller than width * height.");
        if (!(camera.Zoom > 0f))
            throw new ArgumentOutOfRangeException(nameof(camera), "Zoom must be positive; start from PreviewCamera.Default.");

        pixels[..count].Fill(Background);
        zBuffer[..count].Fill(float.PositiveInfinity);
        if (mesh.Triangles.Count == 0) return;

        var view = Matrix4x4.CreateTranslation(-mesh.Center)
            * BuildViewMatrix(camera.YawDeg, camera.PitchDeg);
        float scale = 0.48f * MathF.Min(width, height) / mesh.Radius * camera.Zoom;
        float ox = width * 0.5f + camera.PanX * MathF.Min(width, height);
        float oy = height * 0.5f + camera.PanY * MathF.Min(width, height);

        var target = new Target(pixels, zBuffer, width, height, scale, ox, oy);
        foreach (var t in mesh.Triangles)
        {
            var viewed = t with
            {
                A = Vector3.Transform(t.A, view),
                B = Vector3.Transform(t.B, view),
                C = Vector3.Transform(t.C, view),
                NA = Vector3.TransformNormal(t.NA, view),
                NB = Vector3.TransformNormal(t.NB, view),
                NC = Vector3.TransformNormal(t.NC, view),
            };
            var texture = mesh.Textures is { } textures && t.Material >= 0 && t.Material < textures.Count
                ? textures[t.Material]
                : null;
            RasterizeShaded(viewed, shading, texture, ref target);
        }
    }

    /// <summary>
    /// A triangle's corners, with per-corner normals and UVs and its material slot. A zero
    /// normal falls back to the face normal. Material -1 means no slot.
    /// </summary>
    internal readonly record struct Triangle(
        Vector3 A, Vector3 B, Vector3 C,
        Vector3 NA = default, Vector3 NB = default, Vector3 NC = default,
        Vector2 UA = default, Vector2 UB = default, Vector2 UC = default,
        int Material = -1);

    private static List<Triangle> CollectTriangles(ModelFile model) =>
        Prepare(model).Triangles.ToList();

    private readonly ref struct Target(
        Span<Rgba32> pixels, Span<float> zBuffer, int width, int height, float scale, float ox, float oy)
    {
        public readonly Span<Rgba32> Pixels = pixels;
        public readonly Span<float> ZBuffer = zBuffer;
        public readonly int Width = width;
        public readonly int Height = height;
        public readonly float Scale = scale;
        public readonly float OX = ox;
        public readonly float OY = oy;
    }

    private static readonly Rgba32 WireColor = new(0xD8, 0xDC, 0xE6, 0xFF);
    private const float WireFill = 0.35f;
    private const float WireWidth = 0.75f;

    private static void RasterizeShaded(in Triangle tri, PreviewShading shading, PreviewTexture? texture,
        ref Target target)
    {
        float ax = tri.A.X * target.Scale + target.OX, ay = -tri.A.Y * target.Scale + target.OY, az = tri.A.Z;
        float bx = tri.B.X * target.Scale + target.OX, by = -tri.B.Y * target.Scale + target.OY, bz = tri.B.Z;
        float cx = tri.C.X * target.Scale + target.OX, cy = -tri.C.Y * target.Scale + target.OY, cz = tri.C.Z;
        Vector3 nb = tri.NB, nc = tri.NC;
        Vector2 ub = tri.UB, uc = tri.UC;

        float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        if (MathF.Abs(area) < 1e-4f) return;
        if (area < 0f)
        {
            (bx, cx) = (cx, bx);
            (by, cy) = (cy, by);
            (bz, cz) = (cz, bz);
            (nb, nc) = (nc, nb);
            (ub, uc) = (uc, ub);
            area = -area;
        }

        var faceNormal = Vector3.Cross(tri.B - tri.A, tri.C - tri.A);
        if (faceNormal.LengthSquared() < 1e-12f) return;
        faceNormal = Vector3.Normalize(faceNormal);
        float faceLight = AmbientTerm + DiffuseTerm * MathF.Abs(Vector3.Dot(faceNormal, LightDir));

        bool smoothNormals = tri.NA.LengthSquared() > 1e-12f && nb.LengthSquared() > 1e-12f && nc.LengthSquared() > 1e-12f;
        bool textured = shading.NeedsTextures() && texture is not null;
        bool perPixelLight = shading == PreviewShading.Smooth || (shading.NeedsTextures() && !textured)
            || (shading == PreviewShading.Textured && textured);

        Rgba32 flatColor = shading switch
        {
            PreviewShading.MaterialColors => ToColor(MaterialColor(tri.Material) * faceLight),
            PreviewShading.Wireframe => ToColor(BaseColor * (faceLight * WireFill)),
            _ => ToColor(BaseColor * faceLight),
        };

        // Distance from a pixel to the edge opposite a corner is that corner's weight times
        // the triangle's height over the edge, in pixels.
        float hA = area / MathF.Max(MathF.Sqrt((cx - bx) * (cx - bx) + (cy - by) * (cy - by)), 1e-6f);
        float hB = area / MathF.Max(MathF.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy)), 1e-6f);
        float hC = area / MathF.Max(MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)), 1e-6f);

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx))));
        int maxX = Math.Min(target.Width - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy))));
        int maxY = Math.Min(target.Height - 1, (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy))));

        float invArea = 1f / area;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float w0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) * invArea;
                float w1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) * invArea;
                float w2 = 1f - w0 - w1;
                if (w0 < 0f || w1 < 0f || w2 < 0f) continue;

                float depth = w0 * az + w1 * bz + w2 * cz;
                int idx = y * target.Width + x;
                if (depth >= target.ZBuffer[idx]) continue;

                Rgba32 color;
                if (shading == PreviewShading.Wireframe)
                {
                    float edge = MathF.Min(w0 * hA, MathF.Min(w1 * hB, w2 * hC));
                    color = edge < WireWidth ? WireColor : flatColor;
                }
                else if (textured || perPixelLight)
                {
                    float light = faceLight;
                    if (perPixelLight && smoothNormals)
                    {
                        var n = w0 * tri.NA + w1 * nb + w2 * nc;
                        float len = n.Length();
                        if (len > 1e-6f)
                            light = AmbientTerm + DiffuseTerm * MathF.Abs(Vector3.Dot(n / len, LightDir));
                    }

                    if (textured)
                    {
                        var uv = w0 * tri.UA + w1 * ub + w2 * uc;
                        var texel = texture!.Sample(uv.X, uv.Y);
                        if (texel.A < 128) continue;
                        color = shading == PreviewShading.TexturedUnlit
                            ? texel with { A = 0xFF }
                            : ToColor(new Vector3(texel.R, texel.G, texel.B) / 255f * light);
                    }
                    else
                    {
                        color = ToColor(BaseColor * light);
                    }
                }
                else
                {
                    color = flatColor;
                }

                target.ZBuffer[idx] = depth;
                target.Pixels[idx] = color;
            }
        }
    }

    private static Rgba32 ToColor(Vector3 rgb) => new(
        (byte)Math.Clamp(rgb.X * 255f, 0f, 255f),
        (byte)Math.Clamp(rgb.Y * 255f, 0f, 255f),
        (byte)Math.Clamp(rgb.Z * 255f, 0f, 255f),
        (byte)0xFF);

    /// <summary>
    /// Spreads slot hues by the golden ratio, so neighbouring slots never look alike.
    /// </summary>
    private static Vector3 MaterialColor(int material)
    {
        if (material < 0) return BaseColor;
        float h = material * 0.618034f % 1f * 6f;
        float x = 1f - MathF.Abs(h % 2f - 1f);
        var hue = (int)h switch
        {
            0 => new Vector3(1, x, 0),
            1 => new Vector3(x, 1, 0),
            2 => new Vector3(0, 1, x),
            3 => new Vector3(0, x, 1),
            4 => new Vector3(x, 0, 1),
            _ => new Vector3(1, 0, x),
        };
        const float saturation = 0.55f, value = 0.92f;
        return (Vector3.One * (1f - saturation) + hue * saturation) * value;
    }

    private static Matrix4x4 BuildViewMatrix(float yawDeg, float pitchDeg)
    {
        float yaw = yawDeg * MathF.PI / 180f;
        float pitch = pitchDeg * MathF.PI / 180f;
        return Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateRotationX(pitch);
    }

    private static List<Triangle> TransformTriangles(List<Triangle> triangles, Matrix4x4 view)
    {
        var result = new List<Triangle>(triangles.Count);
        foreach (var t in triangles)
        {
            result.Add(new Triangle(
                Vector3.Transform(t.A, view),
                Vector3.Transform(t.B, view),
                Vector3.Transform(t.C, view)));
        }
        return result;
    }

    private static (Vector3 min, Vector3 max) ComputeBounds(List<Triangle> tris)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var t in tris)
        {
            min = Vector3.Min(min, Vector3.Min(t.A, Vector3.Min(t.B, t.C)));
            max = Vector3.Max(max, Vector3.Max(t.A, Vector3.Max(t.B, t.C)));
        }
        return (min, max);
    }

    private static (float scale, float ox, float oy) FitToScreen(Vector3 min, Vector3 max, int width, int height)
    {
        float spanX = MathF.Max(max.X - min.X, 1e-6f);
        float spanY = MathF.Max(max.Y - min.Y, 1e-6f);
        float scale = 0.9f * MathF.Min(width / spanX, height / spanY);

        float cx = (min.X + max.X) * 0.5f;
        float cy = (min.Y + max.Y) * 0.5f;
        float ox = width * 0.5f - cx * scale;
        float oy = height * 0.5f + cy * scale;
        return (scale, ox, oy);
    }

    private static void Rasterize(Triangle tri, float scale, float ox, float oy,
        int width, int height, Span<Rgba32> pixels, Span<float> zBuffer)
    {
        float ax = tri.A.X * scale + ox, ay = -tri.A.Y * scale + oy, az = tri.A.Z;
        float bx = tri.B.X * scale + ox, by = -tri.B.Y * scale + oy, bz = tri.B.Z;
        float cx = tri.C.X * scale + ox, cy = -tri.C.Y * scale + oy, cz = tri.C.Z;

        float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        if (MathF.Abs(area) < 1e-4f) return;
        if (area < 0f)
        {
            (bx, cx) = (cx, bx);
            (by, cy) = (cy, by);
            (bz, cz) = (cz, bz);
            area = -area;
        }

        var normal = Vector3.Cross(tri.B - tri.A, tri.C - tri.A);
        if (normal.LengthSquared() < 1e-12f) return;
        normal = Vector3.Normalize(normal);
        float lambert = MathF.Abs(Vector3.Dot(normal, LightDir));
        float intensity = AmbientTerm + DiffuseTerm * lambert;
        var shaded = BaseColor * intensity;

        var color = new Rgba32(
            (byte)Math.Clamp(shaded.X * 255f, 0f, 255f),
            (byte)Math.Clamp(shaded.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(shaded.Z * 255f, 0f, 255f),
            (byte)0xFF);

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx))));
        int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy))));
        int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy))));

        float invArea = 1f / area;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float w0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) * invArea;
                float w1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) * invArea;
                float w2 = 1f - w0 - w1;
                if (w0 < 0f || w1 < 0f || w2 < 0f) continue;

                float depth = w0 * az + w1 * bz + w2 * cz;
                int idx = y * width + x;
                if (depth < zBuffer[idx])
                {
                    zBuffer[idx] = depth;
                    pixels[idx] = color;
                }
            }
        }
    }
}
