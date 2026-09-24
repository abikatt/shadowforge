namespace ShadowForge.Formats.HDB;

/// <summary>
/// Splits a type-3 payload into opcode + parameter commands and joins them back.
/// Write(Parse(bytes)) reproduces the input exactly.
/// </summary>
public static class RenderCommandStream
{
    /// <summary>
    /// Technique-state word that opens a staged (eye) draw bracket. It selects a
    /// shadow-receive mode and carries no inline payload.
    /// </summary>
    public const ushort StagedStateOpen = 0x0103;

    /// <summary>
    /// Technique-state word that closes the staged draw bracket and restores the default
    /// single-texture path. Binds nothing and carries no payload.
    /// </summary>
    public const ushort StagedStateClose = 0x05FF;

    private static readonly HashSet<byte> OneByteParamOpcodes = new()
    {
        1, 3, 4, 5, 6, 7, 8, 9, 10, 15, 17, 19, 22, 23, 26, 28, 29, 30,
        31, 34, 35, 39, 40, 43, 45, 57, 69, 79, 84, 89, 92,
        110, 120, 126, 131, 198, 215, 224,
    };

    /// <summary>
    /// IA-select opcodes. The 5-byte parameter is a mode byte, a big-endian u16 holding
    /// the index count minus 2, and a big-endian u16 start index into the index block.
    /// </summary>
    public static bool IsIndexSelect(int opcode) => opcode is 0x10 or 0x20 or 0x30;

    public static List<RenderCommand> Parse(byte[] data)
    {
        var commands = new List<RenderCommand>();
        int pos = 0;
        while (pos < data.Length)
        {
            byte opcode = data[pos++];
            int actual = Math.Min(ParamSize(opcode, data, pos), data.Length - pos);
            var paramData = new byte[actual];
            Array.Copy(data, pos, paramData, 0, actual);
            pos += actual;
            commands.Add(new RenderCommand { Opcode = opcode, Data = paramData });
        }
        return commands;
    }

    public static byte[] Write(List<RenderCommand> commands)
    {
        var bytes = new byte[GetSerializedLength(commands)];
        int pos = 0;
        foreach (var cmd in commands)
        {
            bytes[pos++] = cmd.Opcode;
            cmd.Data.CopyTo(bytes, pos);
            pos += cmd.Data.Length;
        }
        return bytes;
    }

    public static int GetSerializedLength(List<RenderCommand> commands)
    {
        int total = 0;
        foreach (var cmd in commands)
            total += 1 + cmd.Data.Length;
        return total;
    }

    /// <summary>
    /// 0x00 followed by 0xFF is the two-byte end marker. Any other 0x00 is a one-byte pad.
    /// </summary>
    private static int ParamSize(byte opcode, byte[] data, int paramPos)
    {
        int next = paramPos < data.Length ? data[paramPos] : -1;
        if (opcode == 0x00) return next == 0xFF ? 1 : 0;
        if (opcode is 0x50 or 0x60 or 0x61 or 0x62) return 1;
        if (opcode is 0x40 or 0x93) return 3;
        if (opcode == 0x02) return 1 + Math.Max(next, 0) * 2;
        if (IsIndexSelect(opcode) || opcode == 0x94) return 5;
        if (opcode == 0x19) return 4;
        return OneByteParamOpcodes.Contains(opcode) ? 1 : 0;
    }
}
