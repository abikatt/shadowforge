namespace ShadowForge.GameData;

internal static class DirectoryTree
{
    public static void CopyContents(string sourceDir, string destDir)
    {
        foreach (string file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string dest = Path.Combine(destDir, Path.GetRelativePath(sourceDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    /// <summary>
    /// Deletes a staging tree and swallows failures. Files copied from a read-only source keep
    /// the read-only attribute, which makes Directory.Delete throw, so it is cleared first.
    /// </summary>
    public static void DeleteStaging(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
        catch
        {
        }
    }
}
