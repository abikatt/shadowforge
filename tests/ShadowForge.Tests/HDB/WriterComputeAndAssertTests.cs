using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HDB.Raw;

namespace ShadowForge.Tests.HDB;

public sealed class WriterComputeAndAssertTests
{
    [Fact]
    public void ChildPointerComputationMatchesDisk()
    {
        var raw = ModelReader.Read(File.ReadAllBytes(TestFile.HDB));
        var layout = BoneLayout.Build(raw);
        if (layout.Bones.Count == 0) return;

        for (int ft = 0; ft < layout.Bones.Count; ft++)
        {
            int computed = layout.ComputeChildPtr(ft);
            Assert.True(
                computed == layout.Bones[ft].ExpectedChildPtrDisk,
                $"ft={ft} Idx={layout.Bones[ft].Index}: " +
                $"computed={computed}, expected={layout.Bones[ft].ExpectedChildPtrDisk}");
        }
    }

    [Fact]
    public void SiblingPointerComputationMatchesDisk()
    {
        var raw = ModelReader.Read(File.ReadAllBytes(TestFile.HDB));
        var layout = BoneLayout.Build(raw);
        if (layout.Bones.Count == 0) return;

        for (int ft = 0; ft < layout.Bones.Count; ft++)
        {
            int computed = layout.ComputeSiblingPtr(ft);
            Assert.True(
                computed == layout.Bones[ft].ExpectedSiblingPtrDisk,
                $"ft={ft} Idx={layout.Bones[ft].Index}: " +
                $"computed={computed}, expected={layout.Bones[ft].ExpectedSiblingPtrDisk}");
        }
    }
}
