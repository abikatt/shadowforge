namespace ShadowForge.CLI.Commands;

/// <summary>
/// A fresh directory under the system temp path, deleted on dispose. Delete failures are ignored.
/// </summary>
internal sealed class TempDir : IDisposable
{
    public string FullName { get; }

    public TempDir(string label)
    {
        FullName = Path.Combine(Path.GetTempPath(), "shadowforge", $"{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FullName);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(FullName))
                Directory.Delete(FullName, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
