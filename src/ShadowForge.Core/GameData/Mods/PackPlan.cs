namespace ShadowForge.GameData.Mods;

public sealed record PackEntry(string SourcePath, string OverrideRelPath);

/// <summary>
/// Every edited file of one entity with the mod-relative path it overrides. Skipped lists the
/// files left out because the game does not read their type.
/// </summary>
public sealed record PackPlan(
    string EntityId, string RigId, IReadOnlyList<PackEntry> Entries, IReadOnlyList<string> Skipped);
