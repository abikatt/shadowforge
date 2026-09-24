using ShadowForge.IO;
using ShadowForge.Formats.RPJ.Wire;

namespace ShadowForge.Tests.RPJ;

public sealed class WireStructTests
{
    [Fact]
    public void RPJConditionData_RoundTrip_ByteIdentical()
    {
        var data = new byte[0x10];
        BigEndian.WriteUInt32(data, 0x00, 2);
        BigEndian.WriteUInt32(data, 0x04, 100);
        BigEndian.WriteUInt32(data, 0x08, 1);
        BigEndian.WriteUInt32(data, 0x0C, 50);

        var cond = ConditionData.Read(data);
        var output = new byte[0x10];
        ConditionData.Write(output, in cond);

        Assert.Equal(data, output);
        Assert.Equal(2u, cond.Type);
        Assert.Equal(100u, cond.Operand);
        Assert.Equal(1u, cond.Op);
        Assert.Equal(50u, cond.Value);
    }
}
