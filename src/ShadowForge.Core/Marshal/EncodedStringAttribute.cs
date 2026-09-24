namespace ShadowForge.Marshal;

/// <summary>
/// A fixed-width text field held as raw bytes. The generated code copies exactly
/// <see cref="ByteLength"/> bytes and never decodes them, so <see cref="Encoding"/>
/// records the field's encoding for callers only. The field must be a byte[].
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class EncodedStringAttribute : Attribute
{
    public int ByteLength { get; }
    public StringEncoding Encoding { get; }

    public EncodedStringAttribute(int byteLength, StringEncoding encoding = StringEncoding.ASCII)
    {
        ByteLength = byteLength;
        Encoding = encoding;
    }
}
