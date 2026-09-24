using System.Text;
using ShadowForge.IO;
using ShadowForge.Formats.HDB.Raw;
using ShadowForge.Formats.HDB.Wire;

namespace ShadowForge.Formats.HDB.Import;

/// <summary>
/// Walks an HDB byte image the way the runtime does and reports everything that would
/// parse wrong, draw wrong, or hang. Retail files must validate clean, so an error on a
/// retail file is a validator bug.
/// </summary>
public sealed class ModelValidator
{
    public enum Level { Info, Warning, Error }

    public readonly record struct Finding(Level Level, string Message);

    public List<Finding> Findings { get; } = new();
    public StringBuilder Dump { get; } = new();

    private byte[] _d = [];

    private readonly Dictionary<int, int> _rebased = new();

    private void Error(string m) => Findings.Add(new Finding(Level.Error, m));
    private void Warn(string m) => Findings.Add(new Finding(Level.Warning, m));
    private void Info(string m) => Findings.Add(new Finding(Level.Info, m));

    public bool HasErrors => Findings.Any(f => f.Level == Level.Error);

    private bool In(int offset, int size) => offset >= 0 && size >= 0 && offset + size <= _d.Length;

    private uint U32(int o) => BigEndian.ReadUInt32(_d, o);
    private int I32(int o) => BigEndian.ReadInt32(_d, o);
    private ushort U16(int o) => BigEndian.ReadUInt16(_d, o);
    private float F32(int o) => BigEndian.ReadFloat(_d, o);

    /// <summary>
    /// Resolves a pointer cell as the runtime sees it after rebasing. A zero cell is null.
    /// A cell some rebase slot names resolves to cell + value. A non-zero cell no slot
    /// names is an error, because the runtime would use the relative value as a pointer.
    /// </summary>
    private int? ResolvePtr(int cellOffset, string what)
    {
        if (!In(cellOffset, 4)) { Error($"{what}: pointer cell 0x{cellOffset:X} out of file"); return null; }
        int v = I32(cellOffset);
        if (v == 0) return null;
        if (!_rebased.TryGetValue(cellOffset, out int target))
        {
            Error($"{what}: cell 0x{cellOffset:X} holds 0x{v:X} but no rebase slot targets it; runtime would use a garbage pointer");
            return null;
        }
        return target;
    }

    public static ModelValidator Run(byte[] data)
    {
        var v = new ModelValidator { _d = data };
        v.Execute();
        return v;
    }

    private void Execute()
    {
        if (!In(0, 0x18)) { Error("file shorter than the 0x18-byte header"); return; }

        uint magic = U32(0);
        if (magic != RawHeader.MagicValue && magic != 0x40484442u)
            Error($"magic 0x{magic:X8} is neither BDH@ nor @HDB; runtime takes the endian-converting path");

        int p = RawHeader.FirstTableStart(U32(RawHeader.FirstTableOffsetField));
        if (!In(p, 8)) { Error($"first-table region P=0x{p:X} out of file"); return; }
        int ftCount = (int)U32(p);
        int q = p + 4 + I32(p + 4);
        Dump.AppendLine($"P=0x{p:X} ftCount={ftCount} Q=0x{q:X}");
        if (!In(q, 8)) { Error($"rebase table Q=0x{q:X} out of file"); return; }
        int slotCount = (int)U32(q);
        int r = q + 4 + I32(q + 4);
        if (!In(r, 4)) Error($"trailing region R=0x{r:X} out of file");
        else if (!In(r, (int)U32(r))) Warn($"R extent 0x{r:X}+0x{U32(r):X} exceeds file");
        Dump.AppendLine($"rebase slots={slotCount} R=0x{r:X} (len 0x{(In(r, 4) ? U32(r) : 0):X})");

        if (!In(q + 8, slotCount * 4)) { Error("rebase slot array out of file"); return; }
        for (int i = 0; i < slotCount; i++)
        {
            int slotAddr = q + 8 + i * 4;
            int cell = slotAddr + I32(slotAddr);
            if (!In(cell, 4)) { Error($"rebase slot {i}: cell 0x{cell:X} out of file"); continue; }
            int cellVal = I32(cell);
            if (cellVal == 0) continue;
            int target = cell + cellVal;
            if (!In(target, 1))
                Warn($"rebase slot {i}: cell 0x{cell:X} resolves to 0x{target:X}, outside the file");
            _rebased[cell] = target;
        }

        if (!In(p + RawLayout.FirstTableHeaderSize, ftCount * FirstTableEntry.Size)) { Error("first-table entries out of file"); return; }
        int type9 = -1, type11 = -1;
        var ftPayloads = new List<(uint Type, int Payload, int Length)>();
        for (int i = 0; i < ftCount; i++)
        {
            int e = p + RawLayout.FirstTableHeaderSize + i * FirstTableEntry.Size;
            uint type = U32(e);
            int len = (int)U32(e + 12);
            int cell = e + RawEntry.DataOffsetField;
            int payload = -1;
            if (I32(cell) != 0)
            {
                if (!_rebased.TryGetValue(cell, out payload))
                {
                    Error($"FT[{i}] type {type}: data cell 0x{cell:X} not covered by a rebase slot");
                    payload = cell + I32(cell);
                }
                if (!In(payload, len))
                    Error($"FT[{i}] type {type}: payload 0x{payload:X} len 0x{len:X} out of file");
            }
            ftPayloads.Add((type, payload, len));
            if (type == 9 && type9 < 0) type9 = i;
            if (type == 11 && type11 < 0) type11 = i;
        }
        Dump.AppendLine("FT types: " + string.Join(" ", ftPayloads.Select(f => f.Type)));

        int textureCount = 0;
        if (type11 >= 0)
        {
            int t11 = ftPayloads[type11].Payload;
            textureCount = (int)U32(t11);
            int? table = ResolvePtr(t11 + 4, "type11 table pointer");
            if (table is int t10)
            {
                if (!In(t10, textureCount * TextureData.Size))
                    Error($"texture table 0x{t10:X} x{textureCount} out of file");
                else
                    for (int i = 0; i < textureCount; i++)
                    {
                        int rec = t10 + i * TextureData.Size;
                        string name = ReadName(rec, TextureData.NameWidth);
                        Dump.AppendLine($"texture[{i}] '{name}' flag18=0x{U32(rec + 0x18):X}");
                    }
            }
            else if (textureCount > 0)
            {
                Error("type-11 declares textures but its table pointer is null/unrebased");
            }
        }

        if (type9 < 0)
        {
            Warn("no type-9 entry: file loads but no scene graph is built");
            return;
        }

        int t9 = ftPayloads[type9].Payload;
        if (!In(t9, RawUnknownEntry9.RecordSize)) { Error("type-9 payload out of file"); return; }
        uint ver = U32(t9);
        uint flags = U32(t9 + 4);
        if ((flags & 7) != 0 && (flags & 7) != 2)
            Error($"type9.u32[1]=0x{flags:X8}: (v & 7) must be 0 or 2 or the GPU walk is skipped");
        if ((flags & 0x10000) == 0)
            Error($"type9.u32[1]=0x{flags:X8}: bit 16 clear; GPU walk and physical path are skipped");
        if ((flags & 0xFFFE0000) != 0)
            Warn($"type9.u32[1] has bits 17+ preset (0x{flags:X8}); loader ORs the GPU buffer size into them");
        int budget = (int)U32(t9 + 20);
        int matrixCount = (int)U32(t9 + 12);
        Dump.AppendLine($"type9: ver=0x{ver:X} flags=0x{flags:X8} matrixCount={matrixCount} budget={budget}");

        int? listPtr = ResolvePtr(t9 + 8, "type9 u32[2] list pointer");
        int listCopyWords = 0;
        if (ver >= 0x300200 && listPtr is int lp)
        {
            if (!In(lp, 16)) Error($"type9 list 0x{lp:X} header out of file");
            else
            {
                listCopyWords = (int)U32(lp);
                if (!In(lp + 16, listCopyWords * 4))
                    Error($"type9 list data 0x{lp:X} x{listCopyWords} out of file");
            }
        }

        int? root = ResolvePtr(t9 + 16, "type9 u32[4] bone root pointer");
        if (root is not int rootOff)
        {
            Error("type9 u32[4] does not resolve; no scene tree, nothing renders");
            return;
        }

        int cursor = RawLayout.DataRegionStart(t9 + RawUnknownEntry9.RecordSize);
        Dump.AppendLine($"data region starts at 0x{cursor:X}");

        var nodes = new List<int>();
        var visited = new HashSet<int>();
        WalkTree(rootOff, nodes, visited, 0);
        Dump.AppendLine($"scene tree: {nodes.Count} nodes");

        var slotByIndex = new Dictionary<ushort, int>();
        var geometryNodes = new List<(int Node, int Type6)>();
        int nodeUse = 28;
        for (int slot = 0; slot < nodes.Count; slot++)
        {
            int n = nodes[slot];
            var boneFlags = (BoneFlags)U32(n + BoneData.FlagsOffset);
            uint index = U32(n + BoneData.IndexOffset);
            bool hasExtraEuler =
                (boneFlags & (BoneFlags.ExtraEulerA | BoneFlags.ExtraEulerB)) != 0;
            int recSize = hasExtraEuler
                ? BoneData.LongRecordSize
                : BoneData.ShortRecordSize;
            nodeUse += recSize;
            if (!In(n, recSize))
                Error($"node 0x{n:X}: flags 0x{(uint)boneFlags:X} implies {recSize}-byte record extending past file end");
            if (index != 0xFFFFFFFF)
            {
                if (index > 0xFFFF)
                    Error($"node 0x{n:X} Index {index} exceeds 0xFFFF; palette remap compares u16");
                else if (!slotByIndex.TryAdd((ushort)index, slot))
                    Warn($"duplicate bone Index {index}; palette remap uses the first visit");
            }
            if (boneFlags.HasFlag(BoneFlags.Model))
            {
                int? t6 = ResolvePtr(n + BoneData.ModelPtrOffset, $"node 0x{n:X} model pointer");
                if (t6 is int t6o)
                {
                    geometryNodes.Add((n, t6o));
                    nodeUse += ModelRecord.Size;
                }
                else
                {
                    Error($"node 0x{n:X}: HFlag bit 0x10000 set but model pointer unresolved");
                }
            }
            if (matrixCount > 0 && index != 0xFFFFFFFF && index >= (uint)matrixCount)
                Warn($"node 0x{n:X} Index {index} >= type9.u32[3]={matrixCount}");
        }

        var perNode = new List<(int Type6, List<(int Count, int DataOff)> Ibs, List<(int VertCount, uint Fmt, int DataOff, int Stride)> Vas)>();
        long gpuAlloc = 0;
        foreach (var (node, t6) in geometryNodes)
        {
            if (!In(t6, ModelRecord.Size)) { Error($"type6 0x{t6:X} out of file"); continue; }
            int ibCount = (int)U32(t6 + ModelRecord.IndexBlockCountOffset);
            int? ibTable = ResolvePtr(t6 + ModelRecord.IndexTablePtrOffset, "type6 IB table pointer");
            var ibs = new List<(int, int)>();
            if (ibTable is int ibt && ibCount > 0)
            {
                if (!In(ibt, ibCount * 8)) Error($"IB table 0x{ibt:X} x{ibCount} out of file");
                for (int i = 0; i < ibCount; i++)
                {
                    cursor = Align.Up(cursor, 4);
                    if (!In(cursor, IndexBlockHeader.Size)) { Error($"IB block header at 0x{cursor:X} out of file"); break; }
                    int blockBytes = (int)U32(cursor);
                    int dataOff = cursor + IndexBlockHeader.Size;
                    int declaredCount = In(ibt + i * 8, 4) ? (int)U32(ibt + i * 8) : -1;
                    if (declaredCount * 2 > blockBytes)
                        Error($"IB record {i}: type4 count {declaredCount} needs {declaredCount * 2} bytes but block at 0x{cursor:X} holds {blockBytes}");
                    if (!In(dataOff, blockBytes))
                        Error($"IB block data 0x{dataOff:X}+0x{blockBytes:X} out of file");
                    ibs.Add((declaredCount, dataOff));
                    gpuAlloc += Align.Up((long)(declaredCount * 2), 16L);
                    cursor = dataOff + blockBytes;
                }
            }
            nodeUse += 8 * ibs.Count;
            perNode.Add((t6, ibs, new List<(int, uint, int, int)>()));
        }
        for (int g = 0; g < geometryNodes.Count; g++)
        {
            int t6 = geometryNodes[g].Type6;
            if (!In(t6, ModelRecord.Size)) continue;
            int vaCount = (int)U32(t6 + ModelRecord.VertexArrayCountOffset);
            int? vaSetup = ResolvePtr(t6 + ModelRecord.VASetupPtrOffset, "type6 VA setup pointer");
            if (vaSetup is int vs && vaCount > 0)
            {
                nodeUse += 4;
                if (!In(vs + 4, vaCount * VASetupRecord.Size)) Error($"VA setup 0x{vs:X} x{vaCount} out of file");
                for (int i = 0; i < vaCount; i++)
                {
                    int rec = vs + 4 + i * VASetupRecord.Size;
                    int vertCount = (int)U32(rec);
                    uint fmt = U32(rec + 4);
                    var (stride, decl) = VertexFormat.ComputeStrideAndDecl(fmt);
                    int declStride = VertexFormat.DeclStride(decl);
                    if (stride != declStride)
                        Error($"VA record {i}: format 0x{fmt:X8} disk stride {stride} != GPU decl stride {declStride}; vertices misalign");
                    cursor = Align.Up(cursor, 4);
                    if (!In(cursor, VAHeaderData.Size)) { Error($"VA blob header at 0x{cursor:X} out of file"); break; }
                    int blobBytes = (int)U32(cursor);
                    int dataOff = cursor + VAHeaderData.Size;
                    if ((long)stride * vertCount > blobBytes)
                        Error($"VA record {i}: {vertCount} x {stride}B = {(long)stride * vertCount} exceeds blob size {blobBytes} at 0x{cursor:X}");
                    if (!In(dataOff, blobBytes))
                        Error($"VA blob data 0x{dataOff:X}+0x{blobBytes:X} out of file");
                    perNode[g].Vas.Add((vertCount, fmt, dataOff, stride));
                    gpuAlloc += Align.Up((long)(stride * vertCount), 16L);
                    nodeUse += VASetupRecord.Size;
                    cursor = dataOff + blobBytes;
                }
            }
        }
        long gpuRounded = Align.Up(gpuAlloc, 0x10000L);
        Dump.AppendLine($"GPU buffer: {gpuAlloc} -> {gpuRounded} bytes (encoded into type9.u32[1] bits 17+)");
        if (gpuRounded > 0xFFF0000L * 2)
            Error("GPU buffer size exceeds the 12-bit<<16 encoding range of type9.u32[1]");

        for (int g = 0; g < geometryNodes.Count; g++)
        {
            var (node, t6) = geometryNodes[g];
            if (!In(t6, ModelRecord.Size)) continue;
            float radius = F32(t6 + ModelRecord.SphereRadiusOffset);
            if (!(radius > 0f) || float.IsNaN(radius) || float.IsInfinity(radius))
                Error($"type6 0x{t6:X}: bounding sphere radius {radius} culls the model every frame");

            int? rc = ResolvePtr(t6 + ModelRecord.RenderCommandPtrOffset, "type6 RC stream pointer");
            if (rc is not int rcOff) { Error($"type6 0x{t6:X}: no render command stream"); continue; }

            nodeUse += ValidateRCStream(rcOff, perNode[g].Ibs, perNode[g].Vas, slotByIndex, textureCount, g);
        }

        nodeUse += 4 * listCopyWords;
        Dump.AppendLine($"scene node estimate: {nodeUse} of budget {budget} + 1024");
        if (nodeUse > budget + 1024)
            Error($"scene node use ~{nodeUse} exceeds type9.u32[5]+1024={budget + 1024}; the runtime hangs on overflow");
        else if (nodeUse > budget)
            Warn($"scene node use ~{nodeUse} exceeds u32[5]={budget}, inside the +1024 slack");
    }

    private void WalkTree(int node, List<int> order, HashSet<int> visited, int depth)
    {
        while (true)
        {
            if (depth > 512) { Error("bone tree deeper than 512; likely a pointer cycle"); return; }
            if (!visited.Add(node)) { Error($"bone tree revisits node 0x{node:X}; pointer cycle would hang or corrupt the walk"); return; }
            if (!In(node, BoneData.ShortRecordSize)) { Error($"bone node 0x{node:X} out of file"); return; }
            order.Add(node);
            int? child = ResolvePtr(node + BoneData.ChildPtrOffset, $"node 0x{node:X} child ptr");
            if (child is int c) WalkTree(c, order, visited, depth + 1);
            int? sib = ResolvePtr(node + BoneData.SiblingPtrOffset, $"node 0x{node:X} sibling ptr");
            if (sib is not int s) return;
            node = s;
        }
    }

    /// <summary>
    /// Walks one render-command stream under both the load-time copier grammar and the
    /// draw-time opcode semantics. Returns the scene-node bytes the copied stream uses:
    /// words copied * 2, aligned to 4.
    /// </summary>
    private int ValidateRCStream(
        int rcOff,
        List<(int Count, int DataOff)> ibs,
        List<(int VertCount, uint Fmt, int DataOff, int Stride)> vas,
        Dictionary<ushort, int> slotByIndex,
        int textureCount,
        int geoIndex)
    {
        int pos = rcOff;
        int wordsCopied = 0;
        int boundIB = -1;
        int boundVA = -1;
        int palCount = -1;
        int draws = 0;
        const int MaxWords = 1 << 20;

        ushort Next()
        {
            ushort w = U16(pos);
            pos += 2;
            wordsCopied++;
            return w;
        }

        while (true)
        {
            if (!In(pos, 2)) { Error($"RC stream 0x{rcOff:X}: ran off file end without 0x00FF terminator"); break; }
            if (wordsCopied > MaxWords) { Error($"RC stream 0x{rcOff:X}: no terminator within {MaxWords} words"); break; }
            ushort w = Next();
            if (w == 0x0000) continue;
            if (w == 0x00FF) break;

            uint group = (uint)(w & 0xF000);
            switch (group)
            {
                case 0x1000:
                case 0x2000:
                case 0x3000:
                {
                    ushort w1 = Next();
                    ushort w2 = Next();
                    if ((w & 0xF00) == 0x400) Next();
                    draws++;
                    int count = w1 + 2;
                    int start = w2;
                    if (boundIB < 0) { Error($"RC 0x{rcOff:X}: draw before any 0x5NNN index-buffer bind"); break; }
                    if (boundIB >= ibs.Count) break;
                    var ib = ibs[boundIB];
                    if (start + count > ib.Count)
                        Error($"RC 0x{rcOff:X}: draw [{start}..{start + count}) exceeds IB {boundIB} count {ib.Count}");
                    else if (boundVA >= 0 && boundVA < vas.Count)
                    {
                        int maxIdx = -1;
                        for (int i = 0; i < count; i++)
                        {
                            int idx = U16(ib.DataOff + (start + i) * 2);
                            if (idx == DrawTriangles.StripRestart) continue;
                            if (idx > maxIdx) maxIdx = idx;
                        }
                        if (maxIdx >= vas[boundVA].VertCount)
                            Error($"RC 0x{rcOff:X}: draw at index {start} references vertex {maxIdx} but VA {boundVA} has {vas[boundVA].VertCount}");
                    }
                    if (boundVA < 0) Error($"RC 0x{rcOff:X}: draw before any 0x4SSS vertex-array bind");
                    break;
                }
                case 0x4000:
                {
                    ushort vaIdx = Next();
                    if ((w & 0xF00) == 0x400) Next();
                    boundVA = vaIdx;
                    if (vaIdx >= vas.Count)
                        Error($"RC 0x{rcOff:X}: 0x4 selects VA {vaIdx} of {vas.Count}");
                    if ((w & 0xFFF) != 0)
                        Warn($"RC 0x{rcOff:X}: VA bind to stream slot {w & 0xFFF} (usually 0)");
                    break;
                }
                case 0x9000:
                {
                    Next();
                    if ((w & 0xF00) == 0x400) Next();
                    break;
                }
                case 0x5000:
                    boundIB = w & 0xFFF;
                    if (boundIB >= ibs.Count)
                        Error($"RC 0x{rcOff:X}: 0x5 selects IB {boundIB} of {ibs.Count}");
                    break;
                case 0x6000:
                {
                    var material = MaterialWord.Decode(w);
                    int stage = material.Stage;
                    int texIdx = material.TextureIndex;
                    if (stage > 2)
                    {
                        Error($"RC 0x{rcOff:X}: material word 0x{w:X4} uses stage {stage}; retail files use stages 0-2 only");
                    }
                    else if (textureCount == 0)
                    {
                        Error($"RC 0x{rcOff:X}: material word 0x{w:X4} but the file has no texture table");
                    }
                    else if (texIdx >= textureCount)
                    {
                        Error($"RC 0x{rcOff:X}: material word 0x{w:X4} (stage {stage}) indexes texture {texIdx} of {textureCount}");
                    }
                    break;
                }
                case 0x0000:
                {
                    if ((w & 0xF00) == 0x200)
                    {
                        palCount = w & 0xFF;
                        if (palCount > BatchBuilder.MaxPaletteEntries)
                            Error($"RC 0x{rcOff:X}: palette of {palCount} entries; more than 24 hard-hangs the renderer");
                        for (int i = 0; i < palCount; i++)
                        {
                            ushort boneIndex = Next();
                            if (!slotByIndex.ContainsKey(boneIndex))
                                Error($"RC 0x{rcOff:X}: palette entry {i} references bone Index {boneIndex}, not present in the scene tree; draw uses a garbage matrix");
                        }
                    }
                    break;
                }
            }
        }

        if (draws == 0) Warn($"RC stream 0x{rcOff:X} (geometry node {geoIndex}) issues no draws");
        Dump.AppendLine($"geo[{geoIndex}]: rc=0x{rcOff:X} words={wordsCopied} draws={draws} ibs={ibs.Count} vas={vas.Count}");
        return Align.Up(wordsCopied * 2, 4);
    }

    private string ReadName(int offset, int max)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < max && In(offset + i, 1); i++)
        {
            byte b = _d[offset + i];
            if (b == 0) break;
            sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '?');
        }
        return sb.ToString();
    }

}
