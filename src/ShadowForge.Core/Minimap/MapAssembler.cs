using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HOC;
using ShadowForge.Formats.MAP;
using ShadowForge.GameData;

namespace ShadowForge.Minimap;

/// <summary>
/// Reads a stage's db_{stem}.map, reads each placed MODEL block's OBJECT .hdb,
/// and merges every model's world-space triangles into one list alongside the
/// stage's PARTS OCT collision mesh.
/// </summary>
public static class MapAssembler
{
    /// <param name="files">The game data to read the stage from.</param>
    /// <param name="stem">Stage stem, e.g. "bg01_01", "bi03a01", "dg05_03".</param>
    /// <param name="log">Optional sink for skip/error diagnostics.</param>
    public static StageMesh AssembleStage(GameFileSystem files, string stem, Action<string>? log = null)
    {
        var map = StageDef.Read(files.ReadMapDef(stem));
        var tris = new List<Tri>();

        foreach (var model in map.Models)
        {
            string? objectHDB = model.ObjectHDB;
            if (objectHDB is null)
            {
                log?.Invoke($"skip {model.Name}: no OBJECT path");
                continue;
            }

            if (SkipReason(Path.GetFileName(objectHDB)) is { } reason)
            {
                log?.Invoke($"skip {model.Name}: {reason}");
                continue;
            }

            if (ReadAsset(files, stem, objectHDB, log) is not { } hdb)
            {
                log?.Invoke($"error {model.Name}: could not resolve OBJECT path '{objectHDB}'");
                continue;
            }

            var modelFile = ModelCooker.Bake(ModelReader.Read(hdb));
            Mesh.CollectTriangles(modelFile, BuildTransform(model.Settings), tris);
        }

        var collision = new List<Tri>();
        foreach (var part in map.Parts)
        {
            if (!string.Equals(part.Kind, "OCT", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ReadAsset(files, stem, part.Path, log) is not { } hocb)
            {
                log?.Invoke($"error: could not resolve PARTS OCT path '{part.Path}'");
                continue;
            }

            foreach (var t in CollisionMesh.Read(hocb).Triangles)
                collision.Add(new Tri(t.A, t.B, t.C));
        }

        return new StageMesh { Render = tris, Collision = collision };
    }

    private static readonly Regex FarSceneryOrEnvSuffix = new(@"fa\d*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] EnvironmentTokens = ["sky", "cld", "wat", "sun", "lgt", "spt"];

    private static string? SkipReason(string fileName)
    {
        if (fileName.Contains("lgh", StringComparison.OrdinalIgnoreCase))
            return "lighting model (lgh)";
        if (fileName.EndsWith("_obs.hdb", StringComparison.OrdinalIgnoreCase))
            return "obstruction mesh (_obs)";

        string stem = Path.GetFileNameWithoutExtension(fileName);
        if (EnvironmentTokens.Any(t => stem.Contains(t, StringComparison.OrdinalIgnoreCase))
            || FarSceneryOrEnvSuffix.IsMatch(stem))
            return "environment model (sky/cloud/water/sun/light-rig/spot-rig/far-scenery)";
        return null;
    }

    private static byte[]? ReadAsset(GameFileSystem files, string stem, string vfsPath, Action<string>? log)
    {
        try
        {
            return files.ReadStageAsset(stem, vfsPath);
        }
        catch (FileNotFoundException ex)
        {
            log?.Invoke($"error: {ex.Message}");
            return null;
        }
    }

    private static Matrix4x4 BuildTransform(IReadOnlyDictionary<string, string> settings)
    {
        Vector3 position = ParseVector3(settings, "POSITION", Vector3.Zero);
        Vector3 rotateDeg = ParseVector3(settings, "ROTATE", Vector3.Zero);
        Vector3 scale = ParseVector3(settings, "SCALE", Vector3.One);

        var rotation = Matrix4x4.CreateRotationX(float.DegreesToRadians(rotateDeg.X)) *
                       Matrix4x4.CreateRotationY(float.DegreesToRadians(rotateDeg.Y)) *
                       Matrix4x4.CreateRotationZ(float.DegreesToRadians(rotateDeg.Z));

        return Matrix4x4.CreateScale(scale) * rotation * Matrix4x4.CreateTranslation(position);
    }

    private static Vector3 ParseVector3(IReadOnlyDictionary<string, string> settings, string key, Vector3 fallback)
    {
        if (!settings.TryGetValue(key, out string? raw)) return fallback;

        string[] parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return fallback;

        return new Vector3(
            ParseFloat(parts[0], fallback.X),
            ParseFloat(parts[1], fallback.Y),
            ParseFloat(parts[2], fallback.Z));
    }

    private static float ParseFloat(string token, float fallback) =>
        float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
}
