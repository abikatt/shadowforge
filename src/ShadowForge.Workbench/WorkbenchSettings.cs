using System.Text.Json;
using ShadowForge.Formats.HDB;

namespace ShadowForge.Workbench;

/// <summary>
/// Per-user settings in %APPDATA%\ShadowForge\workbench.json. A missing or unreadable file
/// gives the defaults, so a bad file never stops the app from starting.
/// </summary>
public sealed class WorkbenchSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShadowForge", "workbench.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string? BlenderPath { get; set; }

    /// <summary>
    /// The shading both previews use, kept between runs.
    /// </summary>
    public PreviewShading PreviewShading { get; set; } = PreviewShading.Flat;

    /// <summary>
    /// Where derived entities are written, one folder per new id.
    /// </summary>
    public string DerivedRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShadowForge", "entities");

    /// <summary>
    /// Where exported map stages are written, one folder per stage id.
    /// </summary>
    public string MapExportRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShadowForge", "maps");

    public static WorkbenchSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<WorkbenchSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
