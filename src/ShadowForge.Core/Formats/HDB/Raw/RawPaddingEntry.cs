namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// Type 0: alignment padding, kept as read.
/// </summary>
public sealed class RawPaddingEntry : RawEntry
{
    public byte[] Payload { get; set; } = [];
    internal override int ComputedDiskEntryType => (int)FirstTableEntryType.Padding;
    internal override int ComputedPayloadLength => Payload.Length;
}
