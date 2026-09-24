using System.Text;
using Microsoft.Extensions.Logging;
using ShadowForge.Formats.HDB.Raw;
using ShadowForge.IO;

namespace ShadowForge.Formats.HDB;

/// <summary>
/// Writes a readable dump of a RawModel: header, first-table entries decoded by type,
/// second table, and IA/VA block summaries. Render command streams are also shown as
/// big-endian u16 words grouped by opcode, per docs/superpowers/hdb/render-commands.md.
/// </summary>
public static class Inspector
{
    public static void Dump(RawModel raw, ILogger log)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, raw);
        WriteFirstTableSummary(sb, raw);
        WriteFirstTableEntries(sb, raw);
        WriteSecondTable(sb, raw);
        WriteIndexBlocks(sb, raw);
        WriteVertexArrays(sb, raw);
        WriteTrailing(sb, raw);

        foreach (var line in sb.ToString().Split('\n'))
            log.LogInformation("{Line}", line.TrimEnd('\r'));
    }

    private static void WriteHeader(StringBuilder sb, RawModel raw)
    {
        var h = raw.Header;
        sb.AppendLine("=== Header ===");
        sb.AppendLine($"  Magic            0x{h.Magic:X8}  ({MagicAscii(h.Magic)})");
        sb.AppendLine($"  Flags            0x{h.Flags:X8}");
        sb.AppendLine($"  FileSizeHint     0x{h.FileSizeHint:X8}  ({h.FileSizeHint})");
        sb.AppendLine($"  FormatVersion    {h.FormatVersion}");
        sb.AppendLine($"  Reserved10       0x{h.Reserved10:X8}");
        sb.AppendLine($"  FirstTableOffset 0x{h.FirstTableOffset:X8}");
        sb.AppendLine($"  Reserved18       0x{h.Reserved18:X8}");
        sb.AppendLine($"  Reserved1C       0x{h.Reserved1C:X8}");
        sb.AppendLine();
    }

    private static void WriteFirstTableSummary(StringBuilder sb, RawModel raw)
    {
        sb.AppendLine($"=== First Table ({raw.FirstTable.Count} entries) ===");

        var hist = new SortedDictionary<int, int>();
        foreach (var e in raw.FirstTable)
            hist[e.DiskEntryType] = hist.GetValueOrDefault(e.DiskEntryType) + 1;

        sb.Append("  Type histogram: ");
        bool first = true;
        foreach (var kv in hist)
        {
            if (!first) sb.Append(", ");
            sb.Append($"type {kv.Key}={kv.Value}");
            first = false;
        }
        sb.AppendLine();
        sb.AppendLine();
    }

    private static void WriteFirstTableEntries(StringBuilder sb, RawModel raw)
    {
        for (int i = 0; i < raw.FirstTable.Count; i++)
        {
            var e = raw.FirstTable[i];
            sb.AppendLine(
                $"  FT[{i,3}] type={e.DiskEntryType,2} ({TypeName(e.DiskEntryType)})  "
                + $"entry@0x{e.AbsoluteEntryPos:X4}  payload@0x{e.PayloadPos:X4}  len=0x{e.DiskLength:X3}");
            WriteEntryPayload(sb, e);
        }
        sb.AppendLine();
    }

    private static void WriteEntryPayload(StringBuilder sb, RawEntry e)
    {
        switch (e)
        {
            case RawPaddingEntry pad when pad.Payload.Length > 0:
                sb.AppendLine($"        padding: {pad.Payload.Length} bytes");
                break;
            case RawFaceCountEntry fc:
                sb.AppendLine($"        FaceCount={fc.Count}  IBHandleSlot=0x{fc.IndexBufferHandleSlot:X8}");
                break;
            case RawVASetupEntry vs:
                for (int r = 0; r < vs.Records.Count; r++)
                {
                    var rec = vs.Records[r];
                    sb.AppendLine(
                        $"        VA[{r}]  vertexCount={rec.VertexCount}  "
                        + $"format=0x{rec.FormatType:X8}  offset=0x{rec.Offset:X}");
                }
                break;
            case RawModelEntry m:
                sb.AppendLine(
                    $"        Model  renderCommandPtr={m.RenderCommandPtr}  indexBlockCount={m.IndexBlockCount}  "
                    + $"indexTablePtr={m.IndexTablePtr}");
                sb.AppendLine(
                    $"               vertexArrayCount={m.VertexArrayCount}  vaSetupPtr={m.VASetupPtr}");
                sb.AppendLine(
                    $"               sphereCenter=({m.SphereCenter.X}, {m.SphereCenter.Y}, {m.SphereCenter.Z})  "
                    + $"sphereRadius=0x{m.SphereRadiusBits:X8}");
                break;
            case RawBoneEntry b:
                sb.AppendLine(
                    $"        Bone[{b.Index}] \"{b.Name}\"  HFlag=0x{b.HFlag:X8}  "
                    + $"PackedFlags0C=0x{b.PackedFlags0C:X8}");
                sb.AppendLine(
                    $"                  pos=({b.Position.X}, {b.Position.Y}, {b.Position.Z})  "
                    + $"euler=({b.Euler.X}, {b.Euler.Y}, {b.Euler.Z})");
                sb.AppendLine(
                    $"                  scale=({b.Scale.X}, {b.Scale.Y}, {b.Scale.Z})  "
                    + $"childIdx={b.ChildIndex}  nextSiblingFt={b.NextSiblingFtPos}");
                if (b.TrailingBytes.Length > 0)
                    sb.AppendLine($"                  trailing={b.TrailingBytes.Length} bytes");
                break;
            case RawTextureTableEntry t:
                for (int r = 0; r < t.Records.Count; r++)
                {
                    var rec = t.Records[r];
                    sb.AppendLine(
                        $"        Tex[{r}] \"{rec.Name}\"  "
                        + $"FloatParam={rec.FloatParam}  FlagField18=0x{rec.FlagField18:X8}");
                }
                break;
            case RawTextureCountEntry tc:
                sb.AppendLine(
                    $"        TextureCount={tc.Count}  OffsetToTextureTable=0x{tc.OffsetToTextureTable:X8}");
                break;
            case RawUnknownEntry9 u9:
                for (int r = 0; r < u9.Records.Count; r++)
                {
                    var rec = u9.Records[r];
                    sb.Append("        U9[" + r + "]  ");
                    for (int k = 0; k < rec.Length; k++)
                        sb.Append($"{rec[k]:X8} ");
                    sb.AppendLine();
                }
                break;
            case RawUnknownEntry12 u12:
                sb.AppendLine($"        U12 payload {u12.Payload.Length} bytes: {HexLine(u12.Payload, 32)}");
                break;
            case RawUnknownGenericEntry ug:
                sb.AppendLine(
                    $"        type-{e.DiskEntryType} opaque payload {ug.Payload.Length} bytes: "
                    + HexLine(ug.Payload, 32));
                break;
            case RawRenderCommandEntry rc:
                WriteRenderCommands(sb, rc);
                break;
        }
    }

    private static void WriteRenderCommands(StringBuilder sb, RawRenderCommandEntry rc)
    {
        var bytes = RenderCommandStream.Write(rc.Commands);
        sb.AppendLine($"        RC stream: {bytes.Length} bytes");
        sb.AppendLine($"          bytes: {HexLine(bytes, bytes.Length)}");
        sb.Append("          words: ");
        for (int k = 0; k + 1 < bytes.Length; k += 2)
        {
            int w = BigEndian.ReadUInt16(bytes, k);
            sb.Append($"{w:X4} ");
        }
        sb.AppendLine();

        sb.AppendLine("          decode (uint16 BE words, group meaning per docs/superpowers/hdb/render-commands.md):");
        for (int k = 0; k + 1 < bytes.Length;)
        {
            int w = BigEndian.ReadUInt16(bytes, k);
            string group = GroupName(w);
            int extraWords = ExtraWordsConsumed(w);
            sb.Append($"            @+{k:X3}  0x{w:X4}  {group}");
            if (extraWords > 0)
            {
                sb.Append("  data:");
                for (int e = 1; e <= extraWords && k + 2 * e + 1 < bytes.Length; e++)
                {
                    int dw = BigEndian.ReadUInt16(bytes, k + 2 * e);
                    sb.Append($" 0x{dw:X4}");
                }
            }
            sb.AppendLine();
            k += 2 * (1 + extraWords);
        }
    }

    private static string GroupName(int word)
    {
        if (word == 0) return "padding (skipped)";
        if (word == 0x00FF) return "END";

        int top = word & 0xF000;
        int second = word & 0x0F00;
        int low = word & 0x00FF;

        switch (top)
        {
            case 0x0000:
                if (second == 0x0200)
                    return $"matrix palette (count={low}, {low} bone-index words follow)";
                if (second == 0x0100 || second == 0x0300 || second == 0x0400
                    || second == 0x0600 || second == 0x0700 || second == 0x0800 || second == 0x0900)
                    return $"group 0x0  (sub-opcode 0x{second >> 8:X1}, byte param 0x{low:X2})";
                return "group 0x0 (default branch)";
            case 0x1000:
            case 0x2000:
            case 0x3000:
                return $"IA-select (group 0x{top >> 12:X}, draw)";
            case 0x4000:
                return "VA-select / depth-stencil";
            case 0x5000:
                return $"submesh marker / SetFrontBuffer (data=0x{low:X2})";
            case 0x6000:
                return $"material lookup  key=0x{word:X4}, stage={second >> 8}";
            case 0x9000:
                return GroupName9(second);
            case 0xE000:
                return $"scene-node selector  index=0x{word & 0x0FFF:X3}";
            case 0xF000:
                return "group 0xF (data word, e.g. trailing of 0x90FF)";
            default:
                return $"group 0x{top >> 12:X} (undecoded)";
        }
    }

    private static string GroupName9(int second)
    {
        if (second == 0x0000) return "color setter (0x90, RGB triple in 1 trailing word)";
        if (second == 0x0300) return "color setter (0x93, RGB triple in 1 trailing word)";
        if (second == 0x0400) return "color setter (0x94, RGBA in 2 trailing words)";
        return $"color setter (0x9{second >> 8:X1}, undecoded)";
    }

    /// <summary>
    /// Data words the runtime consumes after this opcode word. Unknown groups count as
    /// zero so the dump stays aligned.
    /// </summary>
    private static int ExtraWordsConsumed(int word)
    {
        if ((word & 0xFF00) == 0x0200) return word & 0xFF;

        int second = word & 0x0F00;
        return (word & 0xF000) switch
        {
            0x1000 or 0x2000 or 0x3000 => 2,
            0x4000 => 1,
            0x9000 when second == 0x0400 => 2,
            0x9000 when second is 0x0000 or 0x0300 => 1,
            _ => 0,
        };
    }

    private static void WriteSecondTable(StringBuilder sb, RawModel raw)
    {
        var st = raw.SecondTable;
        int neg = 0, pos = 0, zero = 0;
        foreach (var v in st.Entries)
        {
            if (v < 0) neg++;
            else if (v > 0) pos++;
            else zero++;
        }
        sb.AppendLine($"=== Second Table ({st.Entries.Length} entries) ===");
        sb.AppendLine($"  Padding 0x{st.Padding:X8}");
        sb.AppendLine($"  Sign distribution: negative={neg}  positive={pos}  zero={zero}");
        if (st.Entries.Length > 0)
        {
            sb.Append("  Values:");
            for (int i = 0; i < st.Entries.Length; i++)
            {
                if (i % 8 == 0) { sb.AppendLine(); sb.Append($"    [{i,3}]"); }
                sb.Append($" {st.Entries[i],7}");
            }
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    private static void WriteIndexBlocks(StringBuilder sb, RawModel raw)
    {
        if (raw.IndexBlocks.Count == 0) { sb.AppendLine("=== Index Blocks: none ==="); sb.AppendLine(); return; }
        sb.AppendLine($"=== Index Blocks ({raw.IndexBlocks.Count}) ===");
        for (int i = 0; i < raw.IndexBlocks.Count; i++)
        {
            var ib = raw.IndexBlocks[i];
            sb.AppendLine(
                $"  IA[{i}]  byteSize=0x{ib.ByteSize:X}  indices={ib.Indices.Length}  "
                + $"trailingPad={ib.TrailingPad.Length}");
        }
        sb.AppendLine();
    }

    private static void WriteVertexArrays(StringBuilder sb, RawModel raw)
    {
        if (raw.VertexArrays.Count == 0) { sb.AppendLine("=== Vertex Arrays: none ==="); sb.AppendLine(); return; }
        sb.AppendLine($"=== Vertex Arrays ({raw.VertexArrays.Count}) ===");
        for (int i = 0; i < raw.VertexArrays.Count; i++)
        {
            var va = raw.VertexArrays[i];
            sb.AppendLine(
                $"  VA[{i}]  format=0x{va.FormatType:X8}  vertexCount={va.VertexCount}  "
                + $"byteSize=0x{va.ByteSize:X}");
        }
        sb.AppendLine();
    }

    private static void WriteTrailing(StringBuilder sb, RawModel raw)
    {
        if (raw.PreIaPadding.Length > 0)
            sb.AppendLine($"PreIaPadding: {raw.PreIaPadding.Length} bytes");
        if (raw.TrailingBytes.Length > 0)
            sb.AppendLine($"TrailingBytes: {raw.TrailingBytes.Length} bytes");
    }

    private static string MagicAscii(uint magic)
    {
        var bytes = new[]
        {
            (byte)((magic >> 24) & 0xFF),
            (byte)((magic >> 16) & 0xFF),
            (byte)((magic >> 8) & 0xFF),
            (byte)(magic & 0xFF),
        };
        var sb = new StringBuilder();
        sb.Append('"');
        foreach (var b in bytes)
            sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
        sb.Append('"');
        return sb.ToString();
    }

    private static string TypeName(int t) => t switch
    {
        0 => "padding",
        3 => "render-cmd",
        4 => "face-count",
        5 => "VA-setup",
        6 => "model",
        7 => "bone",
        9 => "scene-graph",
        10 => "tex-table",
        11 => "tex-count",
        12 => "unknown-12",
        13 => "leading",
        _ => "?",
    };

    private static string HexLine(byte[] data, int max)
    {
        var sb = new StringBuilder();
        int n = Math.Min(data.Length, max);
        for (int i = 0; i < n; i++) sb.Append($"{data[i]:X2}");
        if (data.Length > max) sb.Append($"... (+{data.Length - max} bytes)");
        return sb.ToString();
    }
}
