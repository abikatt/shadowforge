using System.Text;
using ShadowForge.Scene;

namespace ShadowForge.Formats.BDSL;

/// <summary>
/// Reads and writes BDSL, the text form of an RPJ scene. The writer emits the scene
/// header, then entries, then waypoints. The reader splits on LF and drops a trailing CR.
/// </summary>
public static class TextScene
{
    public static SceneFile Read(string text)
    {
        var scene = new SceneFile();
        var lines = text.Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            string line = SceneTextSyntax.Clean(lines[i]);
            if (line.StartsWith("scene "))
                i = SceneHeaderText.Read(scene, lines, i);
            else if (EntryText.IsHeader(line))
                i = EntryText.Read(scene, lines, i);
            else if (line.StartsWith("waypoint "))
                i = WaypointText.Read(scene, lines, i);
            else
                i++;
        }
        return scene;
    }

    public static SceneFile ReadFile(string path) => Read(File.ReadAllText(path, Encoding.UTF8));

    public static string Write(SceneFile scene)
    {
        var sb = new StringBuilder();
        SceneHeaderText.Write(sb, scene);

        foreach (var entry in scene.Entries)
        {
            sb.AppendLine();
            EntryText.Write(sb, entry);
        }

        foreach (var wp in scene.Waypoints)
        {
            sb.AppendLine();
            WaypointText.Write(sb, wp);
        }

        return sb.ToString();
    }

    public static void Write(SceneFile scene, string path) =>
        File.WriteAllText(path, Write(scene), new UTF8Encoding(false));
}
