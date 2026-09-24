namespace ShadowForge.CLI.Commands;

/// <summary>
/// The files behind a "path or entity id" argument. Files resolved from an entity id live in
/// a temp directory that is deleted on dispose.
/// </summary>
internal sealed class ResolvedArg : IDisposable
{
    public required IReadOnlyList<string> Paths { get; init; }
    public required string Stem { get; init; }
    public required bool FromEntity { get; init; }
    public TempDir? Temp { get; init; }

    public string Path => Paths[0];

    public string Dir => System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!;

    /// <summary>
    /// The input path with <paramref name="ext"/>, or "{stem}{ext}" in the current directory
    /// for an entity id.
    /// </summary>
    public string DefaultOutput(string ext) =>
        FromEntity
            ? System.IO.Path.Combine(Environment.CurrentDirectory, Stem + ext)
            : System.IO.Path.ChangeExtension(Path, ext);

    public void Dispose() => Temp?.Dispose();
}
