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

    /// <summary>
    /// The game-data folder to open, or null to detect it (SHADOWFORGE_GAME_ROOT, then the
    /// reblue install).
    /// </summary>
    public string? GameRoot { get; set; }

    /// <summary>
    /// Which of the game's name tables labels characters and stages: "us", "de" or "es".
    /// </summary>
    public string NameLanguage { get; set; } = "us";

    public string? BlenderPath { get; set; }

    /// <summary>
    /// Whether map exports carry textures, or flat colours in their place.
    /// </summary>
    public bool ExportTextures { get; set; } = true;

    /// <summary>
    /// Where edited scene scripts are kept as BDSL text, mirroring the game's script folders.
    /// </summary>
    public string ScriptWorkRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShadowForge", "scripts");

    /// <summary>
    /// The mod that Build compiles scene scripts into, and whether it enables that mod.
    /// </summary>
    public string ScriptModName { get; set; } = "scene_edits";

    /// <summary>
    /// The mod name Deploy edit as mod last used, offered again next time.
    /// </summary>
    public string? LastDeployMod { get; set; }
    public bool ScriptEnableOnBuild { get; set; } = true;

    /// <summary>
    /// The longest side, in pixels, that preview textures are scaled down to. 0 keeps full size.
    /// </summary>
    public int PreviewTextureSize { get; set; } = 512;

    /// <summary>
    /// The shading both previews use, kept between runs.
    /// </summary>
    public PreviewShading PreviewShading { get; set; } = PreviewShading.Flat;

    /// <summary>
    /// The Characters list's sort label and filters. A null class shows every class.
    /// </summary>
    public string? CharacterSort { get; set; }
    public string? CharacterClass { get; set; }
    public bool CharacterNamedOnly { get; set; }

    /// <summary>
    /// The Maps list's sort label and filters. A null category shows every category.
    /// </summary>
    public string? MapSort { get; set; }
    public string? MapCategory { get; set; }
    public bool MapNamedOnly { get; set; }
    public bool MapAvailableOnly { get; set; }

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
