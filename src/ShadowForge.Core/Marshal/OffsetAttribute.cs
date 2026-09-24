namespace ShadowForge.Marshal;

/// <summary>
/// Places the field at an absolute byte offset in the struct. The offset may only move
/// forward from the end of the previous field, and a skipped range raises warning SF006.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class OffsetAttribute : Attribute
{
    public int Position { get; }
    public OffsetAttribute(int position) => Position = position;
}
