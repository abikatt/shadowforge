using System.Buffers.Binary;

namespace ShadowForge.IO;

public static class BigEndian
{
    public static ushort ReadUInt16(byte[] data, int offset)
        => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    public static uint ReadUInt32(byte[] data, int offset)
        => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));

    public static short ReadInt16(byte[] data, int offset)
        => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset, 2));

    public static int ReadInt32(byte[] data, int offset)
        => BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));

    public static float ReadFloat(byte[] data, int offset)
        => BitConverter.UInt32BitsToSingle(ReadUInt32(data, offset));

    public static void WriteUInt16(byte[] data, int offset, ushort value)
        => BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset, 2), value);

    public static void WriteInt16(byte[] data, int offset, short value)
        => BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(offset, 2), value);

    public static void WriteUInt32(byte[] data, int offset, uint value)
        => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, 4), value);

    public static void WriteInt32(byte[] data, int offset, int value)
        => BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset, 4), value);

    public static void WriteFloat(byte[] data, int offset, float value)
        => WriteUInt32(data, offset, BitConverter.SingleToUInt32Bits(value));
}
