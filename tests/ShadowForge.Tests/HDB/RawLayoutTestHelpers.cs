using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Raw;

namespace ShadowForge.Tests.HDB;

internal static class RawLayoutTestHelpers
{
    /// <summary>
    /// Zeroes the five fields RawLayout.Compute sets, so a read model looks freshly built.
    /// </summary>
    internal static void StripLayout(RawModel raw)
    {
        raw.Header.FirstTableOffset = 0;
        foreach (var entry in raw.FirstTable)
        {
            entry.AbsoluteEntryPos = 0;
            entry.DiskEntryType = 0;
            entry.DiskOffset = 0;
            entry.DiskLength = 0;
        }
    }

    /// <summary>
    /// Field-by-field equality of everything StripLayout leaves. Returns false with the
    /// first differing field in <paramref name="diff"/>.
    /// </summary>
    internal static bool SemanticEqual(RawModel a, RawModel b, out string? diff)
    {
        if (a.Header.Magic != b.Header.Magic) { diff = $"Header.Magic {a.Header.Magic:X} != {b.Header.Magic:X}"; return false; }
        if (a.Header.Flags != b.Header.Flags) { diff = $"Header.Flags {a.Header.Flags:X} != {b.Header.Flags:X}"; return false; }
        if (a.Header.FileSizeHint != b.Header.FileSizeHint) { diff = $"Header.FileSizeHint"; return false; }
        if (a.Header.FormatVersion != b.Header.FormatVersion) { diff = $"Header.FormatVersion"; return false; }
        if (a.Header.Reserved10 != b.Header.Reserved10) { diff = $"Header.Reserved10"; return false; }
        if (a.Header.Reserved18 != b.Header.Reserved18) { diff = $"Header.Reserved18"; return false; }
        if (a.Header.Reserved1C != b.Header.Reserved1C) { diff = $"Header.Reserved1C"; return false; }

        if (a.FirstTable.Count != b.FirstTable.Count) { diff = $"FirstTable.Count {a.FirstTable.Count} != {b.FirstTable.Count}"; return false; }
        for (int i = 0; i < a.FirstTable.Count; i++)
        {
            var ea = a.FirstTable[i];
            var eb = b.FirstTable[i];
            if (ea.GetType() != eb.GetType()) { diff = $"FirstTable[{i}] type {ea.GetType().Name} != {eb.GetType().Name}"; return false; }
            if (!EntryEqual(ea, eb, i, out diff)) return false;
        }

        if (a.SecondTable.Entries.Length != b.SecondTable.Entries.Length) { diff = $"SecondTable.Entries.Length"; return false; }
        for (int i = 0; i < a.SecondTable.Entries.Length; i++)
            if (a.SecondTable.Entries[i] != b.SecondTable.Entries[i]) { diff = $"SecondTable.Entries[{i}]"; return false; }
        if (a.SecondTable.Padding != b.SecondTable.Padding) { diff = "SecondTable.Padding"; return false; }

        if (a.IndexBlocks.Count != b.IndexBlocks.Count) { diff = $"IndexBlocks.Count"; return false; }
        for (int i = 0; i < a.IndexBlocks.Count; i++)
        {
            var ia = a.IndexBlocks[i];
            var ib = b.IndexBlocks[i];
            if (ia.ByteSize != ib.ByteSize) { diff = $"IndexBlocks[{i}].ByteSize"; return false; }
            if (ia.ByteSizeDuplicate != ib.ByteSizeDuplicate) { diff = $"IndexBlocks[{i}].ByteSizeDuplicate"; return false; }
            if (ia.Reserved08 != ib.Reserved08) { diff = $"IndexBlocks[{i}].Reserved08"; return false; }
            if (ia.Reserved0C != ib.Reserved0C) { diff = $"IndexBlocks[{i}].Reserved0C"; return false; }
            if (ia.Indices.Length != ib.Indices.Length) { diff = $"IndexBlocks[{i}].Indices.Length"; return false; }
            for (int k = 0; k < ia.Indices.Length; k++)
                if (ia.Indices[k] != ib.Indices[k]) { diff = $"IndexBlocks[{i}].Indices[{k}]"; return false; }
            if (!ia.TrailingPad.SequenceEqual(ib.TrailingPad)) { diff = $"IndexBlocks[{i}].TrailingPad"; return false; }
        }

        if (a.VertexArrays.Count != b.VertexArrays.Count) { diff = $"VertexArrays.Count"; return false; }
        for (int i = 0; i < a.VertexArrays.Count; i++)
        {
            var va = a.VertexArrays[i];
            var vb = b.VertexArrays[i];
            if (va.ByteSize != vb.ByteSize) { diff = $"VertexArrays[{i}].ByteSize"; return false; }
            if (va.FormatType != vb.FormatType) { diff = $"VertexArrays[{i}].FormatType"; return false; }
            if (va.VertexCount != vb.VertexCount) { diff = $"VertexArrays[{i}].VertexCount"; return false; }
            if (va.Reserved0C != vb.Reserved0C) { diff = $"VertexArrays[{i}].Reserved0C"; return false; }
            if (!va.RawBytes.SequenceEqual(vb.RawBytes)) { diff = $"VertexArrays[{i}].RawBytes"; return false; }
        }

        if (!a.PreIaPadding.SequenceEqual(b.PreIaPadding)) { diff = "PreIaPadding"; return false; }
        if (!a.TrailingBytes.SequenceEqual(b.TrailingBytes)) { diff = "TrailingBytes"; return false; }

        diff = null;
        return true;
    }

    private static bool EntryEqual(RawEntry a, RawEntry b, int idx, out string? diff)
    {
        switch (a)
        {
            case RawPaddingEntry pa when b is RawPaddingEntry pb:
                if (!pa.Payload.SequenceEqual(pb.Payload)) { diff = $"FirstTable[{idx}] Padding.Payload"; return false; }
                break;

            case RawFaceCountEntry fa when b is RawFaceCountEntry fb:
                if (fa.Count != fb.Count) { diff = $"FirstTable[{idx}] FaceCount.Count"; return false; }
                if (fa.IndexBufferHandleSlot != fb.IndexBufferHandleSlot) { diff = $"FirstTable[{idx}] FaceCount.IndexBufferHandleSlot"; return false; }
                break;

            case RawTextureCountEntry ta when b is RawTextureCountEntry tb:
                if (ta.Count != tb.Count) { diff = $"FirstTable[{idx}] TextureCount.Count"; return false; }
                if (ta.OffsetToTextureTable != tb.OffsetToTextureTable) { diff = $"FirstTable[{idx}] TextureCount.OffsetToTextureTable"; return false; }
                break;

            case RawUnknownEntry12 u12a when b is RawUnknownEntry12 u12b:
                if (!u12a.Payload.SequenceEqual(u12b.Payload)) { diff = $"FirstTable[{idx}] Unknown12.Payload"; return false; }
                break;

            case RawUnknownGenericEntry ga when b is RawUnknownGenericEntry gb:
                if (!ga.Payload.SequenceEqual(gb.Payload)) { diff = $"FirstTable[{idx}] UnknownGeneric.Payload"; return false; }
                break;

            case RawTextureTableEntry tta when b is RawTextureTableEntry ttb:
                if (tta.Records.Count != ttb.Records.Count) { diff = $"FirstTable[{idx}] TextureTable.Records.Count"; return false; }
                for (int r = 0; r < tta.Records.Count; r++)
                {
                    if (tta.Records[r].Name != ttb.Records[r].Name) { diff = $"FirstTable[{idx}] TextureTable.Records[{r}].Name"; return false; }
                    if (tta.Records[r].FloatParam != ttb.Records[r].FloatParam) { diff = $"FirstTable[{idx}] TextureTable.Records[{r}].FloatParam"; return false; }
                    if (tta.Records[r].FlagField18 != ttb.Records[r].FlagField18) { diff = $"FirstTable[{idx}] TextureTable.Records[{r}].FlagField18"; return false; }
                }
                break;

            case RawVASetupEntry va when b is RawVASetupEntry vb:
                if (va.Records.Count != vb.Records.Count) { diff = $"FirstTable[{idx}] VASetup.Records.Count"; return false; }
                for (int r = 0; r < va.Records.Count; r++)
                {
                    if (va.Records[r].VertexCount != vb.Records[r].VertexCount) { diff = $"FirstTable[{idx}] VASetup.Records[{r}].VertexCount"; return false; }
                    if (va.Records[r].FormatType != vb.Records[r].FormatType) { diff = $"FirstTable[{idx}] VASetup.Records[{r}].FormatType"; return false; }
                    if (va.Records[r].Offset != vb.Records[r].Offset) { diff = $"FirstTable[{idx}] VASetup.Records[{r}].Offset"; return false; }
                }
                break;

            case RawRenderCommandEntry ra when b is RawRenderCommandEntry rb:
                if (ra.Commands.Count != rb.Commands.Count) { diff = $"FirstTable[{idx}] RenderCommand.Commands.Count"; return false; }
                for (int c = 0; c < ra.Commands.Count; c++)
                {
                    if (ra.Commands[c].Opcode != rb.Commands[c].Opcode) { diff = $"FirstTable[{idx}] RenderCommand.Commands[{c}].Opcode"; return false; }
                    if (!ra.Commands[c].Data.SequenceEqual(rb.Commands[c].Data)) { diff = $"FirstTable[{idx}] RenderCommand.Commands[{c}].Data"; return false; }
                }
                break;

            case RawModelEntry pa when b is RawModelEntry pb:
                if (pa.RenderCommandPtr != pb.RenderCommandPtr) { diff = $"FirstTable[{idx}] Model.RenderCommandPtr"; return false; }
                if (pa.IndexBlockCount != pb.IndexBlockCount) { diff = $"FirstTable[{idx}] Model.IndexBlockCount"; return false; }
                if (pa.IndexTablePtr != pb.IndexTablePtr) { diff = $"FirstTable[{idx}] Model.IndexTablePtr"; return false; }
                if (pa.VertexArrayCount != pb.VertexArrayCount) { diff = $"FirstTable[{idx}] Model.VertexArrayCount"; return false; }
                if (pa.VASetupPtr != pb.VASetupPtr) { diff = $"FirstTable[{idx}] Model.VASetupPtr"; return false; }
                if (pa.SphereCenter != pb.SphereCenter) { diff = $"FirstTable[{idx}] Model.SphereCenter"; return false; }
                if (pa.SphereRadiusBits != pb.SphereRadiusBits) { diff = $"FirstTable[{idx}] Model.SphereRadiusBits"; return false; }
                break;

            case RawBoneEntry ba when b is RawBoneEntry bb:
                if (ba.Index != bb.Index) { diff = $"FirstTable[{idx}] Bone.Index"; return false; }
                if (ba.Reserved04 != bb.Reserved04) { diff = $"FirstTable[{idx}] Bone.Reserved04"; return false; }
                if (ba.HFlag != bb.HFlag) { diff = $"FirstTable[{idx}] Bone.HFlag"; return false; }
                if (ba.PackedFlags0C != bb.PackedFlags0C) { diff = $"FirstTable[{idx}] Bone.PackedFlags0C"; return false; }
                if (ba.Position != bb.Position) { diff = $"FirstTable[{idx}] Bone.Position"; return false; }
                if (ba.Euler != bb.Euler) { diff = $"FirstTable[{idx}] Bone.Euler"; return false; }
                if (ba.Reserved28 != bb.Reserved28) { diff = $"FirstTable[{idx}] Bone.Reserved28"; return false; }
                if (ba.Scale != bb.Scale) { diff = $"FirstTable[{idx}] Bone.Scale"; return false; }
                if (ba.ChildIndex != bb.ChildIndex) { diff = $"FirstTable[{idx}] Bone.ChildIndex"; return false; }
                if (ba.NextSiblingFtPos != bb.NextSiblingFtPos) { diff = $"FirstTable[{idx}] Bone.NextSiblingFtPos"; return false; }
                if (ba.Name != bb.Name) { diff = $"FirstTable[{idx}] Bone.Name"; return false; }
                for (int e = 0; e < 6; e++)
                    if (ba.ExtraEuler[e] != bb.ExtraEuler[e]) { diff = $"FirstTable[{idx}] Bone.ExtraEuler[{e}]"; return false; }
                if (!ba.TrailingBytes.SequenceEqual(bb.TrailingBytes)) { diff = $"FirstTable[{idx}] Bone.TrailingBytes"; return false; }
                break;

            case RawUnknownEntry9 u9a when b is RawUnknownEntry9 u9b:
                if (u9a.Records.Count != u9b.Records.Count) { diff = $"FirstTable[{idx}] Unknown9.Records.Count"; return false; }
                for (int r = 0; r < u9a.Records.Count; r++)
                {
                    if (u9a.Records[r].Length != u9b.Records[r].Length) { diff = $"FirstTable[{idx}] Unknown9.Records[{r}].Length"; return false; }
                    for (int f = 0; f < u9a.Records[r].Length; f++)
                        if (u9a.Records[r][f] != u9b.Records[r][f]) { diff = $"FirstTable[{idx}] Unknown9.Records[{r}][{f}]"; return false; }
                }
                break;

            default:
                diff = $"FirstTable[{idx}] unhandled pair types {a.GetType().Name}/{b.GetType().Name}";
                return false;
        }

        diff = null;
        return true;
    }

    /// <summary>
    /// Asserts two cooked models are equal, floats within 1e-6.
    /// </summary>
    internal static void AssertCookedEquivalent(ModelFile a, ModelFile b)
    {
        Assert.Equal(a.Bones.Count, b.Bones.Count);
        for (int i = 0; i < a.Bones.Count; i++)
        {
            var ba = a.Bones[i];
            var bb = b.Bones[i];
            Assert.Equal(ba.Index, bb.Index);
            Assert.Equal(ba.Name, bb.Name);
            Assert.Equal(ba.ParentIndex, bb.ParentIndex);
            Assert.Equal(ba.ChildIndex, bb.ChildIndex);
            Assert.Equal(ba.HFlag, bb.HFlag);
            Assert.Equal(ba.PosX, bb.PosX, 1e-6);
            Assert.Equal(ba.PosY, bb.PosY, 1e-6);
            Assert.Equal(ba.PosZ, bb.PosZ, 1e-6);
            Assert.Equal(ba.EulerX, bb.EulerX, 1e-6);
            Assert.Equal(ba.EulerY, bb.EulerY, 1e-6);
            Assert.Equal(ba.EulerZ, bb.EulerZ, 1e-6);
            Assert.Equal(ba.ScaleX, bb.ScaleX, 1e-6);
            Assert.Equal(ba.ScaleY, bb.ScaleY, 1e-6);
            Assert.Equal(ba.ScaleZ, bb.ScaleZ, 1e-6);
            for (int e = 0; e < 6; e++)
                Assert.Equal(ba.ExtraEuler[e], bb.ExtraEuler[e], 1e-6);
        }

        Assert.Equal(a.Textures.Count, b.Textures.Count);
        for (int i = 0; i < a.Textures.Count; i++)
            Assert.Equal(a.Textures[i].Name, b.Textures[i].Name);
        Assert.Equal(a.TextureCount, b.TextureCount);

        Assert.Equal(a.VertexArrays.Count, b.VertexArrays.Count);
        for (int i = 0; i < a.VertexArrays.Count; i++)
        {
            Assert.Equal(a.VertexArrays[i].VAType, b.VertexArrays[i].VAType);
            Assert.Equal(a.VertexArrays[i].VertexCount, b.VertexArrays[i].VertexCount);
            Assert.True(a.VertexArrays[i].RawVertices.SequenceEqual(b.VertexArrays[i].RawVertices));
        }

        Assert.Equal(a.IndexArrays.Count, b.IndexArrays.Count);
        for (int i = 0; i < a.IndexArrays.Count; i++)
        {
            Assert.Equal(a.IndexArrays[i].MaterialIndex, b.IndexArrays[i].MaterialIndex);
            Assert.True(a.IndexArrays[i].Indices.SequenceEqual(b.IndexArrays[i].Indices));
        }

        Assert.Equal(a.MeshGroups.Count, b.MeshGroups.Count);
    }
}
