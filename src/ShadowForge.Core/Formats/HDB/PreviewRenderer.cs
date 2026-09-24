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

    private readonly record struct Triangle(Vector3 A, Vector3 B, Vector3 C);

    private static List<Triangle> CollectTriangles(ModelFile model)
    {
        var result = new List<Triangle>();
        var boneGlobals = BindPose.ComputeGlobals(model.Bones);

        foreach (var va in VertexArrayDraws.Collect(model))
        {
            foreach (var (group, ia) in va.Draws)
            {
                var positions = va.Vertices
                    .Select(v => BindPose.Anchor(v, group.BonePalette, boneGlobals).worldPos)
                    .ToArray();
                foreach (var (a, b, c) in DrawTriangles.Enumerate(ia.Indices, group.Topology, positions.Length))
                    result.Add(new Triangle(positions[a], positions[b], positions[c]));
            }
        }

        return result;
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
        int width, int height, Rgba32[] pixels, float[] zBuffer)
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
