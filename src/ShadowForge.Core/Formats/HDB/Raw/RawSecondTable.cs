namespace ShadowForge.Formats.HDB.Raw;

/// <summary>
/// The rebase table. Each slot holds the offset from the slot to a pointer cell. At
/// load time every non-zero cell a slot names is turned into an absolute address.
/// </summary>
public sealed class RawSecondTable
{
    public int[] Entries { get; set; } = [];

    /// <summary>
    /// The 4-byte word after the entry array.
    /// </summary>
    public int Padding { get; set; }
}
