using System.Text;
using ShadowForge.Formats.RPJ.Wire;
using ShadowForge.IO;
using ShadowForge.Scene;
using ShadowForge.Scene.Script;

namespace ShadowForge.Formats.RPJ;

/// <summary>
/// Reads and writes the RPJ binary scene format. Layout: a 0x270-byte header, the
/// section table, then the entry pool, the script blocks and the waypoints.
/// Entry and block chain offsets are relative to the start of their own section.
/// </summary>
public static class BinaryScene
{
    private const int HeaderSize = 0x270;
    private const int EntryPoolStart = HeaderSize + SectionTableData.Size;

    private const int VersionOffset = 0x00;
    private const int VersionLength = 4;
    private const int FlagsOffset = 0x08;
    private const int SceneNameOffset = 0x0C;
    private const int SceneNameLength = 64;
    private const int FilenameOffset = 0x4C;
    private const int FilenameLength = 28;
    private const int BuildPathOffset = 0x6C;
    private const int BuildPathLength = 260;
    private const int MessagePathOffset = 0x170;
    private const int MessagePathLength = 256;

    private const int MaxEntryChain = 2000;
    private const int MaxBlockChain = 100;

    public static SceneFile Read(string path) => Read(File.ReadAllBytes(path));

    public static SceneFile Read(byte[] data)
    {
        var st = SectionTableData.Read(data.AsSpan(HeaderSize, SectionTableData.Size));
        var scene = new SceneFile
        {
            Version = Encoding.ASCII.GetString(data, VersionOffset, VersionLength).TrimEnd('\0'),
            Flags = BigEndian.ReadUInt32(data, FlagsOffset),
            SceneName = data.DecodeShiftJISFull(SceneNameOffset, SceneNameLength),
            Filename = data.DecodeShiftJISFull(FilenameOffset, FilenameLength),
            MessagePath = data.DecodeShiftJISFull(MessagePathOffset, MessagePathLength),
            BuildPath = data.DecodeShiftJISFull(BuildPathOffset, BuildPathLength),
            SceneVersion = st.SceneVersion,
            AreaType = st.AreaType,
            StageId = st.StageId,
            AreaOriginX = st.AreaOriginX,
            AreaOriginY = st.AreaOriginY,
            AreaOriginZ = st.AreaOriginZ,
            AreaOriginW = st.AreaOriginW,
            AreaMetadata = st.AreaMetadata,
            AreaConfig0 = st.AreaConfig0,
            AreaConfig1 = st.AreaConfig1,
            AreaConfig2 = st.AreaConfig2,
            AreaConfig3 = st.AreaConfig3,
            AreaConfig4 = st.AreaConfig4,
        };

        ReadEntryPool(scene, data, st);
        ReadScriptBlocks(scene, data, st);
        ReadWaypoints(scene, data, st);
        return scene;
    }

    private static void ReadEntryPool(SceneFile scene, byte[] data, SectionTableData st)
    {
        uint dataBase = st.DataBaseOffset;
        if (dataBase == 0 || dataBase >= (uint)data.Length) return;

        uint relOffset = 0;
        for (int guard = 0; guard < MaxEntryChain; guard++)
        {
            int absOffset = (int)(dataBase + relOffset);
            if (absOffset + EntryData.Size > data.Length) break;

            var wire = EntryData.Read(data.AsSpan(absOffset, EntryData.Size));
            var entry = wire.ToModel();
            entry.ScriptBlocks.Capacity = (int)wire.ScriptBlockCount;
            scene.Entries.Add(entry);

            if (wire.NextEntry == 0) break;
            relOffset = wire.NextEntry;
        }
    }

    /// <summary>
    /// Block lists are looked up at entry index times the entry size, not through the
    /// entry chain, and a block offset already visited ends that entry's list.
    /// </summary>
    private static void ReadScriptBlocks(SceneFile scene, byte[] data, SectionTableData st)
    {
        int scriptBase = (int)(st.DataBaseOffset + st.EntryPoolSize);
        var visited = new HashSet<int>();

        for (int entryIndex = 0; entryIndex < scene.Entries.Count; entryIndex++)
        {
            int entryOffset = (int)st.DataBaseOffset + entryIndex * EntryData.Size;
            var wireEntry = EntryData.Read(data.AsSpan(entryOffset, EntryData.Size));
            if (wireEntry.ScriptBlockCount == 0) continue;

            uint blockRelOffset = wireEntry.ScriptBlockOffset;
            for (int guard = 0; guard < MaxBlockChain; guard++)
            {
                int blockOffset = scriptBase + (int)blockRelOffset;
                if (blockOffset + ScriptBlockData.Size > data.Length) break;
                if (!visited.Add(blockOffset)) break;

                var wire = ScriptBlockData.Read(data.AsSpan(blockOffset, ScriptBlockData.Size));
                var block = wire.ToModel();

                int bytecodeStart = blockOffset + ScriptBlockData.Size;
                int bytecodeEnd = bytecodeStart + (int)wire.BytecodeSize;
                ReadBytecode(block, data, bytecodeStart, bytecodeEnd);

                int paramEnd = bytecodeEnd + (int)wire.ParamDataSize;
                if (paramEnd <= data.Length && wire.ParamDataSize > 0)
                    block.ParamData = data[bytecodeEnd..paramEnd];

                scene.Entries[entryIndex].ScriptBlocks.Add(block);

                if (wire.NextBlock == 0) break;
                blockRelOffset = wire.NextBlock;
            }
        }
    }

    /// <summary>
    /// A word pair counts as an instruction header when the opcode is in range, the
    /// size is 8..MaxInstructionSize and the instruction fits the region. Every other
    /// word joins a raw data run.
    /// </summary>
    private static void ReadBytecode(ScriptBlock block, byte[] data, int start, int end)
    {
        int pos = start;
        int dataStart = -1;

        void FlushData()
        {
            if (dataStart >= 0 && dataStart < pos)
            {
                block.Elements.Add(new ScriptData { Bytes = data[dataStart..pos] });
                dataStart = -1;
            }
        }

        while (pos < end)
        {
            if (pos + 8 <= end)
            {
                uint opcode = BigEndian.ReadUInt32(data, pos);
                uint size = BigEndian.ReadUInt32(data, pos + 4);

                if (OpcodeTable.IsValid(opcode) && size >= 8 && size <= OpcodeTable.MaxInstructionSize && pos + (int)size <= end)
                {
                    FlushData();
                    var args = new uint[(int)(size - 8) / 4];
                    for (int p = 0; p < args.Length; p++)
                        args[p] = BigEndian.ReadUInt32(data, pos + 8 + p * 4);

                    block.Elements.Add(new ScriptInstruction { Opcode = opcode, Size = size, RawParams = args });
                    pos += (int)size;
                    continue;
                }
            }

            if (dataStart < 0) dataStart = pos;
            pos += 4;
        }

        pos = end;
        FlushData();
    }

    private static void ReadWaypoints(SceneFile scene, byte[] data, SectionTableData st)
    {
        uint start = st.WaypointOffset;
        if (start == 0 || start >= data.Length) return;

        for (int offset = (int)start; offset + WaypointData.Size <= data.Length; offset += WaypointData.Size)
            scene.Waypoints.Add(WaypointData.Read(data.AsSpan(offset, WaypointData.Size)).ToModel());
    }

    public static void Write(SceneFile scene, string path) => File.WriteAllBytes(path, Write(scene));

    public static byte[] Write(SceneFile scene)
    {
        int entryPoolSize = scene.Entries.Count * EntryData.Size;
        int scriptBase = EntryPoolStart + entryPoolSize;
        int scriptSectionSize = scene.Entries.SelectMany(e => e.ScriptBlocks).Sum(BlockSize);
        int waypointStart = scriptBase + scriptSectionSize;
        int waypointSectionSize = scene.Waypoints.Count * WaypointData.Size;

        var data = new byte[waypointStart + waypointSectionSize];

        var version = Encoding.ASCII.GetBytes(scene.Version);
        Array.Copy(version, 0, data, VersionOffset, Math.Min(version.Length, VersionLength));
        BigEndian.WriteUInt32(data, FlagsOffset, scene.Flags);
        scene.SceneName.WriteShiftJIS(data, SceneNameOffset, SceneNameLength);
        scene.Filename.WriteShiftJIS(data, FilenameOffset, FilenameLength);
        scene.MessagePath.WriteShiftJIS(data, MessagePathOffset, MessagePathLength);
        scene.BuildPath.WriteShiftJIS(data, BuildPathOffset, BuildPathLength);

        var sectionTable = new SectionTableData
        {
            SceneVersion = scene.SceneVersion,
            AreaType = scene.AreaType,
            StageId = scene.StageId,
            AreaOriginX = scene.AreaOriginX,
            AreaOriginY = scene.AreaOriginY,
            AreaOriginZ = scene.AreaOriginZ,
            AreaOriginW = scene.AreaOriginW,
            AreaMetadata = scene.AreaMetadata,
            AreaConfig0 = scene.AreaConfig0,
            AreaConfig1 = scene.AreaConfig1,
            AreaConfig2 = scene.AreaConfig2,
            AreaConfig3 = scene.AreaConfig3,
            AreaConfig4 = scene.AreaConfig4,
            EntryPoolSize = (uint)entryPoolSize,
            ScriptSectionSize = (uint)scriptSectionSize,
            DataBaseOffset = scene.Entries.Count > 0 ? EntryPoolStart : 0u,
            WaypointSectionSize = (uint)waypointSectionSize,
            WaypointOffset = scene.Waypoints.Count > 0 ? (uint)waypointStart : 0,
        };
        SectionTableData.Write(data.AsSpan(HeaderSize, SectionTableData.Size), in sectionTable);

        int scriptPos = scriptBase;
        for (int e = 0; e < scene.Entries.Count; e++)
        {
            var entry = scene.Entries[e];
            bool last = e == scene.Entries.Count - 1;
            var wireEntry = EntryData.FromModel(entry) with
            {
                ScriptBlockCount = (uint)entry.ScriptBlocks.Count,
                ScriptBlockOffset = entry.ScriptBlocks.Count > 0 ? (uint)(scriptPos - scriptBase) : 0,
                NextEntry = last ? 0 : (uint)((e + 1) * EntryData.Size),
            };
            EntryData.Write(data.AsSpan(EntryPoolStart + e * EntryData.Size, EntryData.Size), in wireEntry);

            for (int b = 0; b < entry.ScriptBlocks.Count; b++)
                scriptPos = WriteBlock(data, scriptPos, scriptBase, entry.ScriptBlocks[b], b == entry.ScriptBlocks.Count - 1);
        }

        for (int w = 0; w < scene.Waypoints.Count; w++)
        {
            bool last = w == scene.Waypoints.Count - 1;
            var wire = WaypointData.FromModel(scene.Waypoints[w]) with
            {
                NextOffset = last ? 0 : (uint)((w + 1) * WaypointData.Size),
            };
            WaypointData.Write(data.AsSpan(waypointStart + w * WaypointData.Size, WaypointData.Size), in wire);
        }

        return data;
    }

    private static int WriteBlock(byte[] data, int pos, int scriptBase, ScriptBlock block, bool last)
    {
        int bytecodeSize = BytecodeSize(block);
        int paramSize = block.ParamData.Length;
        int bytecodeStart = pos + ScriptBlockData.Size;
        int paramStart = bytecodeStart + bytecodeSize;
        int end = paramStart + paramSize;

        var wire = ScriptBlockData.FromModel(block) with
        {
            BytecodeSize = (uint)bytecodeSize,
            ParamDataSize = (uint)paramSize,
            BytecodeRelOffset = bytecodeSize > 0 ? (uint)(bytecodeStart - scriptBase) : 0,
            ParamRelOffset = paramSize > 0 ? (uint)(paramStart - scriptBase) : 0,
            NextBlock = last ? 0 : (uint)(end - scriptBase),
        };
        ScriptBlockData.Write(data.AsSpan(pos, ScriptBlockData.Size), in wire);

        int writePos = bytecodeStart;
        foreach (var element in block.Elements)
        {
            switch (element)
            {
                case ScriptInstruction instr:
                    BigEndian.WriteUInt32(data, writePos, instr.Opcode);
                    BigEndian.WriteUInt32(data, writePos + 4, instr.Size);
                    for (int p = 0; p < instr.RawParams.Length; p++)
                        BigEndian.WriteUInt32(data, writePos + 8 + p * 4, instr.RawParams[p]);
                    writePos += (int)instr.Size;
                    break;
                case ScriptData raw:
                    raw.Bytes.CopyTo(data, writePos);
                    writePos += raw.Bytes.Length;
                    break;
            }
        }

        block.ParamData.CopyTo(data, paramStart);
        return end;
    }

    private static int BlockSize(ScriptBlock block) =>
        ScriptBlockData.Size + BytecodeSize(block) + block.ParamData.Length;

    private static int BytecodeSize(ScriptBlock block) => block.Elements.Sum(element => element switch
    {
        ScriptInstruction instr => (int)instr.Size,
        ScriptData raw => raw.Bytes.Length,
        _ => 0,
    });
}
