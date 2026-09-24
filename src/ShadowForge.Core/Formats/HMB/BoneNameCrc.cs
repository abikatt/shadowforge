using System.Text;

namespace ShadowForge.Formats.HMB;

/// <summary>
/// CRC-32/BZIP2: polynomial 0x04C11DB7 (unreflected, MSB-first), init
/// 0xFFFFFFFF, final complement. The runtime hashes bone names with this and
/// matches track hashes against scene-node name hashes at sample time.
/// </summary>
public static class BoneNameCrc
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var tbl = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i << 24;
            for (int j = 0; j < 8; j++)
                c = (c & 0x80000000u) != 0 ? (c << 1) ^ 0x04C11DB7u : c << 1;
            tbl[i] = c;
        }
        return tbl;
    }

    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in bytes)
            crc = Table[(b ^ (crc >> 24)) & 0xFF] ^ (crc << 8);
        return ~crc;
    }

    public static uint Compute(string name)
        => Compute(Encoding.ASCII.GetBytes(name));
}
