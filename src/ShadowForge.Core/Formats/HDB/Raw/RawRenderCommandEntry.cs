namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 3: one render command stream. Each type-3 entry stays a separate instance in disk order.
/// </summary>
public sealed class RawRenderCommandEntry : RawEntry
{
    public List<RenderCommand> Commands { get; set; } = new();
    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.RenderCommands;
    internal override int ComputedPayloadLength => RenderCommandStream.GetSerializedLength(Commands);
}
