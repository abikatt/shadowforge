using System.Buffers.Binary;
using System.Numerics;
using ShadowForge.Marshal;
using System.Diagnostics.CodeAnalysis;

namespace ShadowForge.Tests.Marshal;

[BigEndian, StructSize(0x20)]
public partial struct TestWireStruct
{
    public uint Id;
    public ushort Flags;
    public short SignedVal;
    public float Position;
    public int SignedInt;
    public Vector3 Vec;
    [Computed] public uint Next;
}

public enum TestEnum : uint
{
    Alpha = 1,
    Beta = 2,
}

[BigEndian, StructSize(0x08)]
public partial struct TestNestedInner
{
    public uint A;
    public uint B;
}

[BigEndian, StructSize(0x18)]
public partial struct TestEncodedStringStruct
{
    public uint Id;
    [EncodedString(8, StringEncoding.ASCII)]
    public byte[] Name;
    public TestEnum Kind;
    public TestNestedInner Inner;
}

[BigEndian, StructSize(0x10)]
[SuppressMessage("ShadowForge.Marshal", "SF006",
    Justification = "The 4-byte gap is the subject under test.")]
public partial struct TestSkipOffsetStruct
{
    public uint A;
    [Skip] public int Ignored;
    [Offset(0x08)]
    public uint B;
    public uint C;
}

public sealed class GeneratorRoundTripTests
{
    [Fact]
    public void Read_ParsesAllFieldTypes()
    {
        var data = new byte[0x20];
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x00), 42);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(0x04), 0x00FF);
        BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(0x06), -100);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x08),
            BitConverter.SingleToUInt32Bits(3.14f));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0x0C), -999);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x10),
            BitConverter.SingleToUInt32Bits(1.0f));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x14),
            BitConverter.SingleToUInt32Bits(2.0f));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x18),
            BitConverter.SingleToUInt32Bits(3.0f));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x1C), 0xDEAD);

        var result = TestWireStruct.Read(data);

        Assert.Equal(42u, result.Id);
        Assert.Equal((ushort)0x00FF, result.Flags);
        Assert.Equal((short)-100, result.SignedVal);
        Assert.Equal(3.14f, result.Position);
        Assert.Equal(-999, result.SignedInt);
        Assert.Equal(1.0f, result.Vec.X);
        Assert.Equal(2.0f, result.Vec.Y);
        Assert.Equal(3.0f, result.Vec.Z);
        Assert.Equal(0xDEADu, result.Next);
    }

    [Fact]
    public void Write_ProducesCorrectBytes()
    {
        var s = new TestWireStruct
        {
            Id = 42,
            Flags = 0x00FF,
            SignedVal = -100,
            Position = 3.14f,
            SignedInt = -999,
            Vec = new Vector3(1.0f, 2.0f, 3.0f),
            Next = 0xDEAD,
        };

        var data = new byte[0x20];
        TestWireStruct.Write(data, in s);

        Assert.Equal(42u, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0x00)));
        Assert.Equal((ushort)0x00FF, BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(0x04)));
        Assert.Equal((short)-100, BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(0x06)));
        Assert.Equal(3.14f,
            BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0x08))));
        Assert.Equal(-999, BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0x0C)));
        Assert.Equal(0xDEADu, BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0x1C)));
    }

    [Fact]
    public void RoundTrip_ByteIdentical()
    {
        var original = new byte[0x20];
        new Random(42).NextBytes(original);

        var parsed = TestWireStruct.Read(original);
        var written = new byte[0x20];
        TestWireStruct.Write(written, in parsed);

        Assert.Equal(original, written);
    }

    [Fact]
    public void EncodedString_ReadWrite_RoundTrips()
    {
        var data = new byte[0x18];
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x00), 7);
        data[0x04] = 0x48; data[0x05] = 0x65; data[0x06] = 0x6C;
        data[0x07] = 0x6C; data[0x08] = 0x6F;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x0C), 2);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x10), 0xAA);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x14), 0xBB);

        var result = TestEncodedStringStruct.Read(data);

        Assert.Equal(7u, result.Id);
        Assert.Equal(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0, 0, 0 }, result.Name);
        Assert.Equal(TestEnum.Beta, result.Kind);
        Assert.Equal(0xAAu, result.Inner.A);
        Assert.Equal(0xBBu, result.Inner.B);

        var output = new byte[0x18];
        TestEncodedStringStruct.Write(output, in result);
        Assert.Equal(data, output);
    }

    [Fact]
    public void Skip_ExcludesField_Offset_JumpsForward()
    {
        var data = new byte[0x10];
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x00), 1);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x04), 0xFF);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x08), 2);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x0C), 3);

        var result = TestSkipOffsetStruct.Read(data);

        Assert.Equal(1u, result.A);
        Assert.Equal(0, result.Ignored);
        Assert.Equal(2u, result.B);
        Assert.Equal(3u, result.C);

        var output = new byte[0x10];
        TestSkipOffsetStruct.Write(output, in result);

        Assert.Equal(1u, BinaryPrimitives.ReadUInt32BigEndian(output.AsSpan(0x00)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(output.AsSpan(0x04)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(output.AsSpan(0x08)));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32BigEndian(output.AsSpan(0x0C)));
    }
}
