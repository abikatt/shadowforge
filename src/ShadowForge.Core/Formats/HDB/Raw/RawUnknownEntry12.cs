namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 12: payload of unknown meaning, kept as bytes.
/// </summary>
public sealed class RawUnknownEntry12 : RawEntry
{
    public byte[] Payload { get; set; } = [];
    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.Unknown12;
    internal override int ComputedPayloadLength => Payload.Length;
}
