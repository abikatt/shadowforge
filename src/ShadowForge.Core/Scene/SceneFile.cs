namespace ShadowForge.Scene;

/// <summary>
/// One field scene: the model both the RPJ binary and the BDSL text map onto.
/// </summary>
public sealed class SceneFile
{
    /// <summary>
    /// Four ASCII characters, such as "0.26".
    /// </summary>
    public string Version { get; set; } = "";

    public uint Flags { get; set; }

    /// <summary>
    /// At most 64 bytes of Shift-JIS.
    /// </summary>
    public string SceneName { get; set; } = "";

    /// <summary>
    /// At most 28 bytes of Shift-JIS.
    /// </summary>
    public string Filename { get; set; } = "";

    public AreaType AreaType { get; set; }

    /// <summary>
    /// Area code times 100 plus variant, so dg44_05 is 4405.
    /// </summary>
    public uint StageId { get; set; }

    public uint AreaMetadata { get; set; }

    /// <summary>
    /// At most 256 bytes of Shift-JIS.
    /// </summary>
    public string MessagePath { get; set; } = "";

    /// <summary>
    /// Path of the tool that built the file. At most 260 bytes of Shift-JIS.
    /// </summary>
    public string BuildPath { get; set; } = "";

    /// <summary>
    /// Zero in every shipped file.
    /// </summary>
    public uint SceneVersion { get; set; }

    public float AreaOriginX { get; set; }

    public float AreaOriginY { get; set; }

    public float AreaOriginZ { get; set; }

    /// <summary>
    /// Zero in every shipped file.
    /// </summary>
    public uint AreaOriginW { get; set; }

    public uint AreaConfig0 { get; set; }

    public uint AreaConfig1 { get; set; }

    public uint AreaConfig2 { get; set; }

    public uint AreaConfig3 { get; set; }

    public uint AreaConfig4 { get; set; }

    public List<SceneEntry> Entries { get; set; } = [];

    public List<Waypoint> Waypoints { get; set; } = [];
}
