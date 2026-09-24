using System.Buffers.Binary;

namespace ShadowForge.IO;

public sealed class BigEndianWriter : IDisposable
{
    private readonly BinaryWriter _writer;
    private readonly byte[] _buf = new byte[4];

    public BigEndianWriter(Stream stream, bool leaveOpen = false)
    {
        _writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen);
    }

    public Stream BaseStream => _writer.BaseStream;

    public long Position => _writer.BaseStream.Position;

    public void WriteBytes(byte[] data) => _writer.Write(data);

    public void WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(_buf, value);
        _writer.Write(_buf, 0, 2);
    }

    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(_buf, value);
        _writer.Write(_buf, 0, 4);
    }

    public void WriteInt32(int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(_buf, value);
        _writer.Write(_buf, 0, 4);
    }

    public void WriteFloat(float value)
        => WriteUInt32(BitConverter.SingleToUInt32Bits(value));

    /// <summary>
    /// Writes exactly <paramref name="length"/> bytes: <paramref name="data"/> truncated or zero-padded to fit.
    /// </summary>
    public void WriteFixedBytes(byte[] data, int length)
    {
        if (data.Length >= length)
        {
            _writer.Write(data, 0, length);
            return;
        }
        _writer.Write(data);
        _writer.Write(new byte[length - data.Length]);
    }

    public void WriteZeros(int count) => _writer.Write(new byte[count]);

    public void Flush() => _writer.Flush();

    public void Dispose() => _writer.Dispose();
}
