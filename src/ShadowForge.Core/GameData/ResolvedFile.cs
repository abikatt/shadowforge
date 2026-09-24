namespace ShadowForge.GameData;

/// <summary>
/// One file an entity needs. VfsPath is the path the game requests and a mod overrides
/// (chara\ene\em001\em001_obj.hdb). LooseRelPath is where a loose extract stores it, relative
/// to the game-data root, which for database files equals VfsPath. IPKName and IPKInnerPath
/// locate it in a packed install.
/// </summary>
public sealed record ResolvedFile(
    FileRole Role,
    string VfsPath,
    string LooseRelPath,
    string IPKName,
    string IPKInnerPath,
    bool Exists);
