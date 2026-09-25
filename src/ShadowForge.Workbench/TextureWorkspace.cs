using ShadowForge.Formats.DDS;
using ShadowForge.GameData;
using ShadowForge.GameData.Entities;
using ShadowForge.GameData.Mods;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShadowForge.Workbench;

public sealed record TextureExportResult(string Dir, int Written, int Kept);

public sealed record TextureDeployResult(
    string ModDir, IReadOnlyList<string> Deployed, int Unchanged, IReadOnlyList<string> Problems);

/// <summary>
/// A character's textures as PNGs in a working folder ({root}\{id}\{texture}.png) for editing
/// in any image editor, and the deploy of the edited ones as a mod. Only 2D .dds textures take
/// part; fur volumes (.36t) are left out. A PNG already in the folder is never replaced, since
/// it may hold edits.
/// </summary>
public sealed class TextureWorkspace
{
    private readonly GameInstall _install;

    public string Root { get; }

    public TextureWorkspace(GameInstall install, string root)
    {
        _install = install;
        Root = root;
    }

    public string DirFor(string id) => Path.Combine(Root, id);

    public TextureExportResult Export(string id)
    {
        string dir = DirFor(id);
        Directory.CreateDirectory(dir);
        int written = 0, kept = 0;
        WithRig(id, rig =>
        {
            foreach (string dds in rig.Textures)
            {
                string png = Path.Combine(dir, Path.GetFileNameWithoutExtension(dds) + ".png");
                if (File.Exists(png))
                {
                    kept++;
                    continue;
                }
                Converter.ConvertAndSave(dds, png);
                written++;
            }
        });
        if (written + kept == 0)
            throw new FileNotFoundException($"{id} has no .dds textures to export.");
        return new TextureExportResult(dir, written, kept);
    }

    /// <summary>
    /// Converts each PNG whose pixels differ from the shipped texture back to DDS, keeping the
    /// shipped header and format, and writes the results over the character in the mod. A PNG
    /// that names no texture of the character, or whose size changed, is reported and skipped.
    /// </summary>
    public TextureDeployResult Deploy(string id, string modsRoot, string modName, ModMetadata meta)
    {
        string dir = DirFor(id);
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"There are no exported textures for {id}. Use Export textures first.");

        string staged = Path.Combine(Path.GetTempPath(), "ShadowForge", "textures-" + Guid.NewGuid().ToString("N"));
        var deployed = new List<string>();
        var problems = new List<string>();
        int unchanged = 0;
        try
        {
            Directory.CreateDirectory(staged);
            WithRig(id, rig =>
            {
                var shipped = rig.Textures
                    .GroupBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                foreach (string png in Directory.EnumerateFiles(dir, "*.png").Order(StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileNameWithoutExtension(png);
                    if (!shipped.TryGetValue(name, out string? dds))
                    {
                        problems.Add($"{name}.png: {id} has no texture of that name");
                        continue;
                    }
                    try
                    {
                        if (SamePixels(png, dds))
                        {
                            unchanged++;
                            continue;
                        }
                        Importer.ImportWithReference(png, dds, Path.Combine(staged, name + ".dds"));
                        deployed.Add(name);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or UnknownImageFormatException
                                                   or NotSupportedException or IOException)
                    {
                        problems.Add($"{name}.png: {ex.Message}");
                    }
                }
            });

            if (deployed.Count == 0)
                return new TextureDeployResult("", deployed, unchanged, problems);

            var plan = new EntityPacker(_install).BuildPlan(id, staged);
            var result = new ModDeployer(_install).Deploy(plan, modName, meta);
            return new TextureDeployResult(result.ModDir, deployed, unchanged, problems);
        }
        finally
        {
            DeleteQuietly(staged);
        }
    }

    /// <summary>
    /// Whether the PNG still holds exactly the shipped texture's decoded pixels.
    /// </summary>
    private static bool SamePixels(string png, string dds)
    {
        var (rgba, width, height) = Converter.DecodeRgba(File.ReadAllBytes(dds));
        using var image = Image.Load<Rgba32>(png);
        if (image.Width != width || image.Height != height) return false;
        var pixels = new byte[width * height * 4];
        image.CopyPixelDataTo(pixels);
        return pixels.AsSpan().SequenceEqual(rgba);
    }

    private void WithRig(string id, Action<RigWorkspace> use)
    {
        string temp = Path.Combine(Path.GetTempPath(), "ShadowForge", "rig-" + Guid.NewGuid().ToString("N"));
        try
        {
            use(new EntityAssets(_install).MaterializeRig(id, temp));
        }
        finally
        {
            DeleteQuietly(temp);
        }
    }

    private static void DeleteQuietly(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
