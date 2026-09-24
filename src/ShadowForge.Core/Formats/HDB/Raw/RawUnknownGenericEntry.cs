namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Any other entry type, kept as bytes with its disk type code.
/// </summary>
public sealed class RawUnknownGenericEntry : RawEntry
{
    public byte[] Payload { get; set; } = [];
    internal override int ComputedDiskEntryType => DiskEntryType;
    internal override int ComputedPayloadLength => Payload.Length;
}
