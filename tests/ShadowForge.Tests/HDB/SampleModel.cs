using ShadowForge.Formats.HDB;

namespace ShadowForge.Tests.HDB;

internal static class SampleModel
{
    /// <summary>
    /// The retail <see cref="TestFile.HDB"/> model, read and cooked.
    /// </summary>
    public static ModelFile Cooked() => ModelCooker.Bake(ModelReader.Read(TestFile.HDB));
}
