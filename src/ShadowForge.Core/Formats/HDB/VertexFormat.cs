namespace ShadowForge.Formats.HDB;

/// <summary>
/// The type-5 format word and the two strides it implies: the disk stride of
/// a packed vertex record, and the GPU stride of the vertex declaration the
/// loader hands the hardware. The loader fills in components the format word
/// leaves unset, so neither stride is readable straight off the word.
/// </summary>
public static class VertexFormat
{
    /// <summary>
    /// Disk-stride contribution of each component slot when the loader fills
    /// in a component the format word leaves unset. Slot j is format-word
    /// bit (j + 12). Slots in order: POSITION0 f3, POSITION0 f4 with weight,
    /// NORMAL0, NORMAL0 alternate, COLOR0, TEXCOORD0 to TEXCOORD2, TEXCOORD4,
    /// TEXCOORD3, POSITION1 to POSITION6 alternating float4 and SHORT4N, TANGENT0.
    /// </summary>
    public static readonly int[] FillSize =
    {
        0, 0, 8, 0, 4, 4, 4, 4, 0, 4, 16, 8, 16, 8, 16, 8, 8,
    };

    /// <summary>
    /// Per-slot byte size on the GPU side of the vertex declaration.
    /// </summary>
    public static readonly int[] GpuSize =
    {
        12, 16, 8, 8, 4, 8, 4, 8, 4, 4, 16, 8, 16, 8, 16, 8, 8,
    };

    /// <summary>
    /// Skinned format word emitted by the importer. Its low 12 bits are the
    /// 100-byte disk stride, and both stride paths agree on that figure.
    /// </summary>
    public const uint Skinned = 0x13EFA064;

    /// <summary>
    /// Rigid format word: POSITION0 and NORMAL0 declared, the rest filled in
    /// by the loader for a 48-byte stride.
    /// </summary>
    public const uint Rigid = 0x00005014;

    /// <summary>
    /// Format word shipped files use for eye vertex arrays. It resolves to
    /// the same stride and declaration as <see cref="Skinned"/>, which staged
    /// batches write instead.
    /// </summary>
    public const uint Eye = 0x000EA024u;

    /// <summary>
    /// Resolves a type-5 format word into the disk stride it implies and the
    /// vertex declaration word left after unset components are filled in and
    /// the redundant skinned slots stripped.
    /// </summary>
    public static (int Stride, uint DeclWord) ComputeStrideAndDecl(uint formatWord)
    {
        int stride = (int)(formatWord & 0xFFF);
        uint fmt = formatWord;
        bool skinned = (fmt & (1u << 13)) != 0;

        void Fill(int j) { fmt |= 1u << (j + 12); stride += FillSize[j]; }

        if ((formatWord & (1u << 14)) == 0 && (formatWord & (1u << 15)) == 0) Fill(2);
        if ((formatWord & (1u << 16)) == 0) Fill(4);
        if ((formatWord & (1u << 17)) == 0) Fill(5);
        if ((formatWord & (1u << 18)) == 0) Fill(6);
        if ((formatWord & (1u << 19)) == 0) Fill(7);
        if ((formatWord & (1u << 21)) == 0) Fill(9);
        if ((formatWord & (1u << 28)) == 0) Fill(16);
        if (skinned)
        {
            if ((formatWord & (1u << 22)) == 0) Fill(10);
            if ((formatWord & (1u << 23)) == 0) Fill(11);
            if ((formatWord & (1u << 24)) == 0) Fill(12);
            if ((formatWord & (1u << 25)) == 0) Fill(13);
            if ((formatWord & (1u << 26)) == 0) Fill(14);
            if ((formatWord & (1u << 27)) == 0) Fill(15);
        }

        uint decl = fmt & 0xFFFFF000;
        if (skinned)
        {
            if ((fmt & (1u << 26)) != 0) { decl &= ~(1u << 26); stride -= 16; }
            if ((decl & (1u << 27)) != 0) { decl &= ~(1u << 27); stride -= 8; }
        }
        return (stride, decl);
    }

    /// <summary>
    /// GPU stream stride for a declaration word: the sum of
    /// <see cref="GpuSize"/> over its set bits. Slots 6 and 9 contribute
    /// nothing, and bit 21 implies bit 28.
    /// </summary>
    public static int DeclStride(uint declWord)
    {
        if ((declWord & (1u << 21)) != 0) declWord |= 1u << 28;
        int stride = 0;
        for (int j = 0; j <= 16; j++)
        {
            if (j == 6 || j == 9) continue;
            if ((declWord & (1u << (j + 12))) != 0) stride += GpuSize[j];
        }
        return stride;
    }
}
