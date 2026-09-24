namespace ShadowForge.Generators;

internal sealed class FieldInfo
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public FieldKind Kind { get; set; }
    public int Size { get; set; }

    /// <summary>
    /// The [Offset] value while analyzing, the resolved byte offset once laid out.
    /// </summary>
    public int Offset { get; set; }

    public bool HasExplicitOffset { get; set; }
    public bool IsSkipped { get; set; }
    public bool IsByteArray { get; set; }
    public int EncodedStringLength { get; set; }

    /// <summary>
    /// C# keyword of the integer type read and written: the field's own type for
    /// <see cref="FieldKind.Primitive"/>, the underlying type for <see cref="FieldKind.Enum"/>.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Fully qualified type of a struct field. Set for unsupported structs too, which selects
    /// the SF007 diagnostic over SF005.
    /// </summary>
    public string? NestedStructFullName { get; set; }

    public FieldInfo At(int offset)
    {
        var copy = (FieldInfo)MemberwiseClone();
        copy.Offset = offset;
        return copy;
    }
}
