using System.Buffers.Binary;

namespace ShadowForge.IO;

public sealed class BigEndianReader : IDisposable
{
    private readonly BinaryReader _reader;

    public BigEndianReader(Stream stream, bool leaveOpen = false)
    {
        _reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen);
    }

    public long Position => _reader.BaseStream.Position;

    public void Seek(long offset) => _reader.BaseStream.Seek(offset, SeekOrigin.Begin);

    public byte ReadByte() => _reader.ReadByte();

    public ushort ReadUInt16()
        => BinaryPrimitives.ReadUInt16BigEndian(_reader.ReadBytes(2));

    public uint ReadUInt32()
        => BinaryPrimitives.ReadUInt32BigEndian(_reader.ReadBytes(4));

    public float ReadFloat()
        => BitConverter.UInt32BitsToSingle(ReadUInt32());

    /// <summary>
    /// Reads a fixed-width field and decodes it as ASCII up to the first NUL.
    /// </summary>
    public string ReadAsciiString(int length)
    {
        var bytes = _reader.ReadBytes(length);
        return bytes.DecodeASCII(0, bytes.Length);
    }

    public void Dispose() => _reader.Dispose();
}
