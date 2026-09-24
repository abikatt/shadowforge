using ShadowForge.Formats.MDL;

namespace ShadowForge.GameData;

public sealed class ResolvedEntity
{
    public required string Id { get; init; }
    public required string Class { get; init; }
    public required string RigId { get; init; }

    /// <summary>
    /// The class folder from the .mdl PATH. A shared rig (bs46 on em001) has a RigClass and
    /// RigId that differ from the entity's own Class and Id.
    /// </summary>
    public required string RigClass { get; init; }
    public required ModelDef ModelDef { get; init; }
    public required string ModelDefRelPath { get; init; }
    public required IReadOnlyList<ResolvedFile> Files { get; init; }
}
